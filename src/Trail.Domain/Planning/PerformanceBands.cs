
using Trail.Domain.Common;

namespace Trail.Domain.Planning;

public sealed class PerformanceBands : IEquatable<PerformanceBands>
{
    public const int DefaultLowThreshold = 50;
    public const int DefaultHighThreshold = 70;

    private PerformanceBands()
    {
    }

    private PerformanceBands(int lowThreshold, int highThreshold)
    {
        LowThreshold = lowThreshold;
        HighThreshold = highThreshold;
    }

    public int LowThreshold { get; private set; }

    public int HighThreshold { get; private set; }

    public static PerformanceBands CreateDefault() => new(DefaultLowThreshold, DefaultHighThreshold);

    public static Result<PerformanceBands> Create(int lowThreshold, int highThreshold, ScoringRule scoringRule)
    {
        var minAllowed = scoringRule == ScoringRule.Cebraspe ? -100 : 0;

        if (lowThreshold < minAllowed || lowThreshold > 100)
        {
            return Result.Invalid(Message.Error("planning.bands.low_out_of_range", nameof(LowThreshold)));
        }

        if (highThreshold < minAllowed || highThreshold > 100)
        {
            return Result.Invalid(Message.Error("planning.bands.high_out_of_range", nameof(HighThreshold)));
        }

        if (lowThreshold >= highThreshold)
        {
            return Result.Invalid(Message.Error("planning.bands.low_must_be_less_than_high", nameof(LowThreshold)));
        }

        return Result.Ok(new PerformanceBands(lowThreshold, highThreshold));
    }

    public bool Equals(PerformanceBands? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return LowThreshold == other.LowThreshold && HighThreshold == other.HighThreshold;
    }

    public override bool Equals(object? obj) => obj is PerformanceBands other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(LowThreshold, HighThreshold);

    public static bool operator ==(PerformanceBands? left, PerformanceBands? right) => Equals(left, right);

    public static bool operator !=(PerformanceBands? left, PerformanceBands? right) => !Equals(left, right);
}
