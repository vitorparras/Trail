using System.Reflection;
using Trail.Domain.Accounts;
using Trail.Domain.Common;

namespace Trail.Domain.Tests.Accounts;

public sealed class UserSettingsTests
{
    private readonly UserId _userId = UserId.New();

    [Fact]
    public void CreateDefault_ReturnsSettingsWithDefaultValues()
    {
        var settings = UserSettings.CreateDefault(_userId);

        Assert.Equal(_userId, settings.Id);
        Assert.Equal(_userId, settings.OwnerId);
        Assert.Equal(UserSettings.DefaultTimeZoneId, settings.TimeZoneId);
        Assert.Equal(UserSettings.DefaultPomodoroFocusMinutes, settings.PomodoroFocusMinutes);
        Assert.Equal(UserSettings.DefaultPomodoroBreakMinutes, settings.PomodoroBreakMinutes);
    }

    [Fact]
    public void Create_WithValidParameters_ReturnsOk()
    {
        var result = UserSettings.Create(_userId, "UTC", 50, 10);

        Assert.True(result.IsOk);
        Assert.Equal(_userId, result.Data.Id);
        Assert.Equal(_userId, result.Data.OwnerId);
        Assert.Equal("UTC", result.Data.TimeZoneId);
        Assert.Equal(50, result.Data.PomodoroFocusMinutes);
        Assert.Equal(10, result.Data.PomodoroBreakMinutes);
    }

    [Fact]
    public void Create_WithBoundaryValues_ReturnsOk()
    {
        var minResult = UserSettings.Create(
            _userId,
            "UTC",
            UserSettings.MinPomodoroFocusMinutes,
            UserSettings.MinPomodoroBreakMinutes);

        var maxResult = UserSettings.Create(
            _userId,
            "UTC",
            UserSettings.MaxPomodoroFocusMinutes,
            UserSettings.MaxPomodoroBreakMinutes);

        Assert.True(minResult.IsOk);
        Assert.Equal(UserSettings.MinPomodoroFocusMinutes, minResult.Data.PomodoroFocusMinutes);
        Assert.Equal(UserSettings.MinPomodoroBreakMinutes, minResult.Data.PomodoroBreakMinutes);

        Assert.True(maxResult.IsOk);
        Assert.Equal(UserSettings.MaxPomodoroFocusMinutes, maxResult.Data.PomodoroFocusMinutes);
        Assert.Equal(UserSettings.MaxPomodoroBreakMinutes, maxResult.Data.PomodoroBreakMinutes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingTimeZone_ReturnsTimezoneRequiredError(string? timeZoneId)
    {
        var result = UserSettings.Create(_userId, timeZoneId, 25, 5);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.settings.timezone_required", message.Code);
        Assert.Equal(nameof(UserSettings.TimeZoneId), message.Target);
    }

    [Fact]
    public void Create_WithUnknownTimeZone_ReturnsTimezoneInvalidError()
    {
        var result = UserSettings.Create(_userId, "Invalid/Unknown_Timezone", 25, 5);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.settings.timezone_invalid", message.Code);
        Assert.Equal(nameof(UserSettings.TimeZoneId), message.Target);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(181)]
    [InlineData(300)]
    public void Create_WithFocusMinutesOutOfRange_ReturnsFocusMinutesOutOfRangeError(int focusMinutes)
    {
        var result = UserSettings.Create(_userId, "UTC", focusMinutes, 5);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.settings.focus_minutes_out_of_range", message.Code);
        Assert.Equal(nameof(UserSettings.PomodoroFocusMinutes), message.Target);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(61)]
    [InlineData(120)]
    public void Create_WithBreakMinutesOutOfRange_ReturnsBreakMinutesOutOfRangeError(int breakMinutes)
    {
        var result = UserSettings.Create(_userId, "UTC", 25, breakMinutes);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.settings.break_minutes_out_of_range", message.Code);
        Assert.Equal(nameof(UserSettings.PomodoroBreakMinutes), message.Target);
    }

    [Fact]
    public void Create_WithMultipleViolations_AccumulatesAllErrorsInNotification()
    {
        var result = UserSettings.Create(_userId, "", 0, 100);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal(3, result.Messages.Count);
        Assert.Contains(result.Messages, m => m.Code == "accounts.settings.timezone_required");
        Assert.Contains(result.Messages, m => m.Code == "accounts.settings.focus_minutes_out_of_range");
        Assert.Contains(result.Messages, m => m.Code == "accounts.settings.break_minutes_out_of_range");
    }

    [Fact]
    public void Update_WithValidParameters_UpdatesPropertiesAndReturnsOk()
    {
        var settings = UserSettings.CreateDefault(_userId);

        var result = settings.Update("UTC", 45, 15);

        Assert.True(result.IsOk);
        Assert.Equal("UTC", settings.TimeZoneId);
        Assert.Equal(45, settings.PomodoroFocusMinutes);
        Assert.Equal(15, settings.PomodoroBreakMinutes);
    }

    [Fact]
    public void Update_WithInvalidParameters_ReturnsInvalidAndDoesNotModifyState()
    {
        var settings = UserSettings.CreateDefault(_userId);

        var result = settings.Update("Invalid/Unknown", 0, 200);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal(UserSettings.DefaultTimeZoneId, settings.TimeZoneId);
        Assert.Equal(UserSettings.DefaultPomodoroFocusMinutes, settings.PomodoroFocusMinutes);
        Assert.Equal(UserSettings.DefaultPomodoroBreakMinutes, settings.PomodoroBreakMinutes);
        Assert.Equal(3, result.Messages.Count);
    }

    [Fact]
    public void ParameterlessConstructor_InitializesWithDefaultTimeZone()
    {
        var constructor = typeof(UserSettings).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);

        Assert.NotNull(constructor);
        var settings = (UserSettings)constructor.Invoke(null);
        Assert.Equal(UserSettings.DefaultTimeZoneId, settings.TimeZoneId);
    }

    [Fact]
    public void CreateDefault_WithEmptyUserId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => UserSettings.CreateDefault(new UserId(Guid.Empty)));
    }

    [Fact]
    public void Create_WithEmptyUserIdAndValidFields_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => UserSettings.Create(new UserId(Guid.Empty), "UTC", 25, 5));
    }
}
