using System.Reflection;
using CsCheck;
using Trail.Domain.Planning;

namespace Trail.Domain.Tests.Planning;

public sealed class PerformancePeriodTests
{
    [Fact]
    public void All_IncludesAnyDate()
    {
        var period = PerformancePeriod.All();
        var today = new DateOnly(2026, 10, 16);

        Assert.Equal(PerformancePeriodKind.All, period.Kind);
        Assert.Null(period.Days);
        Assert.Null(period.StartDate);
        Assert.Null(period.EndDate);
        Assert.True(period.Includes(today, today));
        Assert.True(period.Includes(today.AddDays(-100), today));
        Assert.True(period.Includes(today.AddDays(100), today));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-15)]
    public void LastDays_WithNonPositiveDays_ReturnsInvalid(int days)
    {
        var result = PerformancePeriod.LastDays(days);

        Assert.False(result.IsOk);
        Assert.Equal("planning.period.days_must_be_positive", result.Failure.Messages[0].Code);
        Assert.Equal(nameof(PerformancePeriod.Days), result.Failure.Messages[0].Target);
    }

    [Fact]
    public void LastDays_SpecificationExample_IncludesCorrectRange()
    {
        var period = PerformancePeriod.LastDays(15).Data;
        var referenceToday = new DateOnly(2026, 10, 16);

        Assert.True(period.Includes(new DateOnly(2026, 10, 2), referenceToday));
        Assert.True(period.Includes(new DateOnly(2026, 10, 16), referenceToday));
        Assert.False(period.Includes(new DateOnly(2026, 10, 1), referenceToday));
        Assert.False(period.Includes(new DateOnly(2026, 10, 17), referenceToday));
    }

    [Fact]
    public void DateRange_WithInvalidRange_ReturnsInvalid()
    {
        var start = new DateOnly(2026, 10, 20);
        var end = new DateOnly(2026, 10, 10);

        var result = PerformancePeriod.DateRange(start, end);

        Assert.False(result.IsOk);
        Assert.Equal("planning.period.invalid_date_range", result.Failure.Messages[0].Code);
        Assert.Equal(nameof(PerformancePeriod.EndDate), result.Failure.Messages[0].Target);
    }

    [Fact]
    public void DateRange_WithValidRange_IncludesOnlyWithinRange()
    {
        var start = new DateOnly(2026, 10, 5);
        var end = new DateOnly(2026, 10, 15);
        var period = PerformancePeriod.DateRange(start, end).Data;
        var today = new DateOnly(2026, 10, 16);

        Assert.Equal(PerformancePeriodKind.DateRange, period.Kind);
        Assert.True(period.Includes(start, today));
        Assert.True(period.Includes(end, today));
        Assert.True(period.Includes(new DateOnly(2026, 10, 10), today));
        Assert.False(period.Includes(new DateOnly(2026, 10, 4), today));
        Assert.False(period.Includes(new DateOnly(2026, 10, 16), today));
    }

    [Fact]
    public void Create_WithVariousKinds_ProducesExpectedResults()
    {
        var all = PerformancePeriod.Create(PerformancePeriodKind.All);
        Assert.True(all.IsOk);
        Assert.Equal(PerformancePeriodKind.All, all.Data.Kind);

        var lastDaysOk = PerformancePeriod.Create(PerformancePeriodKind.LastDays, days: 30);
        Assert.True(lastDaysOk.IsOk);
        Assert.Equal(30, lastDaysOk.Data.Days);

        var lastDaysInvalid = PerformancePeriod.Create(PerformancePeriodKind.LastDays, days: 0);
        Assert.False(lastDaysInvalid.IsOk);

        var rangeOk = PerformancePeriod.Create(
            PerformancePeriodKind.DateRange,
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 1, 31));
        Assert.True(rangeOk.IsOk);

        var rangeInvalid = PerformancePeriod.Create(
            PerformancePeriodKind.DateRange,
            startDate: new DateOnly(2026, 2, 1),
            endDate: new DateOnly(2026, 1, 1));
        Assert.False(rangeInvalid.IsOk);
    }

    [Fact]
    public void Equality_ChecksAllProperties()
    {
        var p1 = PerformancePeriod.LastDays(15).Data;
        var p2 = PerformancePeriod.LastDays(15).Data;
        var p3 = PerformancePeriod.LastDays(30).Data;
        var pAll = PerformancePeriod.All();

        Assert.True(p1 == p2);
        Assert.False(p1 != p2);
        Assert.True(p1 != p3);
        Assert.Equal(p1, p2);
        Assert.Equal(p1.GetHashCode(), p2.GetHashCode());
        Assert.False(p1.Equals(null));
        Assert.False(p1.Equals(pAll));
    }

    [Fact]
    public void Property_LastDaysInclusionMatchesExpectedInterval()
    {
        var genDays = Gen.Int[1, 365];
        var genOffset = Gen.Int[-500, 500];
        var baseDate = new DateOnly(2026, 1, 1);

        Gen.Select(genDays, genOffset, genOffset).Sample((days, todayOffset, testOffset) =>
        {
            var period = PerformancePeriod.LastDays(days).Data;
            var referenceToday = baseDate.AddDays(todayOffset);
            var testDate = baseDate.AddDays(testOffset);

            var expectedStart = referenceToday.AddDays(-(days - 1));
            var expectedIncluded = testDate >= expectedStart && testDate <= referenceToday;

            Assert.Equal(expectedIncluded, period.Includes(testDate, referenceToday));
        });
    }

    [Fact]
    public void Property_DateRangeInclusionMatchesStartAndEnd()
    {
        var baseDate = new DateOnly(2026, 1, 1);
        var genOffset = Gen.Int[-500, 500];
        var genSpan = Gen.Int[0, 365];

        Gen.Select(genOffset, genSpan, genOffset).Sample((startOffset, span, testOffset) =>
        {
            var start = baseDate.AddDays(startOffset);
            var end = start.AddDays(span);
            var period = PerformancePeriod.DateRange(start, end).Data;
            var testDate = baseDate.AddDays(testOffset);
            var referenceToday = baseDate;

            var expected = testDate >= start && testDate <= end;
            Assert.Equal(expected, period.Includes(testDate, referenceToday));
        });
    }

    [Fact]
    public void ParameterlessConstructor_InitializesEmpty()
    {
        var constructor = typeof(PerformancePeriod).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);

        Assert.NotNull(constructor);
        var period = (PerformancePeriod)constructor.Invoke(null);
        Assert.Equal(PerformancePeriodKind.All, period.Kind);
        Assert.Null(period.Days);
    }

    [Fact]
    public void Create_WithInvalidKind_ReturnsInvalid()
    {
        var result = PerformancePeriod.Create((PerformancePeriodKind)99);

        Assert.False(result.IsOk);
        Assert.Equal("planning.period.invalid_kind", result.Failure.Messages[0].Code);
    }

    [Fact]
    public void Includes_WithInvalidKind_ReturnsFalse()
    {
        var constructor = typeof(PerformancePeriod).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            [typeof(PerformancePeriodKind), typeof(int?), typeof(DateOnly?), typeof(DateOnly?)],
            null);

        Assert.NotNull(constructor);
        var period = (PerformancePeriod)constructor.Invoke([(PerformancePeriodKind)99, null, null, null]);
        var today = new DateOnly(2026, 10, 16);

        Assert.False(period.Includes(today, today));
    }

    [Fact]
    public void Equality_WithSameReference_ReturnsTrue()
    {
        var period = PerformancePeriod.All();

        Assert.True(period.Equals(period));
        Assert.True(period.Equals((object)period));
    }
}
