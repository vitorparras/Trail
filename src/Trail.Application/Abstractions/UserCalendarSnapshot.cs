using Trail.Domain.Accounts;

namespace Trail.Application.Abstractions;

public sealed record UserCalendarSnapshot
{
    private UserCalendarSnapshot(TimeZoneInfo timeZone, DateOnly today, DateTime utcNow)
    {
        TimeZone = timeZone;
        Today = today;
        UtcNow = utcNow;
    }

    public TimeZoneInfo TimeZone { get; }

    public DateOnly Today { get; }

    public DateTime UtcNow { get; }

    public static TimeZoneInfo DefaultTimeZone => TimeZoneInfo.FindSystemTimeZoneById(UserSettings.DefaultTimeZoneId);

    public static UserCalendarSnapshot Create(TimeZoneInfo timeZone, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        var utcNow = now.UtcDateTime;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone));

        return new UserCalendarSnapshot(timeZone, today, utcNow);
    }

    public static UserCalendarSnapshot FromTimeZoneId(string? timeZoneId, DateTimeOffset now) =>
        Create(
            timeZoneId is not null && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var timeZone) ? timeZone : DefaultTimeZone,
            now);
}
