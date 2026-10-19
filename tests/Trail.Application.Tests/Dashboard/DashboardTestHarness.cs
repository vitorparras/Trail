using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Trail.Application.Abstractions;
using Trail.Application.Dashboard;
using Trail.Application.Planning;
using Trail.Application.Tests.Persistence;
using Trail.Domain.Common;
using Trail.Domain.Planning;
using Trail.Infrastructure.Persistence;
using Trail.Infrastructure.Persistence.Interceptors;

namespace Trail.Application.Tests.Dashboard;

internal sealed class DashboardTestHarness : IAsyncDisposable
{
    private readonly ServiceProvider _rootProvider;
    private readonly DbConnection? _sqliteConnection;
    private readonly string? _postgresSchema;
    private readonly string? _postgresConnectionString;

    private DashboardTestHarness(
        ServiceProvider rootProvider,
        SqlCommandRecorder sqlCommands,
        DbConnection? sqliteConnection,
        string? postgresSchema,
        string? postgresConnectionString)
    {
        _rootProvider = rootProvider;
        SqlCommands = sqlCommands;
        _sqliteConnection = sqliteConnection;
        _postgresSchema = postgresSchema;
        _postgresConnectionString = postgresConnectionString;
    }

    public SqlCommandRecorder SqlCommands { get; }

    public static async Task<DashboardTestHarness> CreateAsync(
        DatabaseProvider provider,
        IReadOnlyList<IPlanActivityContributor> activityContributors,
        CancellationToken cancellationToken)
    {
        DbConnection? sqliteConnection = null;
        string? postgresSchema = null;
        string? postgresConnectionString = null;
        var sqlCommands = new SqlCommandRecorder();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<DomainEventDispatchInterceptor>();
        services.AddScoped<TestCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());

        if (provider == DatabaseProvider.Sqlite)
        {
            sqliteConnection = new SqliteConnection("Data Source=:memory:");
            await sqliteConnection.OpenAsync(cancellationToken);
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseSqlite(sqliteConnection).AddInterceptors(sp.GetRequiredService<DomainEventDispatchInterceptor>(), sqlCommands));
        }
        else
        {
            postgresConnectionString = await PostgreSqlTestDatabase.GetConnectionStringAsync(cancellationToken);
            postgresSchema = $"test_dashboard_{Guid.NewGuid():N}";
            await ExecuteAsync(postgresConnectionString, $"CREATE SCHEMA \"{postgresSchema}\";", cancellationToken);

            var connectionString = new NpgsqlConnectionStringBuilder(postgresConnectionString) { SearchPath = postgresSchema }.ConnectionString;
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseNpgsql(connectionString).AddInterceptors(sp.GetRequiredService<DomainEventDispatchInterceptor>(), sqlCommands));
        }

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddApplication();
        services.Replace(ServiceDescriptor.Scoped<IPlanningStore, EfPlanningStore>());
        foreach (var contributor in activityContributors)
        {
            services.AddSingleton(contributor);
        }

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

        return new DashboardTestHarness(rootProvider, sqlCommands, sqliteConnection, postgresSchema, postgresConnectionString);
    }

    public async Task<StudyPlanId> CreatePlanAsync(UserId owner, string name, CancellationToken cancellationToken)
    {
        using var scope = CreateScope(owner);
        var handler = scope.ServiceProvider.GetRequiredService<CreateStudyPlanHandler>();

        var result = await handler.HandleAsync(new CreateStudyPlanCommand(name), cancellationToken);

        Assert.True(result.IsOk);
        return result.Data;
    }

    public async Task<Result<DashboardContext>> HandleAsync(
        UserId user,
        GetDashboardContextQuery query,
        CancellationToken cancellationToken)
    {
        using var scope = CreateScope(user);
        var handler = scope.ServiceProvider.GetRequiredService<GetDashboardContextHandler>();

        return await handler.HandleAsync(query, cancellationToken);
    }

    public async Task<Result<DashboardContext>> HandleAnonymouslyAsync(
        GetDashboardContextQuery query,
        CancellationToken cancellationToken)
    {
        using var scope = _rootProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetDashboardContextHandler>();

        return await handler.HandleAsync(query, cancellationToken);
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
