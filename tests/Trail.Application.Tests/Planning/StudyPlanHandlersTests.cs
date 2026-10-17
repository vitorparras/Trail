using Trail.Application.Planning;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Tests.Planning;

public sealed class StudyPlanHandlersTests
{
    private readonly TestCurrentUser _currentUser = new();
    private readonly FakePlanningStore _store = new();

    [Fact]
    public async Task CreateStudyPlan_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new CreateStudyPlanHandler(_currentUser, _store);
        var command = new CreateStudyPlanCommand("Test Plan");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
        Assert.Equal("auth.unauthenticated", Assert.Single(result.Messages).Code);
    }

    [Fact]
    public async Task CreateStudyPlan_InvalidBands_ReturnsInvalid()
    {
        var handler = new CreateStudyPlanHandler(_currentUser, _store);
        var command = new CreateStudyPlanCommand("Test Plan", LowBandThreshold: 80, HighBandThreshold: 20);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task CreateStudyPlan_InvalidPeriod_ReturnsInvalid()
    {
        var handler = new CreateStudyPlanHandler(_currentUser, _store);
        var command = new CreateStudyPlanCommand(
            "Test Plan",
            PeriodKind: PerformancePeriodKind.LastDays,
            PeriodDays: -5);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task CreateStudyPlan_InvalidName_ReturnsInvalid()
    {
        var handler = new CreateStudyPlanHandler(_currentUser, _store);
        var command = new CreateStudyPlanCommand("   ");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task CreateStudyPlan_StoreConflict_ReturnsConflict()
    {
        _store.SaveChangesResult = false;
        var handler = new CreateStudyPlanHandler(_currentUser, _store);
        var command = new CreateStudyPlanCommand("Test Plan");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("planning.concurrency_conflict", Assert.Single(result.Messages).Code);
    }

    [Fact]
    public async Task CreateStudyPlan_ValidCommand_AddsPlanAndReturnsId()
    {
        var handler = new CreateStudyPlanHandler(_currentUser, _store);
        var command = new CreateStudyPlanCommand(
            "Delegado Federal",
            "Edital 2026",
            ScoringRule.Cebraspe,
            PerformancePeriodKind.LastDays,
            PeriodDays: 30,
            LowBandThreshold: 55,
            HighBandThreshold: 75,
            ExamDate: new DateOnly(2026, 12, 1),
            DefaultMockExamQuestions: 120,
            FitnessTestEnabled: true);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var plan = Assert.Single(_store.Plans.Values);
        Assert.Equal(result.Data, plan.Id);
        Assert.Equal("Delegado Federal", plan.Name);
        Assert.Equal("Edital 2026", plan.Description);
        Assert.Equal(ScoringRule.Cebraspe, plan.ScoringRule);
        Assert.Equal(PerformancePeriodKind.LastDays, plan.DefaultPerformancePeriod.Kind);
        Assert.Equal(30, plan.DefaultPerformancePeriod.Days);
        Assert.Equal(55, plan.PerformanceBands.LowThreshold);
        Assert.Equal(75, plan.PerformanceBands.HighThreshold);
        Assert.Equal(new DateOnly(2026, 12, 1), plan.ExamDate);
        Assert.Equal(120, plan.DefaultMockExamQuestions);
        Assert.True(plan.FitnessTestEnabled);
    }

    [Fact]
    public async Task UpdateStudyPlan_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new UpdateStudyPlanHandler(_currentUser, _store);
        var command = new UpdateStudyPlanCommand(StudyPlanId.New(), 0, "Updated");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task UpdateStudyPlan_NotFound_ReturnsNotFound()
    {
        var handler = new UpdateStudyPlanHandler(_currentUser, _store);
        var command = new UpdateStudyPlanCommand(StudyPlanId.New(), 0, "Updated");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("planning.plan.not_found", Assert.Single(result.Messages).Code);
    }

    [Fact]
    public async Task UpdateStudyPlan_ConcurrencyMismatch_ReturnsConflict()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Original").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new UpdateStudyPlanHandler(_currentUser, _store);
        var command = new UpdateStudyPlanCommand(plan.Id, plan.Version + 1, "Updated");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("planning.concurrency_conflict", Assert.Single(result.Messages).Code);
    }

    [Fact]
    public async Task UpdateStudyPlan_InvalidBands_ReturnsInvalid()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Original").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new UpdateStudyPlanHandler(_currentUser, _store);
        var command = new UpdateStudyPlanCommand(plan.Id, plan.Version, "Updated", LowBandThreshold: 90, HighBandThreshold: 30);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task UpdateStudyPlan_InvalidPeriod_ReturnsInvalid()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Original").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new UpdateStudyPlanHandler(_currentUser, _store);
        var command = new UpdateStudyPlanCommand(
            plan.Id,
            plan.Version,
            "Updated",
            PeriodKind: PerformancePeriodKind.LastDays,
            PeriodDays: -5);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task UpdateStudyPlan_InvalidName_ReturnsInvalid()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Original").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new UpdateStudyPlanHandler(_currentUser, _store);
        var command = new UpdateStudyPlanCommand(plan.Id, plan.Version, "   ");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task UpdateStudyPlan_StoreConflict_ReturnsConflict()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Original").Data;
        _store.Plans[plan.Id] = plan;
        _store.SaveChangesResult = false;
        var handler = new UpdateStudyPlanHandler(_currentUser, _store);
        var command = new UpdateStudyPlanCommand(plan.Id, plan.Version, "Updated");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task UpdateStudyPlan_Valid_UpdatesPlanSuccessfully()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Original").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new UpdateStudyPlanHandler(_currentUser, _store);
        var command = new UpdateStudyPlanCommand(
            plan.Id,
            plan.Version,
            "Updated Name",
            "Updated Desc",
            ScoringRule.Standard,
            PerformancePeriodKind.All,
            null,
            null,
            null,
            40,
            80,
            new DateOnly(2027, 1, 1),
            100,
            true);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Equal("Updated Name", plan.Name);
        Assert.Equal("Updated Desc", plan.Description);
        Assert.Equal(ScoringRule.Standard, plan.ScoringRule);
        Assert.Equal(40, plan.PerformanceBands.LowThreshold);
        Assert.Equal(80, plan.PerformanceBands.HighThreshold);
        Assert.Equal(100, plan.DefaultMockExamQuestions);
        Assert.True(plan.FitnessTestEnabled);
    }

    [Fact]
    public async Task DeleteStudyPlan_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new DeleteStudyPlanHandler(_currentUser, _store);
        var command = new DeleteStudyPlanCommand(StudyPlanId.New());

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task DeleteStudyPlan_NotFound_ReturnsNotFound()
    {
        var handler = new DeleteStudyPlanHandler(_currentUser, _store);
        var command = new DeleteStudyPlanCommand(StudyPlanId.New());

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task DeleteStudyPlan_StoreConflict_ReturnsConflict()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        _store.Plans[plan.Id] = plan;
        _store.SaveChangesResult = false;
        var handler = new DeleteStudyPlanHandler(_currentUser, _store);
        var command = new DeleteStudyPlanCommand(plan.Id);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task DeleteStudyPlan_Valid_RemovesPlanSubjectsAndNotes()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        _store.Plans[plan.Id] = plan;

        var subject = Subject.Create(_currentUser.Id, plan.Id, "Subject").Data;
        var topic = subject.AddTopic("Topic").Data;
        _store.Subjects[subject.Id] = subject;

        var note = TopicNote.Create(_currentUser.Id, topic.Id, "Note").Data;
        _store.TopicNotes[note.Id] = note;

        var handler = new DeleteStudyPlanHandler(_currentUser, _store);
        var command = new DeleteStudyPlanCommand(plan.Id);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(_store.Plans);
        Assert.Empty(_store.Subjects);
        Assert.Empty(_store.TopicNotes);
    }

    [Fact]
    public async Task ListStudyPlans_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new ListStudyPlansHandler(_currentUser, _store);

        var result = await handler.HandleAsync(new ListStudyPlansQuery(), TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task ListStudyPlans_ReturnsSummariesWithCounts()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan 1").Data;
        plan.AddSection("Section 1");
        _store.Plans[plan.Id] = plan;

        var subject = Subject.Create(_currentUser.Id, plan.Id, "Subject 1").Data;
        subject.AddTopic("Topic 1", initialStatus: TopicStatus.Studied);
        subject.AddTopic("Topic 2", initialStatus: TopicStatus.ToStudy);
        _store.Subjects[subject.Id] = subject;

        var handler = new ListStudyPlansHandler(_currentUser, _store);

        var result = await handler.HandleAsync(null, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var summary = Assert.Single(result.Data);
        Assert.Equal(plan.Id, summary.Id);
        Assert.Equal("Plan 1", summary.Name);
        Assert.Equal(1, summary.SectionCount);
        Assert.Equal(1, summary.SubjectCount);
        Assert.Equal(2, summary.TopicCount);
        Assert.Equal(1, summary.StudiedTopicCount);
    }

    [Fact]
    public async Task GetStudyPlan_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new GetStudyPlanHandler(_currentUser, _store);

        var result = await handler.HandleAsync(new GetStudyPlanQuery(StudyPlanId.New()), TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task GetStudyPlan_NotFound_ReturnsNotFound()
    {
        var handler = new GetStudyPlanHandler(_currentUser, _store);

        var result = await handler.HandleAsync(new GetStudyPlanQuery(StudyPlanId.New()), TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetStudyPlan_Valid_ReturnsDetailViewWithSectionsAndSubjects()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Main Plan").Data;
        var sec1 = plan.AddSection("Basic").Data;
        var sec2 = plan.AddSection("Specific").Data;

        var sub1 = Subject.Create(_currentUser.Id, plan.Id, "Portuguese").Data;
        sub1.AddTopic("Grammar", initialStatus: TopicStatus.Studying);
        sub1.AddTopic("Syntax", initialStatus: TopicStatus.Studied);
        plan.PlaceSubject(sub1.Id, sec1.Id);

        var sub2 = Subject.Create(_currentUser.Id, plan.Id, "Law").Data;
        sub2.AddTopic("Constitutional", initialStatus: TopicStatus.Reviewing);
        sub2.AddTopic("Admin", initialStatus: TopicStatus.ToStudy);
        plan.PlaceSubject(sub2.Id, sec2.Id);

        _store.Plans[plan.Id] = plan;
        _store.Subjects[sub1.Id] = sub1;
        _store.Subjects[sub2.Id] = sub2;

        var handler = new GetStudyPlanHandler(_currentUser, _store);

        var result = await handler.HandleAsync(new GetStudyPlanQuery(plan.Id), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var detail = result.Data;
        Assert.Equal(plan.Id, detail.Id);
        Assert.Equal(2, detail.Sections.Count);
        Assert.Equal(2, detail.Subjects.Count);
        Assert.Equal(4, detail.TotalTopicCount);
        Assert.Equal(1, detail.ToStudyCount);
        Assert.Equal(1, detail.StudyingCount);
        Assert.Equal(1, detail.StudiedCount);
        Assert.Equal(1, detail.ReviewingCount);

        var firstSubject = detail.Subjects[0];
        Assert.Equal(sub1.Id, firstSubject.SubjectId);
        Assert.Equal(sec1.Id, firstSubject.SectionId);
        Assert.Equal(2, firstSubject.TopicCount);
        Assert.Equal(1, firstSubject.StudyingCount);
        Assert.Equal(1, firstSubject.StudiedCount);
    }
}
