using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

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
        Assert.Empty(ReferencesOutsideFramework("Trail.Domain"));
    }

    [Fact]
    public void Domain_DeclaresNoDependencies()
    {
        Assert.Empty(DeclaredDependencies("Trail.Domain"));
    }

    [Fact]
    public void Application_ReferencesOnlyDomainAndAbstractions()
    {
        var violations = ReferencesOutsideFramework("Trail.Application")
            .Where(name => !IsAllowedApplicationDependency(name));

        Assert.Empty(violations);
    }

    [Fact]
    public void Application_DeclaresOnlyDomainAndAbstractions()
    {
        var violations = DeclaredDependencies("Trail.Application")
            .Where(name => !IsAllowedApplicationDependency(name));

        Assert.Empty(violations);
    }

    [Fact]
    public void ReferencesOutsideFramework_DetectsThirdPartyAssemblies()
    {
        var references = ReferencesOutsideFramework(TestAssemblyName);

        Assert.Contains(references, name => name.StartsWith("xunit", StringComparison.Ordinal));
    }

    [Fact]
    public void DeclaredDependencies_DetectsPackageDependencies()
    {
        var dependencies = DeclaredDependencies(TestAssemblyName);

        Assert.Contains(dependencies, name => name.StartsWith("xunit", StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_DoesNotReferenceWeb()
    {
        var references = ReferencesOutsideFramework("Trail.Infrastructure");
        Assert.DoesNotContain("Trail.Web", references);
    }

    [Fact]
    public void Infrastructure_DoesNotDeclareWebDependency()
    {
        var dependencies = DeclaredDependencies("Trail.Infrastructure");
        Assert.DoesNotContain("Trail.Web", dependencies);
    }

    [Fact]
    public void Web_DoesNotDeclareDomainDependency()
    {
        var dependencies = DeclaredDependencies("Trail.Web");
        Assert.DoesNotContain("Trail.Domain", dependencies);
    }

    [Theory]
    [InlineData("Trail.Domain")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.EntityFrameworkCore.Abstractions")]
    [InlineData("Microsoft.Extensions.Logging.Abstractions")]
    public void IsAllowedApplicationDependency_AcceptsDomainAndAbstractions(string name)
    {
        Assert.True(IsAllowedApplicationDependency(name));
    }

    [Theory]
    [InlineData("Trail.Infrastructure")]
    [InlineData("Microsoft.EntityFrameworkCore.Sqlite")]
    [InlineData("Microsoft.EntityFrameworkCore.Relational")]
    [InlineData("Microsoft.Extensions.DependencyInjection")]
    [InlineData("Microsoft.AspNetCore.Http.Abstractions")]
    public void IsAllowedApplicationDependency_RejectsImplementations(string name)
    {
        Assert.False(IsAllowedApplicationDependency(name));
    }

    private static string TestAssemblyName => typeof(AssemblyReferenceTests).Assembly.GetName().Name!;

    private static bool IsAllowedApplicationDependency(string name) =>
        name is "Trail.Domain" or "Microsoft.EntityFrameworkCore" or "Microsoft.EntityFrameworkCore.Abstractions"
        || (name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal)
            && name.EndsWith(".Abstractions", StringComparison.Ordinal));

    private static List<string> ReferencesOutsideFramework(string assemblyName) =>
        [.. Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => !FrameworkAssemblies.Contains(name))];

    private static List<string> DeclaredDependencies(string projectName)
    {
        var depsFile = Path.Combine(AppContext.BaseDirectory, $"{TestAssemblyName}.deps.json");
        using var document = JsonDocument.Parse(File.ReadAllText(depsFile));
        var libraries = document.RootElement.GetProperty("targets").EnumerateObject().First().Value;
        var project = libraries.EnumerateObject()
            .Single(library => library.Name.StartsWith($"{projectName}/", StringComparison.Ordinal))
            .Value;

        return project.TryGetProperty("dependencies", out var dependencies)
            ? [.. dependencies.EnumerateObject().Select(dependency => dependency.Name)]
            : [];
    }
}
