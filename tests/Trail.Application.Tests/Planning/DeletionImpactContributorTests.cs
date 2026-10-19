using Trail.Application.Abstractions;
using Trail.Application.Planning;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Tests.Planning;

public sealed class DeletionImpactContributorTests
{
    private readonly TestCurrentUser _currentUser = new();
    private readonly FakePlanningStore _store = new();

    [Fact]
    public async Task HandleAsync_WithContributors_AppendsTheirItemsAfterThePlanningItemsOrderedByCode()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        _store.Plans[plan.Id] = plan;
        var studyTime = new RecordingContributor(DeletionImpactItem.Create("studytime.sample.sessions", 4));
        var practice = new RecordingContributor(
            DeletionImpactItem.Create("practice.sample.entries", 2),
            DeletionImpactItem.Create("fitness.sample.attempts", 1));
        var handler = new GetDeletionImpactHandler(_currentUser, _store, [studyTime, practice]);

        var result = await handler.HandleAsync(new GetDeletionImpactQuery(DeletionTargetKind.StudyPlan, plan.Id), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Equal(
            [
                "planning.deletion.sections",
                "planning.deletion.subjects",
                "planning.deletion.topics",
                "planning.deletion.notes",
                "fitness.sample.attempts",
                "practice.sample.entries",
                "studytime.sample.sessions",
            ],
            result.Data.Items.Select(item => item.Code));
        Assert.Equal([0, 0, 0, 0, 1, 2, 4], result.Data.Items.Select(item => item.Count));
    }

    [Fact]
    public async Task HandleAsync_PlanTarget_PassesEverySubjectAndTopicOfThePlan()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var first = Subject.Create(_currentUser.Id, plan.Id, "First").Data;
        var root = first.AddTopic("Root").Data;
        var child = first.AddTopic("Child", parentId: root.Id).Data;
        var second = Subject.Create(_currentUser.Id, plan.Id, "Second").Data;
        var other = second.AddTopic("Other").Data;
        var foreign = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Foreign").Data;
        foreign.AddTopic("Foreign topic");
        _store.Plans[plan.Id] = plan;
        _store.Subjects[first.Id] = first;
        _store.Subjects[second.Id] = second;
        _store.Subjects[foreign.Id] = foreign;
        var contributor = new RecordingContributor();

        var result = await new GetDeletionImpactHandler(_currentUser, _store, [contributor])
            .HandleAsync(new GetDeletionImpactQuery(DeletionTargetKind.StudyPlan, plan.Id), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var scope = Assert.Single(contributor.Scopes);
        Assert.Equal(DeletionTargetKind.StudyPlan, scope.Kind);
        Assert.Equal(plan.Id, scope.PlanId);
        Assert.Equal(new[] { first.Id, second.Id }.OrderBy(id => id.Value), scope.SubjectIds.OrderBy(id => id.Value));
        Assert.Equal(new[] { root.Id, child.Id, other.Id }.OrderBy(id => id.Value), scope.TopicIds.OrderBy(id => id.Value));
    }

    [Fact]
    public async Task HandleAsync_SectionTarget_PassesEmptyLists()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var section = plan.AddSection("Section").Data;
        var subject = Subject.Create(_currentUser.Id, plan.Id, "Subject").Data;
        subject.AddTopic("Topic");
        plan.PlaceSubject(subject.Id, section.Id);
        _store.Plans[plan.Id] = plan;
        _store.Subjects[subject.Id] = subject;
        var contributor = new RecordingContributor();

        var result = await new GetDeletionImpactHandler(_currentUser, _store, [contributor])
            .HandleAsync(new GetDeletionImpactQuery(DeletionTargetKind.Section, plan.Id, SectionId: section.Id), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var scope = Assert.Single(contributor.Scopes);
        Assert.Equal(DeletionTargetKind.Section, scope.Kind);
        Assert.Equal(plan.Id, scope.PlanId);
        Assert.Empty(scope.SubjectIds);
        Assert.Empty(scope.TopicIds);
    }

    [Fact]
    public async Task HandleAsync_SubjectTarget_PassesTheSubjectAndAllOfItsTopics()
    {
        var planId = StudyPlanId.New();
        var subject = Subject.Create(_currentUser.Id, planId, "Subject").Data;
        var root = subject.AddTopic("Root").Data;
        var child = subject.AddTopic("Child", parentId: root.Id).Data;
        var sibling = subject.AddTopic("Sibling").Data;
        _store.Subjects[subject.Id] = subject;
        var contributor = new RecordingContributor();

        var result = await new GetDeletionImpactHandler(_currentUser, _store, [contributor])
            .HandleAsync(new GetDeletionImpactQuery(DeletionTargetKind.Subject, planId, SubjectId: subject.Id), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var scope = Assert.Single(contributor.Scopes);
        Assert.Equal(DeletionTargetKind.Subject, scope.Kind);
        Assert.Equal(planId, scope.PlanId);
        Assert.Equal([subject.Id], scope.SubjectIds);
        Assert.Equal(new[] { root.Id, child.Id, sibling.Id }.OrderBy(id => id.Value), scope.TopicIds.OrderBy(id => id.Value));
    }

    [Fact]
    public async Task HandleAsync_TopicTarget_PassesTheTopicWithItsSubtopicsAndNoSubject()
    {
        var planId = StudyPlanId.New();
        var subject = Subject.Create(_currentUser.Id, planId, "Subject").Data;
        var root = subject.AddTopic("Root").Data;
        var child = subject.AddTopic("Child", parentId: root.Id).Data;
        var grandchild = subject.AddTopic("Grandchild", parentId: child.Id).Data;
        subject.AddTopic("Sibling");
        _store.Subjects[subject.Id] = subject;
        var contributor = new RecordingContributor();

        var result = await new GetDeletionImpactHandler(_currentUser, _store, [contributor])
            .HandleAsync(
                new GetDeletionImpactQuery(DeletionTargetKind.Topic, planId, SubjectId: subject.Id, TopicId: root.Id),
                TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var scope = Assert.Single(contributor.Scopes);
        Assert.Equal(DeletionTargetKind.Topic, scope.Kind);
        Assert.Equal(planId, scope.PlanId);
        Assert.Empty(scope.SubjectIds);
        Assert.Equal(new[] { root.Id, child.Id, grandchild.Id }.OrderBy(id => id.Value), scope.TopicIds.OrderBy(id => id.Value));
    }

    [Theory]
    [InlineData(false, DeletionTargetKind.StudyPlan, ResultStatus.Forbidden)]
    [InlineData(true, DeletionTargetKind.StudyPlan, ResultStatus.NotFound)]
    [InlineData(true, DeletionTargetKind.Section, ResultStatus.Invalid)]
    [InlineData(true, DeletionTargetKind.Subject, ResultStatus.NotFound)]
    [InlineData(true, (DeletionTargetKind)99, ResultStatus.Invalid)]
    public async Task HandleAsync_WhenTheTargetIsNotResolved_DoesNotAskTheContributors(
        bool authenticated,
        DeletionTargetKind kind,
        ResultStatus expected)
    {
        _currentUser.IsAuthenticated = authenticated;
        var contributor = new RecordingContributor(DeletionImpactItem.Create("practice.sample.entries", 1));

        var result = await new GetDeletionImpactHandler(_currentUser, _store, [contributor])
            .HandleAsync(new GetDeletionImpactQuery(kind, StudyPlanId.New(), SubjectId: SubjectId.New()), TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Status);
        Assert.Empty(contributor.Scopes);
    }

    [Fact]
    public void Constructor_WithNullContributors_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new GetDeletionImpactHandler(_currentUser, _store, null!));
    }

    private sealed class RecordingContributor(params DeletionImpactItem[] items) : IDeletionImpactContributor
    {
        public List<DeletionScope> Scopes { get; } = [];

        public Task<IReadOnlyList<DeletionImpactItem>> GetImpactAsync(DeletionScope scope, CancellationToken cancellationToken = default)
        {
            Scopes.Add(scope);
            return Task.FromResult<IReadOnlyList<DeletionImpactItem>>(items);
        }
    }
}
