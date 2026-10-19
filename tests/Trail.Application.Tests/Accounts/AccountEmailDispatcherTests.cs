using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trail.Application.Accounts;
using Trail.Application.Telemetry;
using Trail.Domain.Common;
using Trail.Infrastructure.Email;

namespace Trail.Application.Tests.Accounts;

public sealed class AccountEmailDispatcherTests : IDisposable
{
    private const string Recipient = "private.person@example.com";

    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    private readonly ApplicationMetrics _metrics = new(new TestMeterFactory());
    private readonly MeterListener _meterListener = new();
    private readonly ConcurrentQueue<(string Instrument, string Kind)> _measurements = new();
    private readonly RecordingLogger<InMemoryAccountEmailDelivery> _deliveryLogger = new();
    private readonly RecordingLogger<AccountEmailDispatcher> _dispatcherLogger = new();

    public AccountEmailDispatcherTests()
    {
        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter == _metrics.Meter)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _meterListener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "kind")
                {
                    _measurements.Enqueue((instrument.Name, (string)tag.Value!));
                }
            }
        });
        _meterListener.Start();
    }

    [Fact]
    public async Task Schedule_WhileRunning_SendsTheMessagesInOrderAndRecordsTheSentMetric()
    {
        var sender = new ControlledEmailSender();
        var (delivery, dispatcher) = Create(sender);
        await dispatcher.StartAsync(TestContext.Current.CancellationToken);

        delivery.Schedule(Email(EmailKinds.Confirmation, "first@example.com"));
        delivery.Schedule(Email(EmailKinds.PasswordReset, "second@example.com"));
        delivery.Schedule(Email(EmailKinds.RegistrationAttempt, "third@example.com"));
        await sender.WaitForAttemptsAsync(3);
        await dispatcher.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["first@example.com", "second@example.com", "third@example.com"], sender.Attempts.Select(message => message.ToAddress));
        Assert.Equal(
            [
                (ApplicationMetrics.EmailsSentInstrumentName, EmailKinds.Confirmation),
                (ApplicationMetrics.EmailsSentInstrumentName, EmailKinds.PasswordReset),
                (ApplicationMetrics.EmailsSentInstrumentName, EmailKinds.RegistrationAttempt),
            ],
            _measurements);
        Assert.Empty(_dispatcherLogger.Entries);
    }

    [Fact]
    public async Task Schedule_WhenTheSenderFails_RecordsAndLogsEachFailureWithoutTheRecipientAndKeepsSending()
    {
        var sender = new ControlledEmailSender
        {
            Outcome = message => message.Kind switch
            {
                EmailKinds.Confirmation => Result.Invalid(Message.Error("accounts.email.send_failed")),
                EmailKinds.PasswordReset => throw new InvalidOperationException($"Mailbox {Recipient} rejected the message"),
                _ => Result.Ok(),
            },
        };
        var (delivery, dispatcher) = Create(sender);
        await dispatcher.StartAsync(TestContext.Current.CancellationToken);

        delivery.Schedule(Email(EmailKinds.Confirmation));
        delivery.Schedule(Email(EmailKinds.PasswordReset));
        delivery.Schedule(Email(EmailKinds.RegistrationAttempt));
        await sender.WaitForAttemptsAsync(3);
        await dispatcher.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                (ApplicationMetrics.EmailsFailedInstrumentName, EmailKinds.Confirmation),
                (ApplicationMetrics.EmailsFailedInstrumentName, EmailKinds.PasswordReset),
                (ApplicationMetrics.EmailsSentInstrumentName, EmailKinds.RegistrationAttempt),
            ],
            _measurements);
        Assert.Collection(
            _dispatcherLogger.Entries,
            entry => AssertFailureLog(entry, EmailKinds.Confirmation, "accounts.email.send_failed"),
            entry => AssertFailureLog(entry, EmailKinds.PasswordReset, nameof(InvalidOperationException)));
    }

    [Fact]
    public async Task StopAsync_WithMessagesStillQueued_SendsThemBeforeStopping()
    {
        var smtp = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = new ControlledEmailSender { Gate = smtp.Task };
        var (delivery, dispatcher) = Create(sender, drainTimeout: TimeSpan.FromMinutes(5));
        await dispatcher.StartAsync(TestContext.Current.CancellationToken);
        delivery.Schedule(Email(EmailKinds.Confirmation));
        delivery.Schedule(Email(EmailKinds.PasswordReset));
        delivery.Schedule(Email(EmailKinds.RegistrationAttempt));
        await sender.FirstAttempt.WaitAsync(WaitTimeout, TestContext.Current.CancellationToken);

        var stopping = dispatcher.StopAsync(TestContext.Current.CancellationToken);

        Assert.False(stopping.IsCompleted);
        smtp.SetResult();
        await stopping.WaitAsync(WaitTimeout, TestContext.Current.CancellationToken);
        Assert.Equal(3, sender.Attempts.Count);
        Assert.Equal(3, _measurements.Count(measurement => measurement.Instrument == ApplicationMetrics.EmailsSentInstrumentName));
        Assert.Empty(_dispatcherLogger.Entries);
    }

    [Fact]
    public async Task StopAsync_WhenTheQueueDoesNotDrainInTime_StopsAtTheTimeoutAndRecordsTheLostMessages()
    {
        var sender = new ControlledEmailSender { Gate = new TaskCompletionSource().Task };
        var (delivery, dispatcher) = Create(sender, drainTimeout: TimeSpan.FromMilliseconds(200));
        await dispatcher.StartAsync(TestContext.Current.CancellationToken);
        delivery.Schedule(Email(EmailKinds.Confirmation));
        delivery.Schedule(Email(EmailKinds.PasswordReset));
        delivery.Schedule(Email(EmailKinds.RegistrationAttempt));
        await sender.FirstAttempt.WaitAsync(WaitTimeout, TestContext.Current.CancellationToken);

        await dispatcher.StopAsync(TestContext.Current.CancellationToken).WaitAsync(WaitTimeout, TestContext.Current.CancellationToken);

        Assert.True(sender.ObservedCancellation);
        Assert.Equal(
            [EmailKinds.Confirmation, EmailKinds.PasswordReset, EmailKinds.RegistrationAttempt],
            _measurements.Where(measurement => measurement.Instrument == ApplicationMetrics.EmailsFailedInstrumentName).Select(measurement => measurement.Kind));
        Assert.Collection(
            _dispatcherLogger.Entries,
            entry => AssertFailureLog(entry, EmailKinds.Confirmation, "ShutdownTimeout"),
            entry =>
            {
                Assert.Equal(LogLevel.Warning, entry.Level);
                Assert.Equal(2, entry.State["Count"]);
            });
    }

    [Fact]
    public async Task StopAsync_CalledAgainAfterTheHostDisposedTheDispatcher_ReturnsWithoutThrowing()
    {
        var (_, dispatcher) = Create(new ControlledEmailSender());
        await dispatcher.StartAsync(TestContext.Current.CancellationToken);
        await dispatcher.StopAsync(TestContext.Current.CancellationToken);
        dispatcher.Dispose();

        await dispatcher.StopAsync(TestContext.Current.CancellationToken);

        Assert.Empty(_dispatcherLogger.Entries);
    }

    [Fact]
    public async Task Schedule_AfterStopping_RecordsTheFailureAndLogsTheKindWithoutTheRecipient()
    {
        var (delivery, dispatcher) = Create(new ControlledEmailSender());
        await dispatcher.StartAsync(TestContext.Current.CancellationToken);
        await dispatcher.StopAsync(TestContext.Current.CancellationToken);

        delivery.Schedule(Email(EmailKinds.PasswordReset));

        Assert.Equal([(ApplicationMetrics.EmailsFailedInstrumentName, EmailKinds.PasswordReset)], _measurements);
        AssertFailureLog(Assert.Single(_deliveryLogger.Entries), EmailKinds.PasswordReset, failure: null);
    }

    [Fact]
    public void Schedule_WhenTheQueueIsFull_RecordsTheFailureAndLogsTheKindWithoutTheRecipient()
    {
        var (delivery, _) = Create(new ControlledEmailSender(), capacity: 1);

        delivery.Schedule(Email(EmailKinds.Confirmation));
        delivery.Schedule(Email(EmailKinds.RegistrationAttempt));

        Assert.Equal([(ApplicationMetrics.EmailsFailedInstrumentName, EmailKinds.RegistrationAttempt)], _measurements);
        AssertFailureLog(Assert.Single(_deliveryLogger.Entries), EmailKinds.RegistrationAttempt, failure: null);
    }

    [Fact]
    public async Task Schedule_SendsUnderTheTraceThatScheduledTheMessageWithoutTheRecipient()
    {
        var spans = new ConcurrentQueue<Activity>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ApplicationTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = spans.Enqueue,
        };
        ActivitySource.AddActivityListener(activityListener);
        var sender = new ControlledEmailSender();
        var (delivery, dispatcher) = Create(sender);
        await dispatcher.StartAsync(TestContext.Current.CancellationToken);
        ActivityTraceId requestTrace;

        using (var request = new Activity("account-request").Start())
        {
            requestTrace = request.TraceId;
            delivery.Schedule(Email(EmailKinds.Confirmation));
        }

        await sender.WaitForAttemptsAsync(1);
        await dispatcher.StopAsync(TestContext.Current.CancellationToken);
        var span = Assert.Single(spans, activity => activity.OperationName == "trail.email.dispatch" && activity.TraceId == requestTrace);
        Assert.Equal(EmailKinds.Confirmation, span.GetTagItem("trail.email.kind"));
        Assert.All(span.TagObjects, tag => Assert.DoesNotContain(Recipient, Convert.ToString(tag.Value, CultureInfo.InvariantCulture) ?? string.Empty, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        _meterListener.Dispose();
    }

    private (InMemoryAccountEmailDelivery Delivery, AccountEmailDispatcher Dispatcher) Create(
        ControlledEmailSender sender,
        TimeSpan? drainTimeout = null,
        int capacity = 500)
    {
        var options = Options.Create(new AccountEmailDeliveryOptions
        {
            Capacity = capacity,
            ShutdownDrainTimeout = drainTimeout ?? TimeSpan.FromMinutes(5),
        });
        var services = new ServiceCollection()
            .AddScoped<IEmailSender>(_ => sender)
            .BuildServiceProvider();
        var delivery = new InMemoryAccountEmailDelivery(options, _metrics, _deliveryLogger);
        var dispatcher = new AccountEmailDispatcher(delivery, services.GetRequiredService<IServiceScopeFactory>(), _metrics, options, _dispatcherLogger);

        return (delivery, dispatcher);
    }

    private static EmailMessage Email(string kind, string toAddress = Recipient) =>
        new(toAddress, "Subject", "<p>Body</p>", "Body", kind);

    private static void AssertFailureLog(LogEntry entry, string kind, string? failure)
    {
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(kind, entry.State["Kind"]);
        if (failure is not null)
        {
            Assert.Equal(failure, entry.State["Failure"]);
        }

        Assert.Null(entry.Exception);
        Assert.DoesNotContain(Recipient, entry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.All(entry.State.Values, value => Assert.DoesNotContain(Recipient, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class ControlledEmailSender : IEmailSender
    {
        private readonly ConcurrentQueue<EmailMessage> _attempts = new();
        private readonly TaskCompletionSource _firstAttempt = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource _attempted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private volatile bool _observedCancellation;

        public bool IsSmtpConfigured => true;

        public Task? Gate { get; init; }

        public Func<EmailMessage, Result>? Outcome { get; init; }

        public IReadOnlyList<EmailMessage> Attempts => [.. _attempts];

        public Task FirstAttempt => _firstAttempt.Task;

        public bool ObservedCancellation => _observedCancellation;

        public async Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            _firstAttempt.TrySetResult();

            if (Gate is not null)
            {
                try
                {
                    await Gate.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _observedCancellation = true;
                    throw;
                }
            }

            _attempts.Enqueue(message);
            Interlocked.Exchange(ref _attempted, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
            return Outcome?.Invoke(message) ?? Result.Ok();
        }

        public async Task WaitForAttemptsAsync(int count)
        {
            while (true)
            {
                var attempted = Volatile.Read(ref _attempted).Task;
                if (_attempts.Count >= count)
                {
                    return;
                }

                await attempted.WaitAsync(WaitTimeout, TestContext.Current.CancellationToken);
            }
        }
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (var meter in _meters)
            {
                meter.Dispose();
            }
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, IReadOnlyDictionary<string, object?> State, Exception? Exception);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();

        public IReadOnlyList<LogEntry> Entries => [.. _entries];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                foreach (var (key, value) in pairs)
                {
                    values[key] = value;
                }
            }

            _entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), values, exception));
        }
    }
}
