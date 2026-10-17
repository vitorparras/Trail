using Trail.Application.Abstractions;
using Trail.Domain.Accounts;
using Trail.Domain.Common;

namespace Trail.Application.Accounts;

public sealed record GetUserSettingsQuery();

public sealed class GetUserSettingsHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserSettingsStore _userSettingsStore;

    public GetUserSettingsHandler(
        ICurrentUser currentUser,
        IUserSettingsStore userSettingsStore)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(userSettingsStore);

        _currentUser = currentUser;
        _userSettingsStore = userSettingsStore;
    }

    public async Task<Result<UserSettingsDto>> HandleAsync(
        GetUserSettingsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_currentUser.IsAuthenticated)
        {
            return Result.Forbidden(Message.Error("accounts.auth.unauthenticated"));
        }

        var settings = await _userSettingsStore.GetForCurrentUserAsync(cancellationToken);
        if (settings is null)
        {
            settings = UserSettings.CreateDefault(_currentUser.Id);
            await _userSettingsStore.AddAsync(settings, cancellationToken);
            await _userSettingsStore.SaveChangesAsync(cancellationToken);
        }

        return Result.Ok(new UserSettingsDto(
            settings.TimeZoneId,
            settings.PomodoroFocusMinutes,
            settings.PomodoroBreakMinutes));
    }
}
