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

    public static readonly IArchRule DomainShouldNotDependOnEntityFrameworkCoreRule =
        Types().That().ResideInNamespaceMatching(@"^Trail\.Domain(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Microsoft\.EntityFrameworkCore(\..*)?$")
            .WithoutRequiringPositiveResults();

    public static readonly IArchRule WebShouldOnlyDependOnInfrastructureInProgramRule =
        Types().That().ResideInNamespaceMatching(@"^Trail\.Web(\..*)?$")
            .And().DoNotHaveFullName("Program")
            .And().DoNotHaveFullNameStartingWith("Program+")
            .And().DoNotHaveFullNameStartingWith("Program.<>")
            .And().DoNotHaveFullNameStartingWith("<Program>")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Trail\.Infrastructure(\..*)?$")
            .WithoutRequiringPositiveResults();

    public static readonly IArchRule PagesShouldNotDependOnDbContextRule =
        Types().That().ResideInNamespaceMatching(@"^Trail\.Web\.Pages(\..*)?$")
            .Or().AreAssignableTo(typeof(PageModel))
            .Should().NotDependOnAny(typeof(IAppDbContext), typeof(AppDbContext))
            .WithoutRequiringPositiveResults();

    public static readonly IArchRule RestrictedLibrariesShouldOnlyBeUsedByInfrastructureRule =
        Types().That().ResideInNamespaceMatching(@"^(Trail\.Domain|Trail\.Application|Trail\.Web)(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(RestrictedLibrariesPattern)
            .WithoutRequiringPositiveResults();

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
    public void RestrictedLibraries_DetectsUsageOutsideInfrastructure_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces("Trail.Web.Violations");

        Assert.Throws<FailedArchRuleException>(() =>
            RestrictedLibrariesShouldOnlyBeUsedByInfrastructureRule.Check(violationArchitecture));
    }
}
