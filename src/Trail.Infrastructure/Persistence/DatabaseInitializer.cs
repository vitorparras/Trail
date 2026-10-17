using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Trail.Infrastructure.Persistence;

public sealed partial class DatabaseInitializer : IDatabaseInitializer, IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<DatabaseOptions> _databaseOptions;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IServiceProvider serviceProvider,
        IOptions<DatabaseOptions> databaseOptions,
        ILogger<DatabaseInitializer> logger)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(databaseOptions);
        ArgumentNullException.ThrowIfNull(logger);
        _serviceProvider = serviceProvider;
        _databaseOptions = databaseOptions;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogDatabaseInitializationFailed(_logger, ex, _databaseOptions.Value.Provider);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var provider = _databaseOptions.Value.Provider;

        if (provider == DatabaseProvider.Sqlite)
        {
            await InitializeSqliteAsync(dbContext, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }

        LogDatabaseInitialized(_logger, provider);
    }

    private async Task InitializeSqliteAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var connectionString = dbContext.Database.GetConnectionString() ?? string.Empty;
        var isInMemory = connectionString.Contains(":memory:", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase);

        if (!isInMemory)
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);
            var dataSource = builder.DataSource;

            if (!string.IsNullOrWhiteSpace(dataSource))
            {
                var fullDatabasePath = Path.GetFullPath(dataSource);
                var dbDirectory = Path.GetDirectoryName(fullDatabasePath);
                if (!string.IsNullOrEmpty(dbDirectory))
                {
                    Directory.CreateDirectory(dbDirectory);
                }

                if (File.Exists(fullDatabasePath) && new FileInfo(fullDatabasePath).Length > 0)
                {
                    var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
                    if (pendingMigrations.Any())
                    {
                        var backupDirectory = Path.Combine(dbDirectory ?? Directory.GetCurrentDirectory(), "backups");
                        Directory.CreateDirectory(backupDirectory);

                        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
                        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fullDatabasePath);
                        var extension = Path.GetExtension(fullDatabasePath);
                        var backupPath = Path.Combine(backupDirectory, $"{fileNameWithoutExt}-{timestamp}{extension}");

                        File.Copy(fullDatabasePath, backupPath, overwrite: false);
                        LogSqliteBackupCreated(_logger, backupPath);
                    }
                }
            }

            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            await dbContext.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Database initialization completed for provider {Provider}.")]
    private static partial void LogDatabaseInitialized(ILogger logger, DatabaseProvider provider);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Pre-migration SQLite backup created at {BackupPath}.")]
    private static partial void LogSqliteBackupCreated(ILogger logger, string backupPath);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Database initialization failed for provider {Provider}.")]
    private static partial void LogDatabaseInitializationFailed(ILogger logger, Exception exception, DatabaseProvider provider);
}
