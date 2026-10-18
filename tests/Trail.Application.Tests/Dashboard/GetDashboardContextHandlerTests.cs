using Trail.Application.Abstractions;
using Trail.Application.Dashboard;
using Trail.Application.Tests.Persistence;
using Trail.Domain.Common;
using Trail.Domain.Planning;
using Trail.Infrastructure.Persistence;

namespace Trail.Application.Tests.Dashboard;

public sealed class GetDashboardContextHandlerTests
{
    private static readonly DateTime Monday = new(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task HandleAsync_WithoutPlans_ReturnsContextWithoutPlan(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var activity = new StubPlanActivityContributor();
        await using var harness = await DashboardTestHarness.CreateAsync(provider, [activity], ct);
        var user = UserId.New();
        await harness.CreatePlanAsync(UserId.New(), "Foreign plan", ct);

        var result = await harness.HandleAsync(user, new GetDashboardContextQuery(), ct);

        Assert.True(result.IsOk);
        Assert.Empty(result.Data.Plans);
        Assert.Null(result.Data.Plan);
        Assert.False(result.Data.HasPlan);
        Assert.Equal(0, activity.Calls);
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task HandleAsync_WithoutChoiceOrActivity_SelectsFirstPlanInListOrderAmongOwnPlans(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await DashboardTestHarness.CreateAsync(provider, [new StubPlanActivityContributor()], ct);
        var user = UserId.New();
        await harness.CreatePlanAsync(UserId.New(), "Aardvark plan", ct);
        var charlie = await harness.CreatePlanAsync(user, "Charlie plan", ct);
        var alpha = await harness.CreatePlanAsync(user, "Alpha plan", ct);
        var bravo = await harness.CreatePlanAsync(user, "Bravo plan", ct);

        var result = await harness.HandleAsync(user, new GetDashboardContextQuery(), ct);

        Assert.True(result.IsOk);
        Assert.Equal(
            [new DashboardPlan(alpha, "Alpha plan"), new DashboardPlan(bravo, "Bravo plan"), new DashboardPlan(charlie, "Charlie plan")],
            result.Data.Plans);
        Assert.True(result.Data.HasPlan);
        Assert.Equal(new DashboardPlan(alpha, "Alpha plan"), result.Data.Plan);
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task HandleAsync_WithOwnPlan_SelectsItWithoutAskingForActivity(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var user = UserId.New();
        var activity = new StubPlanActivityContributor();
        await using var harness = await DashboardTestHarness.CreateAsync(provider, [activity], ct);
        var alpha = await harness.CreatePlanAsync(user, "Alpha plan", ct);
        var bravo = await harness.CreatePlanAsync(user, "Bravo plan", ct);
        activity.Report(alpha, Monday);

        var result = await harness.HandleAsync(user, new GetDashboardContextQuery(bravo), ct);

        Assert.True(result.IsOk);
        Assert.Equal(new DashboardPlan(bravo, "Bravo plan"), result.Data.Plan);
        Assert.Equal([alpha, bravo], result.Data.Plans.Select(plan => plan.Id));
        Assert.Equal(0, activity.Calls);
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task HandleAsync_WithAnotherUsersPlan_ReturnsNotFound(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await DashboardTestHarness.CreateAsync(provider, [new StubPlanActivityContributor()], ct);
        var attacker = UserId.New();
        var victim = UserId.New();
        await harness.CreatePlanAsync(attacker, "Attacker plan", ct);
        var victimPlan = await harness.CreatePlanAsync(victim, "Victim plan", ct);

        var result = await harness.HandleAsync(attacker, new GetDashboardContextQuery(victimPlan), ct);

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("planning.plan.not_found", Assert.Single(result.Messages).Code);
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task HandleAsync_WithUnknownPlan_ReturnsNotFound(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await DashboardTestHarness.CreateAsync(provider, [], ct);
        var user = UserId.New();
        await harness.CreatePlanAsync(user, "Alpha plan", ct);

        var result = await harness.HandleAsync(user, new GetDashboardContextQuery(StudyPlanId.New()), ct);

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("planning.plan.not_found", Assert.Single(result.Messages).Code);
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task HandleAsync_WithActivityFromSeveralContributors_SelectsPlanWithMostRecentActivity(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var user = UserId.New();
        var questions = new StubPlanActivityContributor();
        var sessions = new StubPlanActivityContributor();
        await using var harness = await DashboardTestHarness.CreateAsync(provider, [questions, sessions], ct);
        var alpha = await harness.CreatePlanAsync(user, "Alpha plan", ct);
        var bravo = await harness.CreatePlanAsync(user, "Bravo plan", ct);
        var charlie = await harness.CreatePlanAsync(user, "Charlie plan", ct);
        questions.Report(alpha, Monday.AddDays(2));
        questions.Report(bravo, Monday);
        sessions.Report(alpha, Monday.AddDays(1));
        sessions.Report(charlie, Monday.AddDays(2).AddSeconds(1));

        var result = await harness.HandleAsync(user, new GetDashboardContextQuery(), ct);

        Assert.True(result.IsOk);
        Assert.Equal(new DashboardPlan(charlie, "Charlie plan"), result.Data.Plan);
        Assert.Equal(3, result.Data.Plans.Count);
        Assert.Equal(1, questions.Calls);
        Assert.Equal(1, sessions.Calls);
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task HandleAsync_WithTiedActivity_SelectsTiedPlanThatComesFirstInListOrder(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var user = UserId.New();
        var questions = new StubPlanActivityContributor();
        var sessions = new StubPlanActivityContributor();
        await using var harness = await DashboardTestHarness.CreateAsync(provider, [questions, sessions], ct);
        await harness.CreatePlanAsync(user, "Alpha plan", ct);
        var bravo = await harness.CreatePlanAsync(user, "Bravo plan", ct);
        var charlie = await harness.CreatePlanAsync(user, "Charlie plan", ct);
        questions.Report(charlie, Monday);
        sessions.Report(bravo, Monday);

        var result = await harness.HandleAsync(user, new GetDashboardContextQuery(), ct);

        Assert.True(result.IsOk);
        Assert.Equal(new DashboardPlan(bravo, "Bravo plan"), result.Data.Plan);
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task HandleAsync_WithActivityOnlyForPlansOutsideTheList_FallsBackToFirstPlan(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var user = UserId.New();
        var activity = new StubPlanActivityContributor();
        await using var harness = await DashboardTestHarness.CreateAsync(provider, [activity], ct);
        var foreignPlan = await harness.CreatePlanAsync(UserId.New(), "Aardvark plan", ct);
        var alpha = await harness.CreatePlanAsync(user, "Alpha plan", ct);
        await harness.CreatePlanAsync(user, "Bravo plan", ct);
        activity.Report(foreignPlan, Monday.AddDays(5));
        activity.Report(StudyPlanId.New(), Monday.AddDays(6));

        var result = await harness.HandleAsync(user, new GetDashboardContextQuery(), ct);

        Assert.True(result.IsOk);
        Assert.Equal(new DashboardPlan(alpha, "Alpha plan"), result.Data.Plan);
        Assert.DoesNotContain(result.Data.Plans, plan => plan.Id == foreignPlan);
        Assert.Equal(1, activity.Calls);
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task HandleAsync_WithPlansOfTheSameName_KeepsAStableOrderAndSelectsTheFirst(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var user = UserId.New();
        await using var harness = await DashboardTestHarness.CreateAsync(provider, [], ct);
        var first = await harness.CreatePlanAsync(user, "Same plan", ct);
        var second = await harness.CreatePlanAsync(user, "Same plan", ct);

        var initial = await harness.HandleAsync(user, new GetDashboardContextQuery(), ct);
        var repeated = await harness.HandleAsync(user, new GetDashboardContextQuery(), ct);

        Assert.True(initial.IsOk);
        Assert.True(repeated.IsOk);
        Assert.Equal(
            new[] { first, second }.OrderBy(id => id.Value),
            initial.Data.Plans.Select(plan => plan.Id).OrderBy(id => id.Value));
        Assert.Equal(initial.Data.Plans, repeated.Data.Plans);
        Assert.Equal(initial.Data.Plans[0], initial.Data.Plan);
        Assert.Equal(repeated.Data.Plans[0], repeated.Data.Plan);
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task HandleAsync_ReadsThePlansWithASingleQueryWithoutJoins(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var user = UserId.New();
        await using var harness = await DashboardTestHarness.CreateAsync(provider, [new StubPlanActivityContributor()], ct);
        await harness.CreatePlanAsync(user, "Alpha plan", ct);
        var bravo = await harness.CreatePlanAsync(user, "Bravo plan", ct);

        harness.SqlCommands.Clear();
        var byDefault = await harness.HandleAsync(user, new GetDashboardContextQuery(), ct);
        var defaultCommands = harness.SqlCommands.Commands;

        harness.SqlCommands.Clear();
        var chosen = await harness.HandleAsync(user, new GetDashboardContextQuery(bravo), ct);
        var chosenCommands = harness.SqlCommands.Commands;

        Assert.True(byDefault.IsOk);
        Assert.True(chosen.IsOk);
        foreach (var command in new[] { Assert.Single(defaultCommands), Assert.Single(chosenCommands) })
        {
            Assert.Contains("StudyPlans", command, StringComparison.Ordinal);
            Assert.DoesNotContain("JOIN", command, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Sections", command, StringComparison.Ordinal);
            Assert.DoesNotContain("SubjectPlacements", command, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task HandleAsync_WhenUnauthenticated_ReturnsForbiddenWithoutReadingPlans(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var activity = new StubPlanActivityContributor();
        await using var harness = await DashboardTestHarness.CreateAsync(provider, [activity], ct);
        await harness.CreatePlanAsync(UserId.New(), "Alpha plan", ct);

        var result = await harness.HandleAnonymouslyAsync(new GetDashboardContextQuery(), ct);

        Assert.Equal(ResultStatus.Forbidden, result.Status);
        Assert.Equal("auth.unauthenticated", Assert.Single(result.Messages).Code);
        Assert.Equal(0, activity.Calls);
    }

    private sealed class StubPlanActivityContributor : IPlanActivityContributor
    {
        private readonly List<PlanActivity> _activities = [];

        public int Calls { get; private set; }

        public void Report(StudyPlanId planId, DateTime lastActivityAtUtc) =>
            _activities.Add(new PlanActivity(planId, lastActivityAtUtc));

        public Task<IReadOnlyList<PlanActivity>> GetLatestActivityAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<PlanActivity>>([.. _activities]);
        }
    }
}
