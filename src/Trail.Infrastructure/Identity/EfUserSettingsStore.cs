using Microsoft.EntityFrameworkCore;
using Trail.Application.Abstractions;
using Trail.Application.Accounts;
using Trail.Domain.Accounts;
using Trail.Infrastructure.Persistence;

namespace Trail.Infrastructure.Identity;

public sealed class EfUserSettingsStore : IUserSettingsStore
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public EfUserSettingsStore(AppDbContext dbContext, ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(currentUser);
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<UserSettings?> GetForCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return null;
        }

        return await _dbContext.UserSettings
            .FirstOrDefaultAsync(s => s.Id == _currentUser.Id, cancellationToken);
    }

    public async Task AddAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await _dbContext.UserSettings.AddAsync(settings, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
