using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Trail.Infrastructure.Persistence;
using Trail.Web.Tests.Persistence;

namespace Trail.Web.Tests;

public class TrailWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly DatabaseProvider _provider;
    private readonly string? _databaseDirectory;
    private readonly string? _databasePath;
    private readonly string? _postgresSchema;
    private readonly string? _connectionString;
    private readonly IReadOnlyDictionary<string, string?>? _additionalConfiguration;

    public TrailWebApplicationFactory()
        : this(ResolveDefaultProvider(), null)
    {
    }

    private TrailWebApplicationFactory(DatabaseProvider provider, IReadOnlyDictionary<string, string?>? additionalConfiguration)
    {
        _provider = provider;
        _additionalConfiguration = additionalConfiguration;

        if (_provider == DatabaseProvider.Sqlite)
        {
            _databaseDirectory = Path.Combine(Path.GetTempPath(), "TrailWebTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_databaseDirectory);
            _databasePath = Path.Combine(_databaseDirectory, "trail_test.db");
            _connectionString = $"Data Source={_databasePath}";
        }
        else
        {
            var (connStr, schemaName) = PostgreSqlTestDatabase.CreateIsolatedDatabaseAsync().GetAwaiter().GetResult();
            _connectionString = connStr;
            _postgresSchema = schemaName;
        }
    }

    public static TrailWebApplicationFactory Create(
        DatabaseProvider provider,
        IReadOnlyDictionary<string, string?>? additionalConfiguration = null) =>
        new(provider, additionalConfiguration);

    public static TrailWebApplicationFactory Create(
        IReadOnlyDictionary<string, string?> additionalConfiguration) =>
        new(ResolveDefaultProvider(), additionalConfiguration);

    public DatabaseProvider Provider => _provider;

    public string? DatabasePath => _databasePath;

    public string? ConnectionString => _connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Database:Provider"] = _provider.ToString(),
                ["ConnectionStrings:Default"] = _connectionString,
            };

            if (_additionalConfiguration is not null)
            {
                foreach (var (key, value) in _additionalConfiguration)
                {
                    settings[key] = value;
                }
            }

            config.AddInMemoryCollection(settings);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            if (_databaseDirectory is not null && Directory.Exists(_databaseDirectory))
            {
                try
                {
                    SqliteConnection.ClearAllPools();
                    Directory.Delete(_databaseDirectory, recursive: true);
                }
                catch
                {
                }
            }

            if (!string.IsNullOrEmpty(_postgresSchema))
            {
                try
                {
                    PostgreSqlTestDatabase.DropSchemaAsync(_postgresSchema).GetAwaiter().GetResult();
                }
                catch
                {
                }
            }
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        if (_databaseDirectory is not null && Directory.Exists(_databaseDirectory))
        {
            try
            {
                SqliteConnection.ClearAllPools();
                Directory.Delete(_databaseDirectory, recursive: true);
            }
            catch
            {
            }
        }

        if (!string.IsNullOrEmpty(_postgresSchema))
        {
            try
            {
                await PostgreSqlTestDatabase.DropSchemaAsync(_postgresSchema);
            }
            catch
            {
            }
        }

        GC.SuppressFinalize(this);
    }

    private static DatabaseProvider ResolveDefaultProvider()
    {
        var env = Environment.GetEnvironmentVariable("TRAIL_TEST_DATABASE_PROVIDER")?.Trim();
        return string.Equals(env, "postgresql", StringComparison.OrdinalIgnoreCase)
            ? DatabaseProvider.PostgreSql
            : DatabaseProvider.Sqlite;
    }
}
