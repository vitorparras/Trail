using System.Text.RegularExpressions;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Trail.Architecture.Tests;

public sealed class ApplicationModuleTests
{
    private static readonly string[] IsolatedModules =
    [
        "Planning",
        "Practice",
        "StudyTime",
        "Fitness",
        "Accounts",
    ];

    public static TheoryData<string> Modules => new(IsolatedModules);

    [Theory]
    [MemberData(nameof(Modules))]
    public void ApplicationModules_DoNotDependOnOtherIsolatedModules(string module)
    {
        var rule = CreateModuleIsolationRule(module, ArchitectureModel.ProductionArchitecture);

        rule.Check(ArchitectureModel.ProductionArchitecture);
    }

    [Fact]
    public void ApplicationModules_DetectsCrossModuleDependency_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Application.Practice.Violations",
            "Trail.Application.Planning.Violations");

        var rule = CreateModuleIsolationRule("Practice", violationArchitecture);

        Assert.Throws<FailedArchRuleException>(() =>
            rule.Check(violationArchitecture));
    }

    [Fact]
    public void ApplicationModules_EvaluatesNonVacuously_WhenModuleContainsTypes()
    {
        var architecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Application.Practice.Violations",
            "Trail.Application.Planning.Violations");

        var strictRule = CreateModuleIsolationRule("Practice", architecture);

        Assert.Throws<FailedArchRuleException>(() =>
            strictRule.Check(architecture));
    }

    public static IArchRule CreateModuleIsolationRule(string module, ArchUnitNET.Domain.Architecture? architecture = null)
    {
        var otherModules = IsolatedModules.Where(m => m != module);
        var pattern = @"^Trail\.Application\.(" + string.Join('|', otherModules) + @")(\..*)?$";

        var rule = Types().That().ResideInNamespaceMatching($@"^Trail\.Application\.{module}(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(pattern);

        var hasMatchingTypes = architecture != null && architecture.Types.Any(t =>
            Regex.IsMatch(t.Namespace.FullName, $@"^Trail\.Application\.{module}(\..*)?$"));

        return hasMatchingTypes
            ? rule
            : rule.WithoutRequiringPositiveResults();
    }
}
