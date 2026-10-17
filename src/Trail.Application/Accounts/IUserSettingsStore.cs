using Trail.Domain.Accounts;

namespace Trail.Application.Accounts;

public interface IUserSettingsStore
{
    Task<UserSettings?> GetForCurrentUserAsync(CancellationToken cancellationToken = default);

    Task AddAsync(UserSettings settings, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
