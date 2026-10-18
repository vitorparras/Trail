using Microsoft.EntityFrameworkCore;
using Trail.Infrastructure.Identity;
using Trail.Infrastructure.Persistence;
using Trail.Infrastructure.Persistence.Sqlite;

namespace Trail.Application.Tests.Persistence;

public sealed class EntityConfigurationTests
{
    private const string DomainNamespacePrefix = "Trail.Domain.";
    private const string ConfigurationNamespacePrefix = "Trail.Infrastructure.Persistence.Configurations.";
    private const string IdentityNamespace = "Microsoft.AspNetCore.Identity";

    private static readonly Dictionary<Type, string> ModulesOutsideDomain = new()
    {
        [typeof(AccountInvitation)] = "Accounts",
    };

    [Fact]
    public void ModelEntityTypes_ExceptIdentityAndOwned_HaveOneConfigurationInTheirModuleNamespace()
    {
        var options = new DbContextOptionsBuilder<SqliteAppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new SqliteAppDbContext(options, new TestCurrentUser());

        var configurations = typeof(AppDbContext).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType
                    && contract.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>))
                .Select(contract => (Entity: contract.GetGenericArguments()[0], Configuration: type)))
            .ToLookup(pair => pair.Entity, pair => pair.Configuration);

        var entityTypes = context.Model.GetEntityTypes()
            .Where(entityType => !entityType.IsOwned() && !IsIdentityType(entityType.ClrType))
            .Select(entityType => entityType.ClrType)
            .ToList();

        var violations = new List<string>();

        foreach (var entityType in entityTypes)
        {
            var module = ModuleOf(entityType);
            var matches = configurations[entityType].ToList();

            if (module is null)
            {
                violations.Add($"{entityType.FullName} does not belong to a known module.");
            }
            else if (matches.Count != 1)
            {
                violations.Add($"{entityType.FullName} has {matches.Count} configurations; expected exactly one.");
            }
            else if (!string.Equals(matches[0].Namespace, ConfigurationNamespacePrefix + module, StringComparison.Ordinal))
            {
                violations.Add($"{matches[0].FullName} should be in {ConfigurationNamespacePrefix + module}.");
            }
        }

        Assert.NotEmpty(entityTypes);
        Assert.Empty(violations);
    }

    private static bool IsIdentityType(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.Namespace, IdentityNamespace, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ModuleOf(Type type)
    {
        var typeNamespace = type.Namespace ?? string.Empty;

        if (typeNamespace.StartsWith(DomainNamespacePrefix, StringComparison.Ordinal))
        {
            return typeNamespace[DomainNamespacePrefix.Length..].Split('.')[0];
        }

        return ModulesOutsideDomain.GetValueOrDefault(type);
    }
}
