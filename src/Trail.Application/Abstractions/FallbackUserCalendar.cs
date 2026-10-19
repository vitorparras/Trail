namespace Trail.Application.Abstractions;

internal sealed class FallbackUserCalendar(TimeProvider timeProvider) : IUserCalendar
{
    public Task<UserCalendarSnapshot> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(UserCalendarSnapshot.Create(UserCalendarSnapshot.DefaultTimeZone, timeProvider.GetUtcNow()));
}
