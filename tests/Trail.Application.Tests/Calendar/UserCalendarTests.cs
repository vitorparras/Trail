using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Trail.Application.Abstractions;
using Trail.Application.Tests.Persistence;
using Trail.Domain.Accounts;
using Trail.Domain.Common;
using Trail.Infrastructure.Identity;
using Trail.Infrastructure.Persistence;
using Trail.Infrastructure.Persistence.Interceptors;

namespace Trail.Application.Tests.Calendar;

public sealed class UserCalendarTests
{
    private static readonly DateTimeOffset HalfPastElevenInSaoPaulo = new(2026, 10, 19, 2, 30, 0, TimeSpan.Zero);

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task GetAsync_At2330InSaoPaulo_ReturnsTheLocalDateAndTheUtcInstant(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await CalendarHarness.CreateAsync(provider, ct);
        var user = UserId.New();
        await harness.SaveSettingsAsync(user, "America/Sao_Paulo", ct);

        var snapshot = await harness.GetAsync(user, ct);

        Assert.IsType<UserCalendar>(harness.Resolve(user));
        Assert.Equal(new DateOnly(2026, 10, 18), snapshot.Today);
        Assert.Equal(new DateTime(2026, 10, 19, 2, 30, 0, DateTimeKind.Utc), snapshot.UtcNow);
        Assert.Equal(DateTimeKind.Utc, snapshot.UtcNow.Kind);
        Assert.Equal(TimeSpan.FromHours(-3), snapshot.TimeZone.GetUtcOffset(snapshot.UtcNow));
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task GetAsync_WhenTheClockCrossesLocalMidnight_ReturnsTheNextLocalDate(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await CalendarHarness.CreateAsync(provider, ct);
        var user = UserId.New();
        await harness.SaveSettingsAsync(user, "America/Sao_Paulo", ct);

        var before = await harness.GetAsync(user, ct);
        harness.Clock.Advance(TimeSpan.FromMinutes(30));
        var after = await harness.GetAsync(user, ct);

        Assert.Equal(new DateOnly(2026, 10, 18), before.Today);
        Assert.Equal(new DateOnly(2026, 10, 19), after.Today);
        Assert.Equal(new DateTime(2026, 10, 19, 3, 0, 0, DateTimeKind.Utc), after.UtcNow);
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task GetAsync_WithoutSettings_UsesTheDefaultTimeZone(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await CalendarHarness.CreateAsync(provider, ct);

        var snapshot = await harness.GetAsync(UserId.New(), ct);
        var anonymous = await harness.GetAnonymouslyAsync(ct);

        var defaultZone = TimeZoneInfo.FindSystemTimeZoneById(UserSettings.DefaultTimeZoneId);
        Assert.Equal(new DateOnly(2026, 10, 18), snapshot.Today);
        Assert.True(snapshot.TimeZone.HasSameRules(defaultZone));
        Assert.Equal(new DateOnly(2026, 10, 18), anonymous.Today);
        Assert.True(anonymous.TimeZone.HasSameRules(defaultZone));
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task GetAsync_WithAnotherUsersSettings_NeverUsesThem(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await CalendarHarness.CreateAsync(provider, ct);
        var tokyoUser = UserId.New();
        var saoPauloUser = UserId.New();
        var userWithoutSettings = UserId.New();
        await harness.SaveSettingsAsync(tokyoUser, "Asia/Tokyo", ct);
        await harness.SaveSettingsAsync(saoPauloUser, "America/Sao_Paulo", ct);

        var tokyo = await harness.GetAsync(tokyoUser, ct);
        var saoPaulo = await harness.GetAsync(saoPauloUser, ct);
        var withoutSettings = await harness.GetAsync(userWithoutSettings, ct);
        var anonymous = await harness.GetAnonymouslyAsync(ct);

        Assert.Equal(new DateOnly(2026, 10, 19), tokyo.Today);
        Assert.Equal(TimeSpan.FromHours(9), tokyo.TimeZone.GetUtcOffset(tokyo.UtcNow));
        Assert.Equal(new DateOnly(2026, 10, 18), saoPaulo.Today);
        Assert.Equal(new DateOnly(2026, 10, 18), withoutSettings.Today);
        Assert.Equal(TimeSpan.FromHours(-3), withoutSettings.TimeZone.GetUtcOffset(withoutSettings.UtcNow));
        Assert.Equal(new DateOnly(2026, 10, 18), anonymous.Today);
    }

    [Fact]
    public async Task FallbackUserCalendar_WithoutInfrastructure_UsesTheDefaultTimeZoneAndTheRegisteredClock()
    {
        var clock = new FakeTimeProvider(HalfPastElevenInSaoPaulo);
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(clock);
        services.AddApplication();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var calendar = scope.ServiceProvider.GetRequiredService<IUserCalendar>();
        var snapshot = await calendar.GetAsync(TestContext.Current.CancellationToken);

        Assert.IsType<FallbackUserCalendar>(calendar);
        Assert.Equal(new DateOnly(2026, 10, 18), snapshot.Today);
        Assert.Equal(HalfPastElevenInSaoPaulo.UtcDateTime, snapshot.UtcNow);
        Assert.True(snapshot.TimeZone.HasSameRules(TimeZoneInfo.FindSystemTimeZoneById(UserSettings.DefaultTimeZoneId)));
    }

    private sealed class CalendarHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _rootProvider;
        private readonly DbConnection? _sqliteConnection;
        private readonly string? _postgresSchema;
        private readonly string? _postgresConnectionString;

        private CalendarHarness(
            ServiceProvider rootProvider,
            FakeTimeProvider clock,
            DbConnection? sqliteConnection,
            string? postgresSchema,
            string? postgresConnectionString)
        {
            _rootProvider = rootProvider;
            Clock = clock;
            _sqliteConnection = sqliteConnection;
            _postgresSchema = postgresSchema;
            _postgresConnectionString = postgresConnectionString;
        }

        public FakeTimeProvider Clock { get; }

        public static async Task<CalendarHarness> CreateAsync(DatabaseProvider provider, CancellationToken cancellationToken)
        {
            DbConnection? sqliteConnection = null;
            string? postgresSchema = null;
            string? postgresConnectionString = null;
            var clock = new FakeTimeProvider(HalfPastElevenInSaoPaulo);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMetrics();
            services.AddSingleton<TimeProvider>(clock);
            services.AddScoped<DomainEventDispatchInterceptor>();
            services.AddScoped<TestCurrentUser>();
            services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());

            if (provider == DatabaseProvider.Sqlite)
            {
                sqliteConnection = new SqliteConnection("Data Source=:memory:");
                await sqliteConnection.OpenAsync(cancellationToken);
                services.AddDbContext<AppDbContext>((sp, options) =>
                    options.UseSqlite(sqliteConnection).AddInterceptors(sp.GetRequiredService<DomainEventDispatchInterceptor>()));
            }
            else
            {
                postgresConnectionString = await PostgreSqlTestDatabase.GetConnectionStringAsync(cancellationToken);
                postgresSchema = $"test_calendar_{Guid.NewGuid():N}";
                await ExecuteAsync(postgresConnectionString, $"CREATE SCHEMA \"{postgresSchema}\";", cancellationToken);

                var connectionString = new NpgsqlConnectionStringBuilder(postgresConnectionString) { SearchPath = postgresSchema }.ConnectionString;
                services.AddDbContext<AppDbContext>((sp, options) =>
                    options.UseNpgsql(connectionString).AddInterceptors(sp.GetRequiredService<DomainEventDispatchInterceptor>()));
            }

            services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
            services.AddApplication();
            services.Replace(ServiceDescriptor.Scoped<IUserCalendar, UserCalendar>());

            var rootProvider = services.BuildServiceProvider();

            using (var scope = rootProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (provider == DatabaseProvider.Sqlite)
                {
                    await dbContext.Database.EnsureCreatedAsync(cancellationToken);
                }
                else
                {
                    await dbContext.Database.ExecuteSqlRawAsync(dbContext.Database.GenerateCreateScript(), cancellationToken);
                }
            }

            return new CalendarHarness(rootProvider, clock, sqliteConnection, postgresSchema, postgresConnectionString);
        }

        public async Task SaveSettingsAsync(UserId user, string timeZoneId, CancellationToken cancellationToken)
        {
            using var scope = CreateScope(user);
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.UserSettings.AddAsync(UserSettings.Create(user, timeZoneId, 25, 5).Data, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        public IUserCalendar Resolve(UserId user)
        {
            using var scope = CreateScope(user);
            return scope.ServiceProvider.GetRequiredService<IUserCalendar>();
        }

        public async Task<UserCalendarSnapshot> GetAsync(UserId user, CancellationToken cancellationToken)
        {
            using var scope = CreateScope(user);
            return await scope.ServiceProvider.GetRequiredService<IUserCalendar>().GetAsync(cancellationToken);
        }

        public async Task<UserCalendarSnapshot> GetAnonymouslyAsync(CancellationToken cancellationToken)
        {
            using var scope = _rootProvider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IUserCalendar>().GetAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _rootProvider.DisposeAsync();

            if (_sqliteConnection is not null)
            {
                await _sqliteConnection.DisposeAsync();
            }

            if (_postgresSchema is not null && _postgresConnectionString is not null)
            {
                await ExecuteAsync(_postgresConnectionString, $"DROP SCHEMA IF EXISTS \"{_postgresSchema}\" CASCADE;", CancellationToken.None);
            }
        }

        private IServiceScope CreateScope(UserId user)
        {
            var scope = _rootProvider.CreateScope();
            var currentUser = scope.ServiceProvider.GetRequiredService<TestCurrentUser>();
            currentUser.Id = user;
            currentUser.IsAuthenticated = true;
            return scope;
        }

        private static async Task ExecuteAsync(string connectionString, string sql, CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
