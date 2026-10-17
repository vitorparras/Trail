using Microsoft.EntityFrameworkCore;
using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Infrastructure.Persistence.PostgreSql;
using Trail.Infrastructure.Persistence.Sqlite;

namespace Trail.Application.Tests.Persistence;

public sealed class PendingModelChangesTests
{
    [Fact]
    public void SqliteAppDbContext_HasZeroPendingModelChanges()
    {
        var options = new DbContextOptionsBuilder<SqliteAppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new SqliteAppDbContext(options, new TestCurrentUser());

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void PostgreSqlAppDbContext_HasZeroPendingModelChanges()
    {
        var options = new DbContextOptionsBuilder<PostgreSqlAppDbContext>()
            .UseNpgsql("Host=localhost;Database=test")
            .Options;

        using var context = new PostgreSqlAppDbContext(options, new TestCurrentUser());

        Assert.False(context.Database.HasPendingModelChanges());
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => false;

        public UserId Id => default;
    }
}
