using Trail.Domain.Common;

namespace Trail.Domain.Accounts;

public sealed class UserSettings : AggregateRoot<UserId>
{
    public const string DefaultTimeZoneId = "America/Sao_Paulo";
    public const int DefaultPomodoroFocusMinutes = 25;
    public const int DefaultPomodoroBreakMinutes = 5;
    public const int MinPomodoroFocusMinutes = 1;
    public const int MaxPomodoroFocusMinutes = 180;
    public const int MinPomodoroBreakMinutes = 1;
    public const int MaxPomodoroBreakMinutes = 60;

    private UserSettings()
    {
        TimeZoneId = DefaultTimeZoneId;
    }

    private UserSettings(
        UserId userId,
        string timeZoneId,
        int pomodoroFocusMinutes,
        int pomodoroBreakMinutes)
        : base(userId, userId)
    {
        TimeZoneId = timeZoneId;
        PomodoroFocusMinutes = pomodoroFocusMinutes;
        PomodoroBreakMinutes = pomodoroBreakMinutes;
    }

    public string TimeZoneId { get; private set; }

    public int PomodoroFocusMinutes { get; private set; }

    public int PomodoroBreakMinutes { get; private set; }

    public static UserSettings CreateDefault(UserId userId) =>
        new(userId, DefaultTimeZoneId, DefaultPomodoroFocusMinutes, DefaultPomodoroBreakMinutes);

    public static Result<UserSettings> Create(
        UserId userId,
        string? timeZoneId,
        int pomodoroFocusMinutes,
        int pomodoroBreakMinutes)
    {
        var notification = Validate(timeZoneId, pomodoroFocusMinutes, pomodoroBreakMinutes);

        return notification.ToResult(() => new UserSettings(
            userId,
            timeZoneId!,
            pomodoroFocusMinutes,
            pomodoroBreakMinutes));
    }

    public Result Update(string? timeZoneId, int pomodoroFocusMinutes, int pomodoroBreakMinutes)
    {
        var notification = Validate(timeZoneId, pomodoroFocusMinutes, pomodoroBreakMinutes);

        if (notification.HasErrors)
        {
            return notification.ToResult();
        }

        TimeZoneId = timeZoneId!;
        PomodoroFocusMinutes = pomodoroFocusMinutes;
        PomodoroBreakMinutes = pomodoroBreakMinutes;

        return Result.Ok();
    }

    private static Notification Validate(string? timeZoneId, int pomodoroFocusMinutes, int pomodoroBreakMinutes)
    {
        var notification = new Notification();

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            notification.Add(Message.Error("accounts.settings.timezone_required", nameof(TimeZoneId)));
        }
        else if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _))
        {
            notification.Add(Message.Error("accounts.settings.timezone_invalid", nameof(TimeZoneId)));
        }

        notification
            .AddErrorIf(
                pomodoroFocusMinutes < MinPomodoroFocusMinutes || pomodoroFocusMinutes > MaxPomodoroFocusMinutes,
                "accounts.settings.focus_minutes_out_of_range",
                nameof(PomodoroFocusMinutes))
            .AddErrorIf(
                pomodoroBreakMinutes < MinPomodoroBreakMinutes || pomodoroBreakMinutes > MaxPomodoroBreakMinutes,
                "accounts.settings.break_minutes_out_of_range",
                nameof(PomodoroBreakMinutes));

        return notification;
    }
}
