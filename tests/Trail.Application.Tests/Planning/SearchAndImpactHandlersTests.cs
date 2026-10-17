using Trail.Application.Planning;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Tests.Planning;

public sealed class SearchAndImpactHandlersTests
{
    private readonly TestCurrentUser _currentUser = new();
    private readonly FakePlanningStore _store = new();

    [Fact]
    public async Task GetDeletionImpact_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new GetDeletionImpactHandler(_currentUser, _store);
        var query = new GetDeletionImpactQuery(DeletionTargetKind.StudyPlan, StudyPlanId.New());

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task GetDeletionImpact_InvalidTargetKind_ReturnsInvalid()
    {
        var handler = new GetDeletionImpactHandler(_currentUser, _store);
        var query = new GetDeletionImpactQuery((DeletionTargetKind)99, StudyPlanId.New());

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task GetDeletionImpact_PlanNotFound_ReturnsNotFound()
    {
        var handler = new GetDeletionImpactHandler(_currentUser, _store);
        var query = new GetDeletionImpactQuery(DeletionTargetKind.StudyPlan, StudyPlanId.New());

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetDeletionImpact_PlanTarget_ReturnsExactCounts()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        plan.AddSection("Sec 1");
        plan.AddSection("Sec 2");
        _store.Plans[plan.Id] = plan;

        var sub1 = Subject.Create(_currentUser.Id, plan.Id, "Sub 1").Data;
        var t1 = sub1.AddTopic("T1").Data;
        var t2 = sub1.AddTopic("T2").Data;

        var sub2 = Subject.Create(_currentUser.Id, plan.Id, "Sub 2").Data;
        var t3 = sub2.AddTopic("T3").Data;

        var n1 = TopicNote.Create(_currentUser.Id, t1.Id, "N1").Data;
        var n2 = TopicNote.Create(_currentUser.Id, t3.Id, "N2").Data;

        _store.Subjects[sub1.Id] = sub1;
        _store.Subjects[sub2.Id] = sub2;
        _store.TopicNotes[n1.Id] = n1;
        _store.TopicNotes[n2.Id] = n2;

        var handler = new GetDeletionImpactHandler(_currentUser, _store);
        var query = new GetDeletionImpactQuery(DeletionTargetKind.StudyPlan, plan.Id);

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var impact = result.Data;
        Assert.Equal(DeletionTargetKind.StudyPlan, impact.TargetKind);
        Assert.Equal(plan.Id.Value, impact.TargetId);
        Assert.Equal("Plan", impact.TargetName);
        Assert.Equal(2, impact.SectionCount);
        Assert.Equal(2, impact.SubjectCount);
        Assert.Equal(3, impact.TopicCount);
        Assert.Equal(2, impact.NoteCount);
    }

    [Fact]
    public async Task GetDeletionImpact_Section_MissingSectionId_ReturnsInvalid()
    {
        var handler = new GetDeletionImpactHandler(_currentUser, _store);
        var query = new GetDeletionImpactQuery(DeletionTargetKind.Section, StudyPlanId.New());

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task GetDeletionImpact_Section_PlanNotFound_ReturnsNotFound()
    {
        var handler = new GetDeletionImpactHandler(_currentUser, _store);
        var query = new GetDeletionImpactQuery(DeletionTargetKind.Section, StudyPlanId.New(), SectionId: SectionId.New());

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetDeletionImpact_Section_SectionNotFound_ReturnsNotFound()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        _store.Plans[plan.Id] = plan;

        var handler = new GetDeletionImpactHandler(_currentUser, _store);
        var query = new GetDeletionImpactQuery(DeletionTargetKind.Section, plan.Id, SectionId: SectionId.New());

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetDeletionImpact_Section_ReturnsSectionOnlyImpact()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var sec = plan.AddSection("Module 1").Data;
        _store.Plans[plan.Id] = plan;

        var handler = new GetDeletionImpactHandler(_currentUser, _store);
        var query = new GetDeletionImpactQuery(DeletionTargetKind.Section, plan.Id, SectionId: sec.Id);

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var impact = result.Data;
        Assert.Equal(DeletionTargetKind.Section, impact.TargetKind);
        Assert.Equal(sec.Id.Value, impact.TargetId);
        Assert.Equal("Module 1", impact.TargetName);
        Assert.Equal(1, impact.SectionCount);
        Assert.Equal(0, impact.SubjectCount);
        Assert.Equal(0, impact.TopicCount);
        Assert.Equal(0, impact.NoteCount);
    }

    [Fact]
    public async Task GetDeletionImpact_Subject_MissingSubjectId_ReturnsInvalid()
    {
        var handler = new GetDeletionImpactHandler(_currentUser, _store);
        var query = new GetDeletionImpactQuery(DeletionTargetKind.Subject, StudyPlanId.New());

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task GetDeletionImpact_Subject_NotFound_ReturnsNotFound()
    {
        var handler = new GetDeletionImpactHandler(_currentUser, _store);
        var query = new GetDeletionImpactQuery(DeletionTargetKind.Subject, StudyPlanId.New(), SubjectId: SubjectId.New());

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetDeletionImpact_Subject_ReturnsSubjectSubtreeAndNotesImpact()
    {
        var planId = StudyPlanId.New();
        var subject = Subject.Create(_currentUser.Id, planId, "Civil Law").Data;
        var t1 = subject.AddTopic("T1").Data;
        var t2 = subject.AddTopic("T2").Data;
        var note = TopicNote.Create(_currentUser.Id, t1.Id, "Note").Data;

        _store.Subjects[subject.Id] = subject;
        _store.TopicNotes[note.Id] = note;

        var handler = new GetDeletionImpactHandler(_currentUser, _store);
        var query = new GetDeletionImpactQuery(DeletionTargetKind.Subject, planId, SubjectId: subject.Id);

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var impact = result.Data;
        Assert.Equal(DeletionTargetKind.Subject, impact.TargetKind);
        Assert.Equal(subject.Id.Value, impact.TargetId);
        Assert.Equal("Civil Law", impact.TargetName);
        Assert.Equal(0, impact.SectionCount);
        Assert.Equal(1, impact.SubjectCount);
        Assert.Equal(2, impact.TopicCount);
        Assert.Equal(1, impact.NoteCount);
    }

    [Fact]
    public async Task GetDeletionImpact_Topic_MissingIds_ReturnsInvalid()
    {
        var handler = new GetDeletionImpactHandler(_currentUser, _store);
        var query = new GetDeletionImpactQuery(DeletionTargetKind.Topic, StudyPlanId.New());

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task GetDeletionImpact_Topic_NotFound_ReturnsNotFound()
    {
        var planId = StudyPlanId.New();
        var subject = Subject.Create(_currentUser.Id, planId, "Subject").Data;
        _store.Subjects[subject.Id] = subject;

        var handler = new GetDeletionImpactHandler(_currentUser, _store);
        var query = new GetDeletionImpactQuery(DeletionTargetKind.Topic, planId, SubjectId: subject.Id, TopicId: TopicId.New());

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetDeletionImpact_Topic_SubjectInDifferentPlan_ReturnsNotFound()
    {
        var planId = StudyPlanId.New();
        var otherPlanId = StudyPlanId.New();
        var subject = Subject.Create(_currentUser.Id, otherPlanId, "Subject").Data;
        var topic = subject.AddTopic("Topic").Data;
        _store.Subjects[subject.Id] = subject;

        var handler = new GetDeletionImpactHandler(_currentUser, _store);
        var query = new GetDeletionImpactQuery(DeletionTargetKind.Topic, planId, SubjectId: subject.Id, TopicId: topic.Id);

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetDeletionImpact_Topic_ReturnsSubtreeImpact()
    {
        var planId = StudyPlanId.New();
        var subject = Subject.Create(_currentUser.Id, planId, "Subject").Data;
        var root = subject.AddTopic("Root").Data;
        var child1 = subject.AddTopic("Child 1", parentId: root.Id).Data;
        var child2 = subject.AddTopic("Child 2", parentId: child1.Id).Data;
        subject.AddTopic("Unrelated Root");

        var n1 = TopicNote.Create(_currentUser.Id, root.Id, "N1").Data;
        var n2 = TopicNote.Create(_currentUser.Id, child2.Id, "N2").Data;

        _store.Subjects[subject.Id] = subject;
        _store.TopicNotes[n1.Id] = n1;
        _store.TopicNotes[n2.Id] = n2;

        var handler = new GetDeletionImpactHandler(_currentUser, _store);
        var query = new GetDeletionImpactQuery(DeletionTargetKind.Topic, planId, SubjectId: subject.Id, TopicId: root.Id);

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var impact = result.Data;
        Assert.Equal(DeletionTargetKind.Topic, impact.TargetKind);
        Assert.Equal(root.Id.Value, impact.TargetId);
        Assert.Equal("Root", impact.TargetName);
        Assert.Equal(0, impact.SectionCount);
        Assert.Equal(0, impact.SubjectCount);
        Assert.Equal(3, impact.TopicCount);
        Assert.Equal(2, impact.NoteCount);
    }

    [Fact]
    public async Task SearchPlanTopics_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new SearchPlanTopicsHandler(_currentUser, _store);
        var query = new SearchPlanTopicsQuery(StudyPlanId.New());

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task SearchPlanTopics_PlanNotFound_ReturnsNotFound()
    {
        var handler = new SearchPlanTopicsHandler(_currentUser, _store);
        var query = new SearchPlanTopicsQuery(StudyPlanId.New());

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task SearchPlanTopics_Valid_ReturnsTopicsWithCorrectHierarchyAndFiltering()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var sub1 = Subject.Create(_currentUser.Id, plan.Id, "Admin Law").Data;
        var t1 = sub1.AddTopic("Organização Administrativa", TopicCode.Create("1").Data, initialStatus: TopicStatus.ToStudy).Data;
        var t1_1 = sub1.AddTopic("Administração Indireta", TopicCode.Create("1.1").Data, parentId: t1.Id, initialStatus: TopicStatus.Studied).Data;
        var t1_2 = sub1.AddTopic("Autarquias", TopicCode.Create("1.2").Data, parentId: t1.Id, initialStatus: TopicStatus.Studying).Data;

        var sub2 = Subject.Create(_currentUser.Id, plan.Id, "Penal Law").Data;
        var t2 = sub2.AddTopic("Teoria do Crime", TopicCode.Create("1").Data, initialStatus: TopicStatus.Reviewing).Data;

        plan.PlaceSubject(sub1.Id);
        plan.PlaceSubject(sub2.Id);

        var note = TopicNote.Create(_currentUser.Id, t1_1.Id, "Note").Data;

        _store.Plans[plan.Id] = plan;
        _store.Subjects[sub1.Id] = sub1;
        _store.Subjects[sub2.Id] = sub2;
        _store.TopicNotes[note.Id] = note;

        var handler = new SearchPlanTopicsHandler(_currentUser, _store);

        var allResult = await handler.HandleAsync(new SearchPlanTopicsQuery(plan.Id), TestContext.Current.CancellationToken);
        Assert.True(allResult.IsOk);
        Assert.Equal(4, allResult.Data.Count);
        Assert.Equal(0, allResult.Data[0].Depth);
        Assert.Equal(1, allResult.Data[1].Depth);
        Assert.True(allResult.Data[1].HasNote);

        var subjectFiltered = await handler.HandleAsync(new SearchPlanTopicsQuery(plan.Id, SubjectId: sub2.Id), TestContext.Current.CancellationToken);
        Assert.True(subjectFiltered.IsOk);
        var sub2Item = Assert.Single(subjectFiltered.Data);
        Assert.Equal(t2.Id, sub2Item.Id);

        var statusFiltered = await handler.HandleAsync(new SearchPlanTopicsQuery(plan.Id, Status: TopicStatus.Studied), TestContext.Current.CancellationToken);
        Assert.True(statusFiltered.IsOk);
        var studiedItem = Assert.Single(statusFiltered.Data);
        Assert.Equal(t1_1.Id, studiedItem.Id);

        var accentSearch = await handler.HandleAsync(new SearchPlanTopicsQuery(plan.Id, SearchQuery: "organizacao"), TestContext.Current.CancellationToken);
        Assert.True(accentSearch.IsOk);
        var matched = Assert.Single(accentSearch.Data);
        Assert.Equal(t1.Id, matched.Id);

        var uppercaseSearch = await handler.HandleAsync(new SearchPlanTopicsQuery(plan.Id, SearchQuery: "AUTARQUIAS"), TestContext.Current.CancellationToken);
        Assert.True(uppercaseSearch.IsOk);
        var autoMatched = Assert.Single(uppercaseSearch.Data);
        Assert.Equal(t1_2.Id, autoMatched.Id);

        var codeSearch = await handler.HandleAsync(new SearchPlanTopicsQuery(plan.Id, SearchQuery: "1.1"), TestContext.Current.CancellationToken);
        Assert.True(codeSearch.IsOk);
        var codeMatched = Assert.Single(codeSearch.Data);
        Assert.Equal(t1_1.Id, codeMatched.Id);
    }
}
