using Trail.Application.Abstractions;
using Trail.Infrastructure.Persistence;
using Trail.Web.Planning;
using Trail.Web.Tests.Persistence;

namespace Trail.Web.Tests.Performance;

public sealed class Wave0PerformanceFixture : PerformanceFixture
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IPlanLinkContributor, PerformancePlanLinkContributor>();
        services.AddScoped<IDeletionImpactContributor, PerformanceDeletionImpactContributor>();
    }
}

[PerformanceTest]
[Collection(PerformanceCollections.Sequential)]
public sealed class Wave0PerformanceTests(Wave0PerformanceFixture fixture) : IClassFixture<Wave0PerformanceFixture>
{
    private static readonly PerformanceScenario PlanPage = new(
        "Plan page",
        plan => plan.PlanPath,
        plan => [plan.Name, "Performance link"]);

    private static readonly PerformanceScenario SubjectPage = new(
        "Subject page (largest subject)",
        plan => plan.SubjectPath(plan.LargestSubject),
        plan => [plan.LargestSubject.Name]);

    private static readonly PerformanceScenario SubjectDeletionPreview = new(
        "Subject deletion preview (largest subject)",
        plan => $"{plan.PlanPath}?handler=DeleteSubjectImpact&subjectId={plan.LargestSubject.Id.Value}",
        HtmxPartial: true);

    private static readonly PerformanceScenario TopicDeletionPreview = new(
        "Topic deletion preview (first topic of the largest subject)",
        plan => $"{plan.SubjectPath(plan.LargestSubject)}?handler=DeleteTopicImpact&topicId={plan.LargestSubject.TopicIds[0].Value}",
        HtmxPartial: true);

    private static readonly PerformanceScenario DashboardWithChosenPlan = new(
        "Dashboard with a chosen plan",
        plan => $"/?plano={plan.PlanId.Value}",
        plan => [plan.Name]);

    private static readonly PerformanceScenario DashboardWithDefaultRule = new(
        "Dashboard with the default rule",
        _ => "/",
        plan => [plan.Name]);

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public Task PlanPage_ThousandTopics_StaysWithinBudget(DatabaseProvider provider) =>
        fixture.AssertWithinBudgetAsync(provider, PlanPage);

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public Task SubjectPage_ThousandTopics_StaysWithinBudget(DatabaseProvider provider) =>
        fixture.AssertWithinBudgetAsync(provider, SubjectPage);

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public Task SubjectDeletionPreview_ThousandTopics_StaysWithinBudget(DatabaseProvider provider) =>
        fixture.AssertWithinBudgetAsync(provider, SubjectDeletionPreview);

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public Task TopicDeletionPreview_ThousandTopics_StaysWithinBudget(DatabaseProvider provider) =>
        fixture.AssertWithinBudgetAsync(provider, TopicDeletionPreview);

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public Task Dashboard_WithChosenPlan_StaysWithinBudget(DatabaseProvider provider) =>
        fixture.AssertWithinBudgetAsync(provider, DashboardWithChosenPlan);

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public Task Dashboard_WithDefaultRule_StaysWithinBudget(DatabaseProvider provider) =>
        fixture.AssertWithinBudgetAsync(provider, DashboardWithDefaultRule);
}
