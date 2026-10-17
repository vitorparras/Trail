using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trail.Infrastructure.Persistence;
using Trail.Infrastructure.Persistence.Interceptors;

namespace Trail.Application.Tests.Persistence;

public sealed class TestDatabaseHarness : IAsyncDisposable
{
    private readonly DbConnection? _sqliteConnection;
    private readonly DbContextOptions<TestDbContext> _options;
    private readonly DomainEventDispatchInterceptor? _interceptor;
    private readonly string? _postgresSchema;
    private readonly string? _postgresConnectionString;

    public TestDatabaseHarness(
        DbConnection? sqliteConnection,
        DbContextOptions<TestDbContext> options,
        DomainEventDispatchInterceptor? interceptor,
        string? postgresSchema = null,
        string? postgresConnectionString = null)
    {
        _sqliteConnection = sqliteConnection;
        _options = options;
        _interceptor = interceptor;
        _postgresSchema = postgresSchema;
        _postgresConnectionString = postgresConnectionString;
    }

    public TestDbContext CreateContext(TestCurrentUser currentUser)
    {
        return new TestDbContext(_options, currentUser);
    }

    public async ValueTask DisposeAsync()
    {
        if (_sqliteConnection is not null)
        {
            await _sqliteConnection.DisposeAsync();
        }

        if (_postgresSchema is not null && _postgresConnectionString is not null)
        {
            await using var connection = new NpgsqlConnection(_postgresConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP SCHEMA IF EXISTS \"{_postgresSchema}\" CASCADE;";
            await command.ExecuteNonQueryAsync();
        }
    }
}

public static class TestDbContextFixture
{
    public static async Task<TestDatabaseHarness> CreateAsync(
        DatabaseProvider provider,
        DomainEventDispatchInterceptor? interceptor = null,
        CancellationToken cancellationToken = default)
    {
        if (provider == DatabaseProvider.Sqlite)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(cancellationToken);

            var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite(connection);

            if (interceptor is not null)
            {
                optionsBuilder.AddInterceptors(interceptor);
            }

            var options = optionsBuilder.Options;

            using (var initContext = new TestDbContext(options, new TestCurrentUser { IsAuthenticated = false }))
            {
                await initContext.Database.EnsureCreatedAsync(cancellationToken);
            }

            return new TestDatabaseHarness(connection, options, interceptor);
        }
        else
        {
            var baseConnectionString = await PostgreSqlTestDatabase.GetConnectionStringAsync(cancellationToken);
            var schemaName = $"test_{Guid.NewGuid():N}";

            await using (var connection = new NpgsqlConnection(baseConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE SCHEMA \"{schemaName}\";";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString)
            {
                SearchPath = schemaName,
            };

            var schemaConnectionString = connectionStringBuilder.ConnectionString;

            var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql(schemaConnectionString);

            if (interceptor is not null)
            {
                optionsBuilder.AddInterceptors(interceptor);
            }

            var options = optionsBuilder.Options;

            using (var initContext = new TestDbContext(options, new TestCurrentUser { IsAuthenticated = false }))
            {
                var script = initContext.Database.GenerateCreateScript();
                await initContext.Database.ExecuteSqlRawAsync(script, cancellationToken);
            }

            return new TestDatabaseHarness(null, options, interceptor, schemaName, baseConnectionString);
        }
    }
}
