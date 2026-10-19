using System.Collections.Concurrent;
using Microsoft.AspNetCore.TestHost;

namespace Trail.Web.Tests.Results;

public sealed record RecordedLog(
    string Category,
    LogLevel Level,
    EventId EventId,
    IReadOnlyDictionary<string, object?> State,
    string Message = "",
    string? Exception = null,
    IReadOnlyList<string>? Scopes = null);

public sealed class RecordingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<RecordedLog> _logs = new();
    private IExternalScopeProvider? _scopeProvider;

    public IReadOnlyList<RecordedLog> Logs => [.. _logs];

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, _logs, () => _scopeProvider);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    public void Dispose()
    {
    }

    private sealed class RecordingLogger(string category, ConcurrentQueue<RecordedLog> logs, Func<IExternalScopeProvider?> scopeProvider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => scopeProvider()?.Push(state);

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

            var scopes = new List<string>();
            scopeProvider()?.ForEachScope(
                (scope, collected) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object?>> scopePairs)
                    {
                        collected.AddRange(scopePairs.Select(pair => $"{pair.Key}={pair.Value}"));
                    }
                    else if (scope is not null)
                    {
                        collected.Add(scope.ToString() ?? string.Empty);
                    }
                },
                scopes);

            logs.Enqueue(new RecordedLog(category, logLevel, eventId, values, formatter(state, exception), exception?.ToString(), scopes));
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
