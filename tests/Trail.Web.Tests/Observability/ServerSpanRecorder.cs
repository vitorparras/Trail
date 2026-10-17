using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry;

namespace Trail.Web.Tests.Observability;

internal sealed class ServerSpanRecorder : BaseProcessor<Activity>
{
    private static readonly TimeSpan EndTimeout = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<ActivityTraceId, TaskCompletionSource<Activity>> _pending = new();

    public TracedRequest Trace(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var traceId = ActivityTraceId.CreateRandom();
        var ended = new TaskCompletionSource<Activity>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[traceId] = ended;
        request.Headers.Add("traceparent", $"00-{traceId.ToHexString()}-{ActivitySpanId.CreateRandom().ToHexString()}-01");

        return new TracedRequest(traceId, ended.Task.WaitAsync(EndTimeout, TestContext.Current.CancellationToken));
    }

    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Kind == ActivityKind.Server && _pending.TryGetValue(data.TraceId, out var ended))
        {
            ended.TrySetResult(data);
        }
    }
}

internal sealed record TracedRequest(ActivityTraceId TraceId, Task<Activity> ServerSpanEnded);
