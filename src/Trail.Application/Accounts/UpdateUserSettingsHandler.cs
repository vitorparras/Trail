using Trail.Application.Abstractions;
using Trail.Domain.Accounts;
using Trail.Domain.Common;

namespace Trail.Application.Accounts;

public sealed record UpdateUserSettingsCommand(
    string? TimeZoneId,
    int PomodoroFocusMinutes,
    int PomodoroBreakMinutes);

public sealed class UpdateUserSettingsHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserSettingsStore _userSettingsStore;

    public UpdateUserSettingsHandler(
        ICurrentUser currentUser,
        IUserSettingsStore userSettingsStore)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(userSettingsStore);

        _currentUser = currentUser;
        _userSettingsStore = userSettingsStore;
    }

    public async Task<Result<UserSettingsDto>> HandleAsync(
        UpdateUserSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_currentUser.IsAuthenticated)
        {
            return Result.Forbidden(Message.Error("accounts.auth.unauthenticated"));
        }

        var settings = await _userSettingsStore.GetForCurrentUserAsync(cancellationToken);
        if (settings is null)
        {
            settings = UserSettings.CreateDefault(_currentUser.Id);
            await _userSettingsStore.AddAsync(settings, cancellationToken);
        }

        var updateResult = settings.Update(
            command.TimeZoneId,
            command.PomodoroFocusMinutes,
            command.PomodoroBreakMinutes);

        if (!updateResult.IsOk)
        {
            return updateResult.Failure;
        }

        await _userSettingsStore.SaveChangesAsync(cancellationToken);

        return Result.Ok(new UserSettingsDto(
            settings.TimeZoneId,
            settings.PomodoroFocusMinutes,
            settings.PomodoroBreakMinutes));
    }
}
