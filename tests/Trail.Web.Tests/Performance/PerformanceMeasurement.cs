using System.Globalization;
using Trail.Infrastructure.Persistence;

namespace Trail.Web.Tests.Performance;

public sealed class PerformanceMeasurement
{
    public PerformanceMeasurement(
        string scenario,
        DatabaseProvider provider,
        string path,
        IReadOnlyList<TimeSpan> samples,
        PerformanceBudget budget)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentOutOfRangeException.ThrowIfZero(samples.Count);

        Scenario = scenario;
        Provider = provider;
        Path = path;
        Budget = budget;
        Samples = [.. samples.Order()];
    }

    public string Scenario { get; }

    public DatabaseProvider Provider { get; }

    public string Path { get; }

    public PerformanceBudget Budget { get; }

    public IReadOnlyList<TimeSpan> Samples { get; }

    public TimeSpan Min => Samples[0];

    public TimeSpan P50 => Percentile(0.50);

    public TimeSpan P95 => Percentile(0.95);

    public TimeSpan Max => Samples[^1];

    public bool IsWithinBudget => P95 <= Budget.ServerTime;

    public TimeSpan Percentile(double fraction)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(fraction, 0.0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fraction, 1.0);

        var rank = (int)Math.Ceiling(fraction * Samples.Count);
        return Samples[Math.Clamp(rank, 1, Samples.Count) - 1];
    }

    public string Describe()
    {
        var verdict = IsWithinBudget ? "within" : "OVER";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"[{verdict} budget] scenario '{Scenario}' on {Provider}: p50={Milliseconds(P50):F1} ms, p95={Milliseconds(P95):F1} ms, max={Milliseconds(Max):F1} ms, min={Milliseconds(Min):F1} ms (budget {Milliseconds(Budget.ServerTime):F0} ms on p95, {Samples.Count} samples after {Budget.WarmupRequests} warm-up requests, GET {Path})");
    }

    public string ToMarkdownRow() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"| {Scenario} | {Provider} | {Milliseconds(P50):F1} | {Milliseconds(P95):F1} | {Milliseconds(Max):F1} | {Milliseconds(Budget.ServerTime):F0} | {(IsWithinBudget ? "pass" : "FAIL")} |");

    private static double Milliseconds(TimeSpan value) => value.TotalMilliseconds;
}
