using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trail.Application.Accounts;
using Trail.Application.Telemetry;

namespace Trail.Infrastructure.Email;

public sealed partial class InMemoryAccountEmailDelivery : IAccountEmailDelivery
{
    private readonly Channel<ScheduledAccountEmail> _channel;
    private readonly ApplicationMetrics _metrics;
    private readonly ILogger<InMemoryAccountEmailDelivery> _logger;

    public InMemoryAccountEmailDelivery(
        IOptions<AccountEmailDeliveryOptions> options,
        ApplicationMetrics metrics,
        ILogger<InMemoryAccountEmailDelivery> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(logger);

        _channel = Channel.CreateBounded<ScheduledAccountEmail>(new BoundedChannelOptions(options.Value.Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });
        _metrics = metrics;
        _logger = logger;
    }

    internal ChannelReader<ScheduledAccountEmail> Reader => _channel.Reader;

    public void Schedule(EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (_channel.Writer.TryWrite(new ScheduledAccountEmail(message, Activity.Current?.Context ?? default)))
        {
            return;
        }

        _metrics.RecordEmailFailed(message.Kind);
        LogEmailRejected(_logger, message.Kind);
    }

    internal void Close() => _channel.Writer.TryComplete();

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Account email of kind {Kind} was not queued: the queue is full or closed")]
    private static partial void LogEmailRejected(ILogger logger, string kind);
}

internal sealed record ScheduledAccountEmail(EmailMessage Message, ActivityContext Origin);
