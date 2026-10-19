using Microsoft.EntityFrameworkCore;
using Trail.Domain.Common;
using Trail.Infrastructure.Persistence;
using Trail.Infrastructure.Persistence.PostgreSql;
using Trail.Infrastructure.Persistence.Sqlite;
using Trail.Web.Tests.Accounts;

namespace Trail.Web.Tests.Performance;

public sealed class PerformanceSeedContext
{
    private readonly IServiceProvider _services;

    internal PerformanceSeedContext(IServiceProvider services, DatabaseProvider provider, UserId ownerId)
    {
        _services = services;
        Provider = provider;
        OwnerId = ownerId;
    }

    public DatabaseProvider Provider { get; }

    public UserId OwnerId { get; }

    public SeededPerformancePlan Plan { get; internal set; } = null!;

    public async Task WithDbContextAsync(Func<AppDbContext, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var scope = _services.CreateScope();
        await using var dbContext = CreateDbContext(scope.ServiceProvider);
        await action(dbContext);
    }

    private AppDbContext CreateDbContext(IServiceProvider services)
    {
        var currentUser = new TestCurrentUser
        {
            IsAuthenticated = true,
            Id = OwnerId,
        };

        return Provider == DatabaseProvider.Sqlite
            ? new SqliteAppDbContext(services.GetRequiredService<DbContextOptions<SqliteAppDbContext>>(), currentUser)
            : new PostgreSqlAppDbContext(services.GetRequiredService<DbContextOptions<PostgreSqlAppDbContext>>(), currentUser);
    }
}
