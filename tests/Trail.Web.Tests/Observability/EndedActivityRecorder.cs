using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry;

namespace Trail.Web.Tests.Observability;

internal sealed class EndedActivityRecorder : BaseProcessor<Activity>
{
    private readonly ConcurrentQueue<Activity> _ended = new();

    public IReadOnlyCollection<Activity> Ended => _ended.ToArray();

    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);

        _ended.Enqueue(data);
    }
}
