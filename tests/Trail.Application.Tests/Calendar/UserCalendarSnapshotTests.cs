using Trail.Application.Abstractions;
using Trail.Domain.Accounts;

namespace Trail.Application.Tests.Calendar;

public sealed class UserCalendarSnapshotTests
{
    [Fact]
    public void Create_WithAnOffsetInstant_StoresTheUtcInstantAndTheLocalDate()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

        var snapshot = UserCalendarSnapshot.Create(zone, new DateTimeOffset(2026, 10, 18, 20, 30, 0, TimeSpan.FromHours(-3)));

        Assert.Same(zone, snapshot.TimeZone);
        Assert.Equal(new DateTime(2026, 10, 18, 23, 30, 0, DateTimeKind.Utc), snapshot.UtcNow);
        Assert.Equal(DateTimeKind.Utc, snapshot.UtcNow.Kind);
        Assert.Equal(new DateOnly(2026, 10, 19), snapshot.Today);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Not/A_Zone")]
    public void FromTimeZoneId_WithoutAKnownZone_UsesTheDefaultTimeZone(string? timeZoneId)
    {
        var snapshot = UserCalendarSnapshot.FromTimeZoneId(timeZoneId, new DateTimeOffset(2026, 10, 19, 2, 30, 0, TimeSpan.Zero));

        Assert.True(snapshot.TimeZone.HasSameRules(TimeZoneInfo.FindSystemTimeZoneById(UserSettings.DefaultTimeZoneId)));
        Assert.Equal(new DateOnly(2026, 10, 18), snapshot.Today);
    }

    [Fact]
    public void FromTimeZoneId_WithAKnownZone_UsesIt()
    {
        var snapshot = UserCalendarSnapshot.FromTimeZoneId("Europe/Lisbon", new DateTimeOffset(2026, 10, 18, 23, 30, 0, TimeSpan.Zero));

        Assert.Equal(TimeSpan.FromHours(1), snapshot.TimeZone.GetUtcOffset(snapshot.UtcNow));
        Assert.Equal(new DateOnly(2026, 10, 19), snapshot.Today);
    }

    [Fact]
    public void Create_WithoutTimeZone_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => UserCalendarSnapshot.Create(null!, DateTimeOffset.UnixEpoch));
    }
}
