using Microsoft.EntityFrameworkCore;
using Trail.Application.Abstractions;

namespace Trail.Infrastructure.Persistence.Sqlite;

public class SqliteAppDbContext : AppDbContext
{
    public SqliteAppDbContext(DbContextOptions<SqliteAppDbContext> options, ICurrentUser currentUser)
        : base(options, currentUser)
    {
    }
}
