using Microsoft.EntityFrameworkCore;
using Trail.Application.Abstractions;
using Trail.Infrastructure.Persistence;

namespace Trail.Infrastructure.Identity;

public sealed class UserCalendar : IUserCalendar
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public UserCalendar(AppDbContext dbContext, ICurrentUser currentUser, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<UserCalendarSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var timeZoneId = _currentUser.IsAuthenticated
            ? await _dbContext.UserSettings
                .AsNoTracking()
                .Where(settings => settings.Id == _currentUser.Id)
                .Select(settings => settings.TimeZoneId)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return UserCalendarSnapshot.FromTimeZoneId(timeZoneId, _timeProvider.GetUtcNow());
    }
}
