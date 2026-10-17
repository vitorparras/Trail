namespace Trail.Application.Accounts;

public sealed record UserSettingsDto(
    string TimeZoneId,
    int PomodoroFocusMinutes,
    int PomodoroBreakMinutes);
