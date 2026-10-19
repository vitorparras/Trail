using System.Globalization;

namespace Trail.Domain.Common;

public readonly record struct CalendarWeek
{
    public const int DaysInWeek = 7;

    private CalendarWeek(DateOnly start) => Start = start;

    public DateOnly Start { get; }

    public DateOnly End => Start.AddDays(DaysInWeek - 1);

    public IReadOnlyList<DateOnly> Days => [.. Enumerable.Range(0, DaysInWeek).Select(Start.AddDays)];

    public static CalendarWeek Of(DateOnly date)
    {
        var start = date.AddDays(-DaysSinceMonday(date.DayOfWeek));
        if (start.DayNumber > DateOnly.MaxValue.DayNumber - (DaysInWeek - 1))
        {
            throw new ArgumentOutOfRangeException(nameof(date), date, "The week of this date ends after the last representable date.");
        }

        return new CalendarWeek(start);
    }

    public bool Contains(DateOnly date) => date >= Start && date <= End;

    public CalendarWeek Previous() => new(Start.AddDays(-DaysInWeek));

    public CalendarWeek Next() => Of(Start.AddDays(DaysInWeek));

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Start:yyyy-MM-dd}/{End:yyyy-MM-dd}");

    private static int DaysSinceMonday(DayOfWeek day) => ((int)day + DaysInWeek - 1) % DaysInWeek;
}
