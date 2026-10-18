using System.Collections.Concurrent;
using Microsoft.AspNetCore.TestHost;

namespace Trail.Web.Tests.Results;

public sealed record RecordedLog(string Category, LogLevel Level, EventId EventId, IReadOnlyDictionary<string, object?> State);

public sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<RecordedLog> _logs = new();

    public IReadOnlyList<RecordedLog> Logs => [.. _logs];

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, _logs);

    public void Dispose()
    {
    }

    private sealed class RecordingLogger(string category, ConcurrentQueue<RecordedLog> logs) : ILogger
    {
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

            logs.Enqueue(new RecordedLog(category, logLevel, eventId, values));
        }
    }
}

public sealed class LogRecordingWebApplicationFactory : TrailWebApplicationFactory
{
    public RecordingLoggerProvider Recorder { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddSingleton<ILoggerProvider>(Recorder));
    }
}
