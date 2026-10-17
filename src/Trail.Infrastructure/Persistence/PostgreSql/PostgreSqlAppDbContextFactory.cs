using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Trail.Application.Abstractions;
using Trail.Domain.Common;

namespace Trail.Infrastructure.Persistence.PostgreSql;

public sealed class PostgreSqlAppDbContextFactory : IDesignTimeDbContextFactory<PostgreSqlAppDbContext>
{
    public PostgreSqlAppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PostgreSqlAppDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=trail_design;Username=postgres;Password=postgres",
            builder => builder.MigrationsAssembly(typeof(PostgreSqlAppDbContext).Assembly.GetName().Name));

        return new PostgreSqlAppDbContext(optionsBuilder.Options, new DesignTimeCurrentUser());
    }

    private sealed class DesignTimeCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => false;

        public UserId Id => default;
    }
}
