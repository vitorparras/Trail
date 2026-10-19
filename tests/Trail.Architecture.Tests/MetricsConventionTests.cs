using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Trail.Architecture.Tests;

public sealed class MetricsConventionTests
{
    public static readonly IArchRule PublicMetricsShouldResideInTelemetryRule =
        Classes().That().ResideInNamespaceMatching(@"^Trail\.Application(\..*)?$")
            .And().ArePublic()
            .And().HaveNameEndingWith("Metrics")
            .Should().ResideInNamespaceMatching(@"^Trail\.Application\.Telemetry$");

    [Fact]
    public void PublicMetrics_ResideInApplicationTelemetry()
    {
        PublicMetricsShouldResideInTelemetryRule.Check(ArchitectureModel.ProductionArchitecture);
    }

    [Fact]
    public void PublicMetrics_DetectsMetricsOutsideTelemetry_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces("Trail.Application.Violations.Metrics");

        Assert.Throws<FailedArchRuleException>(() =>
            PublicMetricsShouldResideInTelemetryRule.Check(violationArchitecture));
    }
}
