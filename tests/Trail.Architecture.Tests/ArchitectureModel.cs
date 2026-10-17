using ArchUnitNET.Loader;
using Trail.Application;
using Trail.Domain.Common;
using Trail.Infrastructure.Persistence;
using Trail.Web;

namespace Trail.Architecture.Tests;

public static class ArchitectureModel
{
    public static readonly System.Reflection.Assembly DomainAssembly = typeof(UserId).Assembly;
    public static readonly System.Reflection.Assembly ApplicationAssembly = typeof(DependencyInjection).Assembly;
    public static readonly System.Reflection.Assembly InfrastructureAssembly = typeof(AppDbContext).Assembly;
    public static readonly System.Reflection.Assembly WebAssembly = typeof(SharedResource).Assembly;
    public static readonly System.Reflection.Assembly TestsAssembly = typeof(ArchitectureModel).Assembly;

    public static readonly ArchUnitNET.Domain.Architecture ProductionArchitecture = new ArchLoader()
        .LoadAssemblies(DomainAssembly, ApplicationAssembly, InfrastructureAssembly, WebAssembly)
        .Build();

    public static ArchUnitNET.Domain.Architecture BuildArchitectureWithNamespaces(params string[] namespaces) =>
        new ArchLoader()
            .LoadAssemblies(DomainAssembly, ApplicationAssembly, InfrastructureAssembly, WebAssembly)
            .LoadNamespacesWithinAssembly(TestsAssembly, namespaces)
            .Build();

    public static ArchUnitNET.Domain.Architecture BuildIsolatedArchitecture(params string[] namespaces) =>
        new ArchLoader()
            .LoadNamespacesWithinAssembly(TestsAssembly, namespaces)
            .Build();
}
