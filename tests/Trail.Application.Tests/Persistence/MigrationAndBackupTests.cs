using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trail.Infrastructure;
using Trail.Infrastructure.Persistence;

namespace Trail.Application.Tests.Persistence;

public sealed class MigrationAndBackupTests
{
    [Fact]
    public async Task ApplyMigrations_Sqlite_FromScratch_AppliesMigrationsAndEnablesWalMode()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "TrailTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var databasePath = Path.Combine(tempDirectory, "test.db");

        try
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "Sqlite",
                    ["ConnectionStrings:Default"] = $"Data Source={databasePath}",
                })
                .Build();

            services.AddLogging();
            services.AddInfrastructure(configuration);

            using var serviceProvider = services.BuildServiceProvider();
            var initializer = serviceProvider.GetRequiredService<IDatabaseInitializer>();

            await initializer.InitializeAsync(TestContext.Current.CancellationToken);

            using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync(TestContext.Current.CancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode;";
            var journalMode = (string?)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

            Assert.Equal("wal", journalMode, ignoreCase: true);

            var dbContext = serviceProvider.GetRequiredService<AppDbContext>();
            var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);
            Assert.Contains(appliedMigrations, m => m.Contains("InitialCreate", StringComparison.Ordinal));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task MigrateExistingDatabase_Sqlite_WithPendingMigrations_CreatesBackupCopy()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "TrailTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var databasePath = Path.Combine(tempDirectory, "test.db");
        var backupDirectory = Path.Combine(tempDirectory, "backups");

        try
        {
            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE Dummy (Id INTEGER PRIMARY KEY); INSERT INTO Dummy VALUES (1);";
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            Assert.True(File.Exists(databasePath));
            Assert.True(new FileInfo(databasePath).Length > 0);

            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "Sqlite",
                    ["ConnectionStrings:Default"] = $"Data Source={databasePath}",
                })
                .Build();

            services.AddLogging();
            services.AddInfrastructure(configuration);

            using var serviceProvider = services.BuildServiceProvider();
            var initializer = serviceProvider.GetRequiredService<IDatabaseInitializer>();

            await initializer.InitializeAsync(TestContext.Current.CancellationToken);

            Assert.True(Directory.Exists(backupDirectory));
            var backupFiles = Directory.GetFiles(backupDirectory, "test-*.db");
            Assert.Single(backupFiles);
            Assert.True(new FileInfo(backupFiles[0]).Length > 0);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task ApplyMigrations_AppliesSuccessfully(DatabaseProvider provider)
    {
        if (provider == DatabaseProvider.Sqlite)
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "TrailTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            var databasePath = Path.Combine(tempDirectory, "test.db");

            try
            {
                var services = new ServiceCollection();
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Database:Provider"] = "Sqlite",
                        ["ConnectionStrings:Default"] = $"Data Source={databasePath}",
                    })
                    .Build();

                services.AddLogging();
                services.AddInfrastructure(configuration);

                using var serviceProvider = services.BuildServiceProvider();
                var initializer = serviceProvider.GetRequiredService<IDatabaseInitializer>();

                await initializer.InitializeAsync(TestContext.Current.CancellationToken);

                var dbContext = serviceProvider.GetRequiredService<AppDbContext>();
                var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);
                Assert.Contains(appliedMigrations, m => m.Contains("InitialCreate", StringComparison.Ordinal));
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
        }
        else
        {
            var connectionString = await PostgreSqlTestDatabase.GetConnectionStringAsync(TestContext.Current.CancellationToken);
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "PostgreSql",
                    ["ConnectionStrings:Default"] = connectionString,
                })
                .Build();

            services.AddLogging();
            services.AddInfrastructure(configuration);

            using var serviceProvider = services.BuildServiceProvider();
            var initializer = serviceProvider.GetRequiredService<IDatabaseInitializer>();

            await initializer.InitializeAsync(TestContext.Current.CancellationToken);

            var dbContext = serviceProvider.GetRequiredService<AppDbContext>();
            var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);
            Assert.Contains(appliedMigrations, m => m.Contains("InitialCreate", StringComparison.Ordinal));
        }
    }
}
