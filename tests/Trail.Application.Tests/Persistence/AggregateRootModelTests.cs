using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Infrastructure.Persistence;
using Trail.Infrastructure.Persistence.PostgreSql;
using Trail.Infrastructure.Persistence.Sqlite;

namespace Trail.Application.Tests.Persistence;

public sealed class AggregateRootModelTests
{
    [Theory]
    [InlineData(DatabaseProvider.Sqlite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public void AggregateRoots_InModel_HaveVersionConcurrencyTokenAndOwnerQueryFilter(DatabaseProvider provider)
    {
        using var context = CreateContext(provider, discoverDbSets: true);

        AssertAggregateRootConventions(context.Model);
    }

    [Theory]
    [InlineData(DatabaseProvider.Sqlite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public void AggregateRoots_ReachedOnlyThroughConfigurations_HaveVersionConcurrencyTokenAndOwnerQueryFilter(DatabaseProvider provider)
    {
        using var context = CreateContext(provider, discoverDbSets: false);

        AssertAggregateRootConventions(context.Model);
    }

    private static AppDbContext CreateContext(DatabaseProvider provider, bool discoverDbSets)
    {
        var currentUser = new TestCurrentUser();

        if (provider == DatabaseProvider.Sqlite)
        {
            var options = new DbContextOptionsBuilder<SqliteAppDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;

            return discoverDbSets
                ? new SqliteAppDbContext(options, currentUser)
                : new ConfigurationOnlySqliteAppDbContext(options, currentUser);
        }

        var postgreSqlOptions = new DbContextOptionsBuilder<PostgreSqlAppDbContext>()
            .UseNpgsql("Host=localhost;Database=test")
            .Options;

        return discoverDbSets
            ? new PostgreSqlAppDbContext(postgreSqlOptions, currentUser)
            : new ConfigurationOnlyPostgreSqlAppDbContext(postgreSqlOptions, currentUser);
    }

    private static void AssertAggregateRootConventions(IModel model)
    {
        var aggregateRoots = model.GetEntityTypes()
            .Where(entityType => entityType.BaseType is null && typeof(IAggregateRoot).IsAssignableFrom(entityType.ClrType))
            .ToList();

        var violations = new List<string>();

        foreach (var entityType in aggregateRoots)
        {
            var version = entityType.FindProperty(nameof(IAggregateRoot.Version));

            if (version is null || !version.IsConcurrencyToken)
            {
                violations.Add($"{entityType.DisplayName()} does not use {nameof(IAggregateRoot.Version)} as a concurrency token.");
            }

            if (!entityType.GetDeclaredQueryFilters().Any(filter => ReferencesOwnerId(filter.Expression)))
            {
                violations.Add($"{entityType.DisplayName()} has no query filter on {nameof(IAggregateRoot.OwnerId)}.");
            }
        }

        Assert.NotEmpty(aggregateRoots);
        Assert.Empty(violations);
    }

    private static bool ReferencesOwnerId(LambdaExpression? filter)
    {
        if (filter is null)
        {
            return false;
        }

        var finder = new OwnerIdReferenceFinder();
        finder.Visit(filter.Body);
        return finder.Found;
    }

    private sealed class OwnerIdReferenceFinder : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ParameterExpression
                && string.Equals(node.Member.Name, nameof(IAggregateRoot.OwnerId), StringComparison.Ordinal))
            {
                Found = true;
            }

            return base.VisitMember(node);
        }
    }

    private sealed class ConfigurationOnlySqliteAppDbContext(
        DbContextOptions<SqliteAppDbContext> options,
        ICurrentUser currentUser)
        : SqliteAppDbContext(options, currentUser)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.Conventions.Remove<DbSetFindingConvention>();
        }
    }

    private sealed class ConfigurationOnlyPostgreSqlAppDbContext(
        DbContextOptions<PostgreSqlAppDbContext> options,
        ICurrentUser currentUser)
        : PostgreSqlAppDbContext(options, currentUser)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.Conventions.Remove<DbSetFindingConvention>();
        }
    }
}
