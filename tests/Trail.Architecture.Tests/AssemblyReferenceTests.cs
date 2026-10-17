using System.Reflection;
using System.Runtime.InteropServices;

namespace Trail.Architecture.Tests;

public sealed class AssemblyReferenceTests
{
    private static readonly HashSet<string> FrameworkAssemblies = Directory
        .EnumerateFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")
        .Select(Path.GetFileNameWithoutExtension)
        .OfType<string>()
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Domain_ReferencesOnlyTheDotNetFramework()
    {
        var violations = ReferencesOutsideFramework("Trail.Domain");

        Assert.Empty(violations);
    }

    [Fact]
    public void Application_ReferencesOnlyDomainAndAbstractions()
    {
        var violations = ReferencesOutsideFramework("Trail.Application")
            .Where(name => !IsAllowedApplicationDependency(name));

        Assert.Empty(violations);
    }

    [Fact]
    public void ReferencesOutsideFramework_DetectsThirdPartyAssemblies()
    {
        var references = ReferencesOutsideFramework(typeof(AssemblyReferenceTests).Assembly.GetName().Name!);

        Assert.Contains(references, name => name.StartsWith("xunit", StringComparison.Ordinal));
    }

    private static bool IsAllowedApplicationDependency(string name) =>
        name is "Trail.Domain" or "Microsoft.EntityFrameworkCore" or "Microsoft.EntityFrameworkCore.Abstractions"
        || (name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal)
            && name.EndsWith(".Abstractions", StringComparison.Ordinal));

    private static List<string> ReferencesOutsideFramework(string assemblyName) =>
        [.. Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => !FrameworkAssemblies.Contains(name))];
}
