using System.Reflection;
using CsCheck;
using Trail.Domain.Planning;

namespace Trail.Domain.Tests.Planning;

public sealed class PerformanceBandsTests
{
    [Fact]
    public void CreateDefault_Returns50And70()
    {
        var bands = PerformanceBands.CreateDefault();

        Assert.Equal(50, bands.LowThreshold);
        Assert.Equal(70, bands.HighThreshold);
    }

    [Fact]
    public void Create_Cebraspe_AllowsNegativeThresholdsWithinRange()
    {
        var result = PerformanceBands.Create(-20, 40, ScoringRule.Cebraspe);

        Assert.True(result.IsOk);
        Assert.Equal(-20, result.Data.LowThreshold);
        Assert.Equal(40, result.Data.HighThreshold);
    }

    [Fact]
    public void Create_Standard_RejectsNegativeThreshold()
    {
        var result = PerformanceBands.Create(-10, 50, ScoringRule.Standard);

        Assert.False(result.IsOk);
        Assert.Equal("planning.bands.low_out_of_range", result.Failure.Messages[0].Code);
        Assert.Equal(nameof(PerformanceBands.LowThreshold), result.Failure.Messages[0].Target);
    }

    [Fact]
    public void Create_WhenLowThresholdGreaterThanOrEqualToHigh_ReturnsInvalid()
    {
        var equalResult = PerformanceBands.Create(60, 60, ScoringRule.Standard);
        var greaterResult = PerformanceBands.Create(70, 60, ScoringRule.Standard);

        Assert.False(equalResult.IsOk);
        Assert.Equal("planning.bands.low_must_be_less_than_high", equalResult.Failure.Messages[0].Code);
        Assert.False(greaterResult.IsOk);
        Assert.Equal("planning.bands.low_must_be_less_than_high", greaterResult.Failure.Messages[0].Code);
    }

    [Fact]
    public void Create_WhenThresholdsOutOfRange_ReturnsInvalid()
    {
        var lowTooHigh = PerformanceBands.Create(101, 120, ScoringRule.Standard);
        Assert.False(lowTooHigh.IsOk);
        Assert.Equal("planning.bands.low_out_of_range", lowTooHigh.Failure.Messages[0].Code);

        var highTooHigh = PerformanceBands.Create(50, 101, ScoringRule.Standard);
        Assert.False(highTooHigh.IsOk);
        Assert.Equal("planning.bands.high_out_of_range", highTooHigh.Failure.Messages[0].Code);

        var lowTooLowCebraspe = PerformanceBands.Create(-101, 50, ScoringRule.Cebraspe);
        Assert.False(lowTooLowCebraspe.IsOk);
        Assert.Equal("planning.bands.low_out_of_range", lowTooLowCebraspe.Failure.Messages[0].Code);
    }

    [Fact]
    public void Equality_ChecksBothThresholds()
    {
        var b1 = PerformanceBands.Create(40, 60, ScoringRule.Standard).Data;
        var b2 = PerformanceBands.Create(40, 60, ScoringRule.Standard).Data;
        var b3 = PerformanceBands.Create(45, 60, ScoringRule.Standard).Data;

        Assert.True(b1 == b2);
        Assert.False(b1 != b2);
        Assert.True(b1 != b3);
        Assert.Equal(b1, b2);
        Assert.Equal(b1.GetHashCode(), b2.GetHashCode());
        Assert.False(b1.Equals(null));
    }

    [Fact]
    public void Property_CebraspeValidThresholdsAlwaysSucceed()
    {
        Gen.Select(Gen.Int[-100, 99], Gen.Int[-100, 100]).Sample((val1, val2) =>
        {
            var low = Math.Min(val1, val2);
            var high = Math.Max(val1, val2);
            if (low == high)
            {
                if (high < 100)
                {
                    high++;
                }
                else
                {
                    low--;
                }
            }

            var result = PerformanceBands.Create(low, high, ScoringRule.Cebraspe);
            Assert.True(result.IsOk);
            Assert.Equal(low, result.Data.LowThreshold);
            Assert.Equal(high, result.Data.HighThreshold);
        });
    }

    [Fact]
    public void Property_StandardValidThresholdsAlwaysSucceed()
    {
        Gen.Select(Gen.Int[0, 99], Gen.Int[0, 100]).Sample((val1, val2) =>
        {
            var low = Math.Min(val1, val2);
            var high = Math.Max(val1, val2);
            if (low == high)
            {
                if (high < 100)
                {
                    high++;
                }
                else
                {
                    low--;
                }
            }

            var result = PerformanceBands.Create(low, high, ScoringRule.Standard);
            Assert.True(result.IsOk);
            Assert.Equal(low, result.Data.LowThreshold);
            Assert.Equal(high, result.Data.HighThreshold);
        });
    }

    [Fact]
    public void Property_WhenLowGteHigh_AlwaysReturnsInvalid()
    {
        Gen.Select(Gen.Int[0, 100], Gen.Int[0, 100]).Sample((val1, val2) =>
        {
            var low = Math.Max(val1, val2);
            var high = Math.Min(val1, val2);

            var result = PerformanceBands.Create(low, high, ScoringRule.Standard);
            Assert.False(result.IsOk);
            Assert.Equal("planning.bands.low_must_be_less_than_high", result.Failure.Messages[0].Code);
        });
    }

    [Fact]
    public void ParameterlessConstructor_InitializesEmpty()
    {
        var constructor = typeof(PerformanceBands).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);

        Assert.NotNull(constructor);
        var bands = (PerformanceBands)constructor.Invoke(null);
        Assert.Equal(0, bands.LowThreshold);
        Assert.Equal(0, bands.HighThreshold);
    }

    [Fact]
    public void Equality_WithSameReference_ReturnsTrue()
    {
        var bands = PerformanceBands.CreateDefault();

        Assert.True(bands.Equals(bands));
        Assert.True(bands.Equals((object)bands));
    }
}
