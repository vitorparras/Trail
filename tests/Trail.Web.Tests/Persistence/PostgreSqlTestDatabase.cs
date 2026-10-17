using Npgsql;
using Testcontainers.PostgreSql;

namespace Trail.Web.Tests.Persistence;

public static class PostgreSqlTestDatabase
{
    private static readonly SemaphoreSlim Lock = new(1, 1);
    private static PostgreSqlContainer? _container;

    public static async Task<string> GetConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        if (_container is not null)
        {
            return _container.GetConnectionString();
        }

        await Lock.WaitAsync(cancellationToken);
        try
        {
            if (_container is null)
            {
                var container = new PostgreSqlBuilder("postgres:17.5-alpine")
                    .WithDatabase("trail_test")
                    .WithUsername("postgres")
                    .WithPassword("postgres")
                    .Build();

                await container.StartAsync(cancellationToken);
                _container = container;
            }

            return _container.GetConnectionString();
        }
        finally
        {
            Lock.Release();
        }
    }

    public static async Task<(string ConnectionString, string SchemaName)> CreateIsolatedDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var baseConnectionString = await GetConnectionStringAsync(cancellationToken);
        var schemaName = $"test_{Guid.NewGuid():N}";

        await using (var connection = new NpgsqlConnection(baseConnectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE SCHEMA \"{schemaName}\";";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            SearchPath = schemaName
        };

        return (builder.ConnectionString, schemaName);
    }

    public static async Task DropSchemaAsync(string schemaName, CancellationToken cancellationToken = default)
    {
        var baseConnectionString = await GetConnectionStringAsync(cancellationToken);
        await using var connection = new NpgsqlConnection(baseConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
