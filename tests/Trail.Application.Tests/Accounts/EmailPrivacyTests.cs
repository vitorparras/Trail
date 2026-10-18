using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trail.Application.Accounts;
using Trail.Application.Telemetry;
using Trail.Infrastructure.Email;

namespace Trail.Application.Tests.Accounts;

public sealed class EmailPrivacyTests
{
    private const string Recipient = "private.person@example.com";

    [Fact]
    public async Task SendAsync_WhenDeliveryFails_KeepsTheRecipientOutOfLogsAndTraces()
    {
        var logger = new RecordingLogger<MailKitEmailSender>();
        var spans = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ApplicationTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = spans.Enqueue,
        };
        ActivitySource.AddActivityListener(listener);
        using var parent = new Activity("email-privacy-test").Start();
        var sender = new MailKitEmailSender(
            Options.Create(new EmailOptions { Host = "127.0.0.1", Port = 54321, FromAddress = "noreply@trail.local", FromName = "Trail" }),
            logger);

        var result = await sender.SendAsync(
            new EmailMessage(Recipient, "Reset", "<p>Reset</p>", "Reset", EmailKinds.PasswordReset),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(EmailKinds.PasswordReset, entry.State["Kind"]);
        Assert.Null(entry.Exception);
        Assert.DoesNotContain(Recipient, entry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.All(entry.State.Values, value => Assert.DoesNotContain(Recipient, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, StringComparison.OrdinalIgnoreCase));

        var span = Assert.Single(spans, activity => activity.TraceId == parent.TraceId);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.DoesNotContain(Recipient, span.StatusDescription ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.All(span.TagObjects, tag => Assert.DoesNotContain(Recipient, Convert.ToString(tag.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, StringComparison.OrdinalIgnoreCase));
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
