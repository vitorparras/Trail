using Trail.Application.Planning;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Tests.Planning;

public sealed class SubjectHandlersTests
{
    private readonly TestCurrentUser _currentUser = new();
    private readonly FakePlanningStore _store = new();

    [Fact]
    public async Task CreateSubject_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new CreateSubjectHandler(_currentUser, _store);
        var command = new CreateSubjectCommand(StudyPlanId.New(), "Math");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task CreateSubject_PlanNotFound_ReturnsNotFound()
    {
        var handler = new CreateSubjectHandler(_currentUser, _store);
        var command = new CreateSubjectCommand(StudyPlanId.New(), "Math");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task CreateSubject_ConcurrencyMismatch_ReturnsConflict()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new CreateSubjectHandler(_currentUser, _store);
        var command = new CreateSubjectCommand(plan.Id, "Math", ExpectedPlanVersion: plan.Version + 1);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task CreateSubject_InvalidName_ReturnsInvalid()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new CreateSubjectHandler(_currentUser, _store);
        var command = new CreateSubjectCommand(plan.Id, "   ");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task CreateSubject_SectionNotFound_ReturnsNotFound()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new CreateSubjectHandler(_currentUser, _store);
        var command = new CreateSubjectCommand(plan.Id, "Math", SectionId: SectionId.New());

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task CreateSubject_StoreConflict_ReturnsConflict()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        _store.Plans[plan.Id] = plan;
        _store.SaveChangesResult = false;
        var handler = new CreateSubjectHandler(_currentUser, _store);
        var command = new CreateSubjectCommand(plan.Id, "Math");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task CreateSubject_Valid_CreatesSubjectAndPlacesInPlan()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var sec = plan.AddSection("Basic").Data;
        _store.Plans[plan.Id] = plan;

        var handler = new CreateSubjectHandler(_currentUser, _store);
        var command = new CreateSubjectCommand(plan.Id, "Portuguese", sec.Id, plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var subject = Assert.Single(_store.Subjects.Values);
        Assert.Equal(result.Data, subject.Id);
        Assert.Equal("Portuguese", subject.Name);

        var placement = Assert.Single(plan.Outline);
        Assert.Equal(subject.Id, placement.SubjectId);
        Assert.Equal(sec.Id, placement.SectionId);
    }

    [Fact]
    public async Task UpdateSubject_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new UpdateSubjectHandler(_currentUser, _store);
        var command = new UpdateSubjectCommand(SubjectId.New(), "Renamed", 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task UpdateSubject_NotFound_ReturnsNotFound()
    {
        var handler = new UpdateSubjectHandler(_currentUser, _store);
        var command = new UpdateSubjectCommand(SubjectId.New(), "Renamed", 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task UpdateSubject_ConcurrencyMismatch_ReturnsConflict()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Math").Data;
        _store.Subjects[subject.Id] = subject;
        var handler = new UpdateSubjectHandler(_currentUser, _store);
        var command = new UpdateSubjectCommand(subject.Id, "Renamed", subject.Version + 1);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task UpdateSubject_InvalidName_ReturnsInvalid()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Math").Data;
        _store.Subjects[subject.Id] = subject;
        var handler = new UpdateSubjectHandler(_currentUser, _store);
        var command = new UpdateSubjectCommand(subject.Id, "   ", subject.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task UpdateSubject_StoreConflict_ReturnsConflict()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Math").Data;
        _store.Subjects[subject.Id] = subject;
        _store.SaveChangesResult = false;
        var handler = new UpdateSubjectHandler(_currentUser, _store);
        var command = new UpdateSubjectCommand(subject.Id, "Renamed", subject.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task UpdateSubject_Valid_RenamesSubject()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Old Name").Data;
        _store.Subjects[subject.Id] = subject;
        var handler = new UpdateSubjectHandler(_currentUser, _store);
        var command = new UpdateSubjectCommand(subject.Id, "New Name", subject.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Equal("New Name", subject.Name);
    }

    [Fact]
    public async Task MoveSubject_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new MoveSubjectHandler(_currentUser, _store);
        var command = new MoveSubjectCommand(StudyPlanId.New(), SubjectId.New(), null, 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task MoveSubject_PlanNotFound_ReturnsNotFound()
    {
        var handler = new MoveSubjectHandler(_currentUser, _store);
        var command = new MoveSubjectCommand(StudyPlanId.New(), SubjectId.New(), null, 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task MoveSubject_ConcurrencyMismatch_ReturnsConflict()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new MoveSubjectHandler(_currentUser, _store);
        var command = new MoveSubjectCommand(plan.Id, SubjectId.New(), null, plan.Version + 1);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task MoveSubject_SubjectNotPlaced_ReturnsNotFound()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new MoveSubjectHandler(_currentUser, _store);
        var command = new MoveSubjectCommand(plan.Id, SubjectId.New(), null, plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task MoveSubject_SectionNotFound_ReturnsNotFound()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var subId = SubjectId.New();
        plan.PlaceSubject(subId);
        _store.Plans[plan.Id] = plan;

        var handler = new MoveSubjectHandler(_currentUser, _store);
        var command = new MoveSubjectCommand(plan.Id, subId, SectionId.New(), plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task MoveSubject_StoreConflict_ReturnsConflict()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var subId = SubjectId.New();
        plan.PlaceSubject(subId);
        _store.Plans[plan.Id] = plan;
        _store.SaveChangesResult = false;

        var handler = new MoveSubjectHandler(_currentUser, _store);
        var command = new MoveSubjectCommand(plan.Id, subId, null, plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task MoveSubject_Valid_MovesSubjectToSection()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var sec = plan.AddSection("New Sec").Data;
        var subId = SubjectId.New();
        plan.PlaceSubject(subId);
        _store.Plans[plan.Id] = plan;

        var handler = new MoveSubjectHandler(_currentUser, _store);
        var command = new MoveSubjectCommand(plan.Id, subId, sec.Id, plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var placement = Assert.Single(plan.Outline);
        Assert.Equal(sec.Id, placement.SectionId);
    }

    [Fact]
    public async Task ReorderSubjects_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new ReorderSubjectsHandler(_currentUser, _store);
        var command = new ReorderSubjectsCommand(StudyPlanId.New(), null, [], 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task ReorderSubjects_PlanNotFound_ReturnsNotFound()
    {
        var handler = new ReorderSubjectsHandler(_currentUser, _store);
        var command = new ReorderSubjectsCommand(StudyPlanId.New(), null, [], 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ReorderSubjects_ConcurrencyMismatch_ReturnsConflict()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new ReorderSubjectsHandler(_currentUser, _store);
        var command = new ReorderSubjectsCommand(plan.Id, null, [], plan.Version + 1);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task ReorderSubjects_InvalidOrder_ReturnsInvalid()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var sub1 = SubjectId.New();
        var sub2 = SubjectId.New();
        plan.PlaceSubject(sub1);
        plan.PlaceSubject(sub2);
        _store.Plans[plan.Id] = plan;

        var handler = new ReorderSubjectsHandler(_currentUser, _store);
        var command = new ReorderSubjectsCommand(plan.Id, null, [sub1], plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task ReorderSubjects_StoreConflict_ReturnsConflict()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var sub1 = SubjectId.New();
        var sub2 = SubjectId.New();
        plan.PlaceSubject(sub1);
        plan.PlaceSubject(sub2);
        _store.Plans[plan.Id] = plan;
        _store.SaveChangesResult = false;

        var handler = new ReorderSubjectsHandler(_currentUser, _store);
        var command = new ReorderSubjectsCommand(plan.Id, null, [sub2, sub1], plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task ReorderSubjects_Valid_UpdatesOrder()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var sub1 = SubjectId.New();
        var sub2 = SubjectId.New();
        plan.PlaceSubject(sub1);
        plan.PlaceSubject(sub2);
        _store.Plans[plan.Id] = plan;

        var handler = new ReorderSubjectsHandler(_currentUser, _store);
        var command = new ReorderSubjectsCommand(plan.Id, null, [sub2, sub1], plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var outline = plan.Outline;
        Assert.Equal(sub2, outline[0].SubjectId);
        Assert.Equal(sub1, outline[1].SubjectId);
    }

    [Fact]
    public async Task DeleteSubject_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new DeleteSubjectHandler(_currentUser, _store);
        var command = new DeleteSubjectCommand(StudyPlanId.New(), SubjectId.New());

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task DeleteSubject_PlanNotFound_ReturnsNotFound()
    {
        var handler = new DeleteSubjectHandler(_currentUser, _store);
        var command = new DeleteSubjectCommand(StudyPlanId.New(), SubjectId.New());

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task DeleteSubject_SubjectNotFound_ReturnsNotFound()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new DeleteSubjectHandler(_currentUser, _store);
        var command = new DeleteSubjectCommand(plan.Id, SubjectId.New());

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task DeleteSubject_SubjectInDifferentPlan_ReturnsNotFound()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var otherPlan = StudyPlan.Create(_currentUser.Id, "Other").Data;
        var subject = Subject.Create(_currentUser.Id, otherPlan.Id, "Subject").Data;
        _store.Plans[plan.Id] = plan;
        _store.Subjects[subject.Id] = subject;

        var handler = new DeleteSubjectHandler(_currentUser, _store);
        var command = new DeleteSubjectCommand(plan.Id, subject.Id);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task DeleteSubject_PlacementNotFound_ReturnsNotFound()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var subject = Subject.Create(_currentUser.Id, plan.Id, "Subject").Data;
        _store.Plans[plan.Id] = plan;
        _store.Subjects[subject.Id] = subject;

        var handler = new DeleteSubjectHandler(_currentUser, _store);
        var command = new DeleteSubjectCommand(plan.Id, subject.Id);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task DeleteSubject_StoreConflict_ReturnsConflict()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var subject = Subject.Create(_currentUser.Id, plan.Id, "Subject").Data;
        plan.PlaceSubject(subject.Id);
        _store.Plans[plan.Id] = plan;
        _store.Subjects[subject.Id] = subject;
        _store.SaveChangesResult = false;

        var handler = new DeleteSubjectHandler(_currentUser, _store);
        var command = new DeleteSubjectCommand(plan.Id, subject.Id);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task DeleteSubject_Valid_RemovesSubjectPlacementAndNotes()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var subject = Subject.Create(_currentUser.Id, plan.Id, "Subject").Data;
        var topic = subject.AddTopic("Topic").Data;
        plan.PlaceSubject(subject.Id);

        var note = TopicNote.Create(_currentUser.Id, topic.Id, "Notes").Data;

        _store.Plans[plan.Id] = plan;
        _store.Subjects[subject.Id] = subject;
        _store.TopicNotes[note.Id] = note;

        var handler = new DeleteSubjectHandler(_currentUser, _store);
        var command = new DeleteSubjectCommand(plan.Id, subject.Id);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(plan.Outline);
        Assert.Empty(_store.Subjects);
        Assert.Empty(_store.TopicNotes);
    }

    [Fact]
    public async Task GetSubjectTopics_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new GetSubjectTopicsHandler(_currentUser, _store);
        var query = new GetSubjectTopicsQuery(SubjectId.New());

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task GetSubjectTopics_SubjectNotFound_ReturnsNotFound()
    {
        var handler = new GetSubjectTopicsHandler(_currentUser, _store);
        var query = new GetSubjectTopicsQuery(SubjectId.New());

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetSubjectTopics_PlanNotFound_ReturnsNotFound()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        _store.Subjects[subject.Id] = subject;
        var handler = new GetSubjectTopicsHandler(_currentUser, _store);
        var query = new GetSubjectTopicsQuery(subject.Id);

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetSubjectTopics_Valid_ReturnsDetailViewWithHierarchy()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var sec = plan.AddSection("Section").Data;
        var subject = Subject.Create(_currentUser.Id, plan.Id, "Constitutional Law").Data;
        plan.PlaceSubject(subject.Id, sec.Id);

        var rootTopic = subject.AddTopic("Fundamental Rights", TopicCode.Create("1").Data).Data;
        var subTopic = subject.AddTopic("Individual Rights", TopicCode.Create("1.1").Data, parentId: rootTopic.Id).Data;

        var note = TopicNote.Create(_currentUser.Id, rootTopic.Id, "Rights note").Data;

        _store.Plans[plan.Id] = plan;
        _store.Subjects[subject.Id] = subject;
        _store.TopicNotes[note.Id] = note;

        var handler = new GetSubjectTopicsHandler(_currentUser, _store);
        var query = new GetSubjectTopicsQuery(subject.Id);

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var detail = result.Data;
        Assert.Equal(subject.Id, detail.Id);
        Assert.Equal(plan.Id, detail.StudyPlanId);
        Assert.Equal(sec.Id, detail.SectionId);
        Assert.Equal("Section", detail.SectionName);
        Assert.Equal(2, detail.Topics.Count);

        var rootItem = detail.Topics[0];
        Assert.Equal(rootTopic.Id, rootItem.Id);
        Assert.Equal("1", rootItem.Code);
        Assert.True(rootItem.HasNote);
        Assert.Equal(0, rootItem.Depth);

        var subItem = detail.Topics[1];
        Assert.Equal(subTopic.Id, subItem.Id);
        Assert.Equal("1.1", subItem.Code);
        Assert.False(subItem.HasNote);
        Assert.Equal(1, subItem.Depth);
    }
}
