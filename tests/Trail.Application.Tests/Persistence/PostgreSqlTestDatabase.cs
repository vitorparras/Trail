using Testcontainers.PostgreSql;

namespace Trail.Application.Tests.Persistence;

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
}
