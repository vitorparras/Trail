using Microsoft.EntityFrameworkCore;
using Trail.Application.Abstractions;

namespace Trail.Infrastructure.Persistence.PostgreSql;

public class PostgreSqlAppDbContext : AppDbContext
{
    public PostgreSqlAppDbContext(DbContextOptions<PostgreSqlAppDbContext> options, ICurrentUser currentUser)
        : base(options, currentUser)
    {
    }
}
