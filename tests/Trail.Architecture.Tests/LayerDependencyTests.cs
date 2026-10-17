using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Application.Abstractions;
using Trail.Infrastructure.Persistence;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Trail.Architecture.Tests;

public sealed class LayerDependencyTests
{
    private const string RestrictedLibrariesPattern =
        @"^(Microsoft\.EntityFrameworkCore\.Sqlite|Npgsql\.EntityFrameworkCore\.PostgreSQL|Microsoft\.Data\.Sqlite|Npgsql|Microsoft\.AspNetCore\.Identity|MailKit|MimeKit|ClosedXML)($|\..*)";

    public static readonly IArchRule DomainShouldNotDependOnOtherLayersRule =
        Types().That().ResideInNamespaceMatching(@"^Trail\.Domain(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Trail\.(Application|Infrastructure|Web)(\..*)?$")
            .WithoutRequiringPositiveResults();

    public static readonly IArchRule DomainShouldNotDependOnEntityFrameworkCoreRule =
        Types().That().ResideInNamespaceMatching(@"^Trail\.Domain(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Microsoft\.EntityFrameworkCore(\..*)?$")
            .WithoutRequiringPositiveResults();

    public static readonly IArchRule ApplicationShouldNotDependOnInfrastructureOrWebRule =
        Types().That().ResideInNamespaceMatching(@"^Trail\.Application(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Trail\.(Infrastructure|Web)(\..*)?$")
            .WithoutRequiringPositiveResults();

    public static readonly IArchRule InfrastructureShouldNotDependOnWebRule =
        Types().That().ResideInNamespaceMatching(@"^Trail\.Infrastructure(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Trail\.Web(\..*)?$")
            .WithoutRequiringPositiveResults();

    public static readonly IArchRule WebShouldOnlyDependOnInfrastructureInProgramRule =
        Types().That().FollowCustomPredicate(
                IsWebTypeOutsideProgram,
                "reside in Web outside Program")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Trail\.Infrastructure(\..*)?$")
            .WithoutRequiringPositiveResults();

    public static readonly IArchRule PagesShouldNotDependOnDbContextRule =
        Types().That().ResideInNamespaceMatching(@"^Trail\.Web\.Pages(\..*)?$")
            .Or().AreAssignableTo(typeof(PageModel))
            .Should().NotDependOnAny(typeof(IAppDbContext), typeof(AppDbContext))
            .WithoutRequiringPositiveResults();

    public static readonly IArchRule RestrictedLibrariesShouldOnlyBeUsedByInfrastructureRule =
        Types().That().FollowCustomPredicate(
                IsDomainApplicationOrWebType,
                "reside in Domain, Application, or Web")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(RestrictedLibrariesPattern)
            .WithoutRequiringPositiveResults();

    [Fact]
    public void Domain_DoesNotDependOnOtherLayers()
    {
        DomainShouldNotDependOnOtherLayersRule.Check(ArchitectureModel.ProductionArchitecture);
    }

    [Fact]
    public void Domain_DetectsApplicationDependency_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces("Trail.Domain.Violations.Layers");

        Assert.Throws<FailedArchRuleException>(() =>
            DomainShouldNotDependOnOtherLayersRule.Check(violationArchitecture));
    }

    [Fact]
    public void Domain_DoesNotDependOnEntityFrameworkCore()
    {
        DomainShouldNotDependOnEntityFrameworkCoreRule.Check(ArchitectureModel.ProductionArchitecture);
    }

    [Fact]
    public void Domain_DetectsEntityFrameworkCoreUsage_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces("Trail.Domain.Violations");

        Assert.Throws<FailedArchRuleException>(() =>
            DomainShouldNotDependOnEntityFrameworkCoreRule.Check(violationArchitecture));
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrWeb()
    {
        ApplicationShouldNotDependOnInfrastructureOrWebRule.Check(ArchitectureModel.ProductionArchitecture);
    }

    [Fact]
    public void Application_DetectsInfrastructureDependency_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces("Trail.Application.Violations.Layers");

        Assert.Throws<FailedArchRuleException>(() =>
            ApplicationShouldNotDependOnInfrastructureOrWebRule.Check(violationArchitecture));
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnWeb()
    {
        InfrastructureShouldNotDependOnWebRule.Check(ArchitectureModel.ProductionArchitecture);
    }

    [Fact]
    public void Infrastructure_DetectsWebDependency_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces("Trail.Infrastructure.Violations.Layers");

        Assert.Throws<FailedArchRuleException>(() =>
            InfrastructureShouldNotDependOnWebRule.Check(violationArchitecture));
    }

    [Fact]
    public void Web_UsesInfrastructureOnlyInComposition()
    {
        WebShouldOnlyDependOnInfrastructureInProgramRule.Check(ArchitectureModel.ProductionArchitecture);
    }

    [Fact]
    public void Web_DetectsInfrastructureUsageOutsideProgram_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces("Trail.Web.Violations");

        Assert.Throws<FailedArchRuleException>(() =>
            WebShouldOnlyDependOnInfrastructureInProgramRule.Check(violationArchitecture));
    }

    [Fact]
    public void Pages_DoNotDependOnAppDbContext()
    {
        PagesShouldNotDependOnDbContextRule.Check(ArchitectureModel.ProductionArchitecture);
    }

    [Fact]
    public void Pages_DetectsIAppDbContextUsage_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces("Trail.Web.Pages.Violations");

        Assert.Throws<FailedArchRuleException>(() =>
            PagesShouldNotDependOnDbContextRule.Check(violationArchitecture));
    }

    [Fact]
    public void RestrictedLibraries_AreIsolatedToInfrastructure()
    {
        RestrictedLibrariesShouldOnlyBeUsedByInfrastructureRule.Check(ArchitectureModel.ProductionArchitecture);
    }

    [Fact]
    public void RestrictedLibraries_DetectsUsageInWeb_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces("Trail.Web.Violations");

        Assert.Throws<FailedArchRuleException>(() =>
            RestrictedLibrariesShouldOnlyBeUsedByInfrastructureRule.Check(violationArchitecture));
    }

    [Fact]
    public void RestrictedLibraries_DetectsUsageInApplication_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces("Trail.Application.Violations.Restricted");

        Assert.Throws<FailedArchRuleException>(() =>
            RestrictedLibrariesShouldOnlyBeUsedByInfrastructureRule.Check(violationArchitecture));
    }

    private static bool IsWebTypeOutsideProgram(IType type)
    {
        var isInWeb = type.Assembly.FullName == ArchitectureModel.WebAssembly.FullName
            || type.Namespace.FullName.StartsWith("Trail.Web", StringComparison.Ordinal);

        if (!isInWeb)
        {
            return false;
        }

        var fullName = type.FullName;
        var name = type.Name;

        return !(fullName == "Program"
            || fullName.StartsWith("Program+", StringComparison.Ordinal)
            || fullName.StartsWith("Program.<>", StringComparison.Ordinal)
            || fullName.StartsWith("<Program>", StringComparison.Ordinal)
            || fullName.StartsWith("<<Main>", StringComparison.Ordinal)
            || name == "Program"
            || name.StartsWith("Program+", StringComparison.Ordinal)
            || name.StartsWith("<Program>", StringComparison.Ordinal)
            || name.StartsWith("<<Main>", StringComparison.Ordinal));
    }

    private static bool IsDomainApplicationOrWebType(IType type)
    {
        if (type.Assembly.FullName == ArchitectureModel.DomainAssembly.FullName
            || type.Assembly.FullName == ArchitectureModel.ApplicationAssembly.FullName
            || type.Assembly.FullName == ArchitectureModel.WebAssembly.FullName)
        {
            return true;
        }

        var ns = type.Namespace.FullName;
        return ns.StartsWith("Trail.Domain", StringComparison.Ordinal)
            || ns.StartsWith("Trail.Application", StringComparison.Ordinal)
            || ns.StartsWith("Trail.Web", StringComparison.Ordinal);
    }
}
