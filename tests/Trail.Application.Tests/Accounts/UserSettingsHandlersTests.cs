using Trail.Application.Accounts;
using Trail.Domain.Accounts;
using Trail.Domain.Common;

namespace Trail.Application.Tests.Accounts;

public sealed class UserSettingsHandlersTests
{
    private readonly TestCurrentUser _currentUser = new();
    private readonly FakeUserSettingsStore _userSettingsStore = new();

    [Fact]
    public async Task GetUserSettings_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new GetUserSettingsHandler(_currentUser, _userSettingsStore);

        var result = await handler.HandleAsync(new GetUserSettingsQuery(), TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.auth.unauthenticated", message.Code);
    }

    [Fact]
    public async Task GetUserSettings_ExistingSettings_ReturnsDto()
    {
        var existing = UserSettings.Create(_currentUser.Id, "UTC", 50, 10).Data;
        _userSettingsStore.Settings = existing;
        var handler = new GetUserSettingsHandler(_currentUser, _userSettingsStore);

        var result = await handler.HandleAsync(new GetUserSettingsQuery(), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Equal("UTC", result.Data.TimeZoneId);
        Assert.Equal(50, result.Data.PomodoroFocusMinutes);
        Assert.Equal(10, result.Data.PomodoroBreakMinutes);
    }

    [Fact]
    public async Task GetUserSettings_NoSettings_CreatesDefaultAndReturnsDto()
    {
        var handler = new GetUserSettingsHandler(_currentUser, _userSettingsStore);

        var result = await handler.HandleAsync(new GetUserSettingsQuery(), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Equal(UserSettings.DefaultTimeZoneId, result.Data.TimeZoneId);
        Assert.Equal(UserSettings.DefaultPomodoroFocusMinutes, result.Data.PomodoroFocusMinutes);
        Assert.Equal(UserSettings.DefaultPomodoroBreakMinutes, result.Data.PomodoroBreakMinutes);

        Assert.NotNull(_userSettingsStore.Settings);
        Assert.True(_userSettingsStore.Saved);
    }

    [Fact]
    public async Task UpdateUserSettings_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new UpdateUserSettingsHandler(_currentUser, _userSettingsStore);
        var command = new UpdateUserSettingsCommand("UTC", 30, 10);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.auth.unauthenticated", message.Code);
    }

    [Fact]
    public async Task UpdateUserSettings_ValidUpdate_UpdatesSettingsAndSaves()
    {
        _userSettingsStore.Settings = UserSettings.CreateDefault(_currentUser.Id);
        var handler = new UpdateUserSettingsHandler(_currentUser, _userSettingsStore);
        var command = new UpdateUserSettingsCommand("UTC", 45, 15);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Equal("UTC", result.Data.TimeZoneId);
        Assert.Equal(45, result.Data.PomodoroFocusMinutes);
        Assert.Equal(15, result.Data.PomodoroBreakMinutes);

        Assert.True(_userSettingsStore.Saved);
        Assert.Equal("UTC", _userSettingsStore.Settings.TimeZoneId);
    }

    [Fact]
    public async Task UpdateUserSettings_NoExistingSettings_InitializesDefaultAndUpdates()
    {
        var handler = new UpdateUserSettingsHandler(_currentUser, _userSettingsStore);
        var command = new UpdateUserSettingsCommand("UTC", 45, 15);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Equal("UTC", result.Data.TimeZoneId);
        Assert.NotNull(_userSettingsStore.Settings);
        Assert.True(_userSettingsStore.Saved);
    }

    [Fact]
    public async Task UpdateUserSettings_InvalidUpdate_ReturnsInvalidAndDoesNotSave()
    {
        _userSettingsStore.Settings = UserSettings.CreateDefault(_currentUser.Id);
        var handler = new UpdateUserSettingsHandler(_currentUser, _userSettingsStore);
        var command = new UpdateUserSettingsCommand("Invalid/Zone", 0, 100);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.False(_userSettingsStore.Saved);
    }
}
