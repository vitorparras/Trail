namespace Trail.Web.Tests.Performance;

public sealed record PerformanceBudget(TimeSpan ServerTime, int WarmupRequests, int SampledRequests)
{
    public static PerformanceBudget Default { get; } = new(TimeSpan.FromMilliseconds(300), 10, 20);
}
