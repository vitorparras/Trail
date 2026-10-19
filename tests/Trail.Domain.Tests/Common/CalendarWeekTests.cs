using System.Globalization;
using CsCheck;
using Trail.Domain.Common;

namespace Trail.Domain.Tests.Common;

public sealed class CalendarWeekTests
{
    private static readonly Gen<DateOnly> AnyDate = Gen.Int[DateOnly.MinValue.DayNumber, DateOnly.MaxValue.DayNumber - (2 * CalendarWeek.DaysInWeek)]
        .Select(DateOnly.FromDayNumber);

    [Theory]
    [InlineData("2026-10-12", "2026-10-12", "2026-10-18")]
    [InlineData("2026-10-15", "2026-10-12", "2026-10-18")]
    [InlineData("2026-10-18", "2026-10-12", "2026-10-18")]
    [InlineData("2026-10-19", "2026-10-19", "2026-10-25")]
    [InlineData("2026-12-31", "2026-12-28", "2027-01-03")]
    [InlineData("2028-02-29", "2028-02-28", "2028-03-05")]
    public void Of_KnownDates_ReturnsTheWeekFromMondayToSunday(string date, string start, string end)
    {
        var week = CalendarWeek.Of(DateOnly.Parse(date, CultureInfo.InvariantCulture));

        Assert.Equal(DateOnly.Parse(start, CultureInfo.InvariantCulture), week.Start);
        Assert.Equal(DateOnly.Parse(end, CultureInfo.InvariantCulture), week.End);
        Assert.Equal(DayOfWeek.Monday, week.Start.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, week.End.DayOfWeek);
    }

    [Fact]
    public void Of_AnyDate_ReturnsASevenDayWeekStartingOnMondayThatContainsTheDate()
    {
        AnyDate.Sample(date =>
        {
            var week = CalendarWeek.Of(date);

            Assert.True(week.Contains(date));
            Assert.Equal(DayOfWeek.Monday, week.Start.DayOfWeek);
            Assert.Equal(DayOfWeek.Sunday, week.End.DayOfWeek);
            Assert.Equal(CalendarWeek.DaysInWeek - 1, week.End.DayNumber - week.Start.DayNumber);
            Assert.Equal(Enumerable.Range(week.Start.DayNumber, CalendarWeek.DaysInWeek).Select(DateOnly.FromDayNumber), week.Days);
        });
    }

    [Fact]
    public void Contains_AnyDateAndAnyWeek_IsTrueOnlyForTheWeekOfTheDate()
    {
        AnyDate.Select(Gen.Int[-3 * CalendarWeek.DaysInWeek, 3 * CalendarWeek.DaysInWeek]).Sample((date, offset) =>
        {
            var other = DateOnly.FromDayNumber(Math.Clamp(date.DayNumber + offset, DateOnly.MinValue.DayNumber, DateOnly.MaxValue.DayNumber - (2 * CalendarWeek.DaysInWeek)));
            var week = CalendarWeek.Of(other);

            Assert.Equal(week == CalendarWeek.Of(date), week.Contains(date));
        });
    }

    [Fact]
    public void Of_EveryDayOfAWeek_ReturnsThatSameWeek()
    {
        AnyDate.Sample(date =>
        {
            var week = CalendarWeek.Of(date);

            Assert.All(week.Days, day => Assert.Equal(week, CalendarWeek.Of(day)));
            Assert.False(week.Contains(week.Start.AddDays(-1)));
            Assert.False(week.Contains(week.End.AddDays(1)));
        });
    }

    [Fact]
    public void NextAndPrevious_AnyWeek_AreTheAdjacentWeeks()
    {
        Gen.Int[DateOnly.MinValue.DayNumber + CalendarWeek.DaysInWeek, DateOnly.MaxValue.DayNumber - (2 * CalendarWeek.DaysInWeek)]
            .Select(DateOnly.FromDayNumber)
            .Sample(date =>
            {
                var week = CalendarWeek.Of(date);

                Assert.Equal(week.End.AddDays(1), week.Next().Start);
                Assert.Equal(week.Start.AddDays(-1), week.Previous().End);
                Assert.Equal(week, week.Next().Previous());
                Assert.Equal(week, week.Previous().Next());
            });
    }

    [Fact]
    public void Of_FirstRepresentableDate_StartsTheWeekOnThatMonday()
    {
        var week = CalendarWeek.Of(DateOnly.MinValue);

        Assert.Equal(DayOfWeek.Monday, DateOnly.MinValue.DayOfWeek);
        Assert.Equal(DateOnly.MinValue, week.Start);
        Assert.Equal(default(CalendarWeek), week);
        Assert.Throws<ArgumentOutOfRangeException>(() => week.Previous());
    }

    [Fact]
    public void Of_DateInTheLastIncompleteWeek_Throws()
    {
        var lastFullWeek = CalendarWeek.Of(DateOnly.MaxValue.AddDays(-CalendarWeek.DaysInWeek));

        Assert.Throws<ArgumentOutOfRangeException>(() => CalendarWeek.Of(DateOnly.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => lastFullWeek.Next());
    }

    [Fact]
    public void ToString_ReturnsTheIsoDatesOfStartAndEnd()
    {
        var week = CalendarWeek.Of(new DateOnly(2026, 10, 18));

        Assert.Equal("2026-10-12/2026-10-18", week.ToString());
    }
}
