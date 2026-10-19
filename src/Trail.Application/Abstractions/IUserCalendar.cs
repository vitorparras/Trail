namespace Trail.Application.Abstractions;

public interface IUserCalendar
{
    Task<UserCalendarSnapshot> GetAsync(CancellationToken cancellationToken = default);
}
