using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Trail.Application.Abstractions;
using Trail.Domain.Common;

namespace Trail.Infrastructure.Persistence.Sqlite;

public sealed class SqliteAppDbContextFactory : IDesignTimeDbContextFactory<SqliteAppDbContext>
{
    public SqliteAppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SqliteAppDbContext>();
        optionsBuilder.UseSqlite(
            "Data Source=trail_design.db",
            builder => builder.MigrationsAssembly(typeof(SqliteAppDbContext).Assembly.GetName().Name));

        return new SqliteAppDbContext(optionsBuilder.Options, new DesignTimeCurrentUser());
    }

    private sealed class DesignTimeCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => false;

        public UserId Id => default;
    }
}
