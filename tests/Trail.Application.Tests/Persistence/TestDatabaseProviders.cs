using Trail.Infrastructure.Persistence;

namespace Trail.Application.Tests.Persistence;

public static class TestDatabaseProviders
{
    public static TheoryData<DatabaseProvider> EnabledProviders
    {
        get
        {
            var data = new TheoryData<DatabaseProvider>();
            var env = Environment.GetEnvironmentVariable("TRAIL_TEST_DATABASE_PROVIDER")?.Trim();

            if (string.Equals(env, "sqlite", StringComparison.OrdinalIgnoreCase))
            {
                data.Add(DatabaseProvider.Sqlite);
            }
            else if (string.Equals(env, "postgresql", StringComparison.OrdinalIgnoreCase))
            {
                data.Add(DatabaseProvider.PostgreSql);
            }
            else
            {
                data.Add(DatabaseProvider.Sqlite);
                data.Add(DatabaseProvider.PostgreSql);
            }

            return data;
        }
    }
}
