using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Time.Testing;
using Trail.Application.Abstractions;
using Trail.Domain.Accounts;
using Trail.Infrastructure.Identity;

namespace Trail.Web.Tests.Calendar;

public sealed class UserCalendarCompositionTests(TrailWebApplicationFactory baseFactory) : IClassFixture<TrailWebApplicationFactory>
{
    [Fact]
    public async Task Composition_ResolvesTheInfrastructureCalendarWithTheRegisteredClock()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 19, 2, 30, 0, TimeSpan.Zero));
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        await using var scope = factory.Services.CreateAsyncScope();

        var calendar = scope.ServiceProvider.GetRequiredService<IUserCalendar>();
        var snapshot = await calendar.GetAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromHours(1));
        var later = await calendar.GetAsync(TestContext.Current.CancellationToken);

        Assert.IsType<UserCalendar>(calendar);
        Assert.True(snapshot.TimeZone.HasSameRules(TimeZoneInfo.FindSystemTimeZoneById(UserSettings.DefaultTimeZoneId)));
        Assert.Equal(new DateOnly(2026, 10, 18), snapshot.Today);
        Assert.Equal(new DateTime(2026, 10, 19, 2, 30, 0, DateTimeKind.Utc), snapshot.UtcNow);
        Assert.Equal(new DateOnly(2026, 10, 19), later.Today);
    }
}
