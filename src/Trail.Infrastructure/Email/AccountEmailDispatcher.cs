using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trail.Application.Accounts;
using Trail.Application.Telemetry;

namespace Trail.Infrastructure.Email;

public sealed partial class AccountEmailDispatcher : BackgroundService
{
    private const string ShutdownTimeoutFailure = "ShutdownTimeout";

    private readonly InMemoryAccountEmailDelivery _delivery;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ApplicationMetrics _metrics;
    private readonly ILogger<AccountEmailDispatcher> _logger;
    private readonly TimeSpan _drainTimeout;
    private readonly CancellationTokenSource _drainLimit = new();
    private int _stopRequested;

    public AccountEmailDispatcher(
        InMemoryAccountEmailDelivery delivery,
        IServiceScopeFactory scopeFactory,
        ApplicationMetrics metrics,
        IOptions<AccountEmailDeliveryOptions> options,
        ILogger<AccountEmailDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _delivery = delivery;
        _scopeFactory = scopeFactory;
        _metrics = metrics;
        _drainTimeout = options.Value.ShutdownDrainTimeout;
        _logger = logger;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) == 0)
        {
            _delivery.Close();
            _drainLimit.CancelAfter(_drainTimeout);
        }

        using var hostTimeout = cancellationToken.Register(static state => ((CancellationTokenSource)state!).Cancel(), _drainLimit);

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _drainLimit.Cancel();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _delivery.Reader;
        var drainLimit = _drainLimit.Token;

        try
        {
            while (!drainLimit.IsCancellationRequested && await reader.WaitToReadAsync(drainLimit))
            {
                while (!drainLimit.IsCancellationRequested && reader.TryRead(out var email))
                {
                    await DeliverAsync(email, drainLimit);
                }
            }
        }
        catch (OperationCanceledException) when (drainLimit.IsCancellationRequested)
        {
        }

        DropRemaining(reader);
    }

    private async Task DeliverAsync(ScheduledAccountEmail email, CancellationToken cancellationToken)
    {
        var kind = email.Message.Kind;
        using var activity = ApplicationTelemetry.ActivitySource.StartActivity("trail.email.dispatch", ActivityKind.Consumer, email.Origin);
        activity?.SetTag("trail.email.kind", kind);

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var result = await sender.SendAsync(email.Message, cancellationToken);

            if (result.IsOk)
            {
                _metrics.RecordEmailSent(kind);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return;
            }

            RecordFailure(kind, string.Join(',', result.Messages.Select(message => message.Code)), activity);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            RecordFailure(kind, ShutdownTimeoutFailure, activity);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            RecordFailure(kind, exception.GetType().Name, activity);
        }
    }

    private void RecordFailure(string kind, string failure, Activity? activity)
    {
        _metrics.RecordEmailFailed(kind);
        activity?.SetStatus(ActivityStatusCode.Error, failure);
        LogEmailNotDelivered(_logger, kind, failure);
    }

    private void DropRemaining(ChannelReader<ScheduledAccountEmail> reader)
    {
        var dropped = 0;
        while (reader.TryRead(out var email))
        {
            _metrics.RecordEmailFailed(email.Message.Kind);
            dropped++;
        }

        if (dropped > 0)
        {
            LogEmailsDropped(_logger, dropped);
        }
    }

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Account email of kind {Kind} was not delivered: {Failure}")]
    private static partial void LogEmailNotDelivered(ILogger logger, string kind, string failure);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "{Count} queued account emails were dropped when the application stopped")]
    private static partial void LogEmailsDropped(ILogger logger, int count);
}
