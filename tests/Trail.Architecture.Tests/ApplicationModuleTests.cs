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
        var rule = CreateModuleIsolationRule(module);

        rule.Check(ArchitectureModel.ProductionArchitecture);
    }

    [Fact]
    public void ApplicationModules_DetectsCrossModuleDependency_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Application.Practice.Violations",
            "Trail.Application.Planning.Violations");

        var rule = CreateModuleIsolationRule("Practice");

        Assert.Throws<FailedArchRuleException>(() =>
            rule.Check(violationArchitecture));
    }

    public static IArchRule CreateModuleIsolationRule(string module)
    {
        var otherModules = IsolatedModules.Where(m => m != module);
        var pattern = @"^Trail\.Application\.(" + string.Join('|', otherModules) + @")(\..*)?$";

        return Types().That().ResideInNamespaceMatching($@"^Trail\.Application\.{module}(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(pattern)
            .WithoutRequiringPositiveResults();
    }
}
