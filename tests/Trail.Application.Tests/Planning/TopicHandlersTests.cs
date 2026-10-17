using Trail.Application.Planning;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Tests.Planning;

public sealed class TopicHandlersTests
{
    private readonly TestCurrentUser _currentUser = new();
    private readonly FakePlanningStore _store = new();

    [Fact]
    public async Task AddTopic_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new AddTopicHandler(_currentUser, _store);
        var command = new AddTopicCommand(SubjectId.New(), "Title");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task AddTopic_SubjectNotFound_ReturnsNotFound()
    {
        var handler = new AddTopicHandler(_currentUser, _store);
        var command = new AddTopicCommand(SubjectId.New(), "Title");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task AddTopic_ConcurrencyMismatch_ReturnsConflict()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        _store.Subjects[subject.Id] = subject;
        var handler = new AddTopicHandler(_currentUser, _store);
        var command = new AddTopicCommand(subject.Id, "Title", ExpectedSubjectVersion: subject.Version + 1);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task AddTopic_InvalidCode_ReturnsInvalid()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        _store.Subjects[subject.Id] = subject;
        var handler = new AddTopicHandler(_currentUser, _store);
        var command = new AddTopicCommand(subject.Id, "Title", Code: "invalid..code");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task AddTopic_DuplicateCode_ReturnsConflict()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        subject.AddTopic("First", TopicCode.Create("1.1").Data);
        _store.Subjects[subject.Id] = subject;
        var handler = new AddTopicHandler(_currentUser, _store);
        var command = new AddTopicCommand(subject.Id, "Second", Code: "1.1");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task AddTopic_ParentNotFound_ReturnsNotFound()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        _store.Subjects[subject.Id] = subject;
        var handler = new AddTopicHandler(_currentUser, _store);
        var command = new AddTopicCommand(subject.Id, "Child", ParentId: TopicId.New());

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task AddTopic_StoreConflict_ReturnsConflict()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        _store.Subjects[subject.Id] = subject;
        _store.SaveChangesResult = false;
        var handler = new AddTopicHandler(_currentUser, _store);
        var command = new AddTopicCommand(subject.Id, "Topic 1");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task AddTopic_Valid_AddsTopicAndReturnsId()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        _store.Subjects[subject.Id] = subject;
        var handler = new AddTopicHandler(_currentUser, _store);
        var command = new AddTopicCommand(
            subject.Id,
            "Crimes against Administration",
            Code: "1.2.3",
            InitialStatus: TopicStatus.Studying,
            ExpectedSubjectVersion: subject.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var topic = Assert.Single(subject.Topics);
        Assert.Equal(result.Data, topic.Id);
        Assert.Equal("Crimes against Administration", topic.Title);
        Assert.Equal("1.2.3", topic.Code?.Value);
        Assert.Equal(TopicStatus.Studying, topic.Status);
    }

    [Fact]
    public async Task UpdateTopic_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new UpdateTopicHandler(_currentUser, _store);
        var command = new UpdateTopicCommand(SubjectId.New(), TopicId.New(), "Updated", null, 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task UpdateTopic_SubjectNotFound_ReturnsNotFound()
    {
        var handler = new UpdateTopicHandler(_currentUser, _store);
        var command = new UpdateTopicCommand(SubjectId.New(), TopicId.New(), "Updated", null, 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task UpdateTopic_ConcurrencyMismatch_ReturnsConflict()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var topic = subject.AddTopic("Topic").Data;
        _store.Subjects[subject.Id] = subject;
        var handler = new UpdateTopicHandler(_currentUser, _store);
        var command = new UpdateTopicCommand(subject.Id, topic.Id, "Updated", null, subject.Version + 1);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task UpdateTopic_TopicNotFound_ReturnsNotFound()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        _store.Subjects[subject.Id] = subject;
        var handler = new UpdateTopicHandler(_currentUser, _store);
        var command = new UpdateTopicCommand(subject.Id, TopicId.New(), "Updated", null, subject.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task UpdateTopic_StoreConflict_ReturnsConflict()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var topic = subject.AddTopic("Topic").Data;
        _store.Subjects[subject.Id] = subject;
        _store.SaveChangesResult = false;
        var handler = new UpdateTopicHandler(_currentUser, _store);
        var command = new UpdateTopicCommand(subject.Id, topic.Id, "Updated", null, subject.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task UpdateTopic_Valid_UpdatesTopic()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var topic = subject.AddTopic("Old Title").Data;
        _store.Subjects[subject.Id] = subject;
        var handler = new UpdateTopicHandler(_currentUser, _store);
        var command = new UpdateTopicCommand(subject.Id, topic.Id, "New Title", "2.1", subject.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Equal("New Title", topic.Title);
        Assert.Equal("2.1", topic.Code?.Value);
    }

    [Fact]
    public async Task UpdateTopic_InvalidCode_ReturnsInvalid()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var topic = subject.AddTopic("Topic").Data;
        _store.Subjects[subject.Id] = subject;
        var handler = new UpdateTopicHandler(_currentUser, _store);
        var command = new UpdateTopicCommand(subject.Id, topic.Id, "Updated", "invalid..code", subject.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task MoveTopic_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new MoveTopicHandler(_currentUser, _store);
        var command = new MoveTopicCommand(SubjectId.New(), TopicId.New(), null, 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task MoveTopic_SubjectNotFound_ReturnsNotFound()
    {
        var handler = new MoveTopicHandler(_currentUser, _store);
        var command = new MoveTopicCommand(SubjectId.New(), TopicId.New(), null, 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task MoveTopic_ConcurrencyMismatch_ReturnsConflict()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var topic = subject.AddTopic("Topic").Data;
        _store.Subjects[subject.Id] = subject;
        var handler = new MoveTopicHandler(_currentUser, _store);
        var command = new MoveTopicCommand(subject.Id, topic.Id, null, subject.Version + 1);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task MoveTopic_CycleDetected_ReturnsInvalid()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var root = subject.AddTopic("Root").Data;
        var child = subject.AddTopic("Child", parentId: root.Id).Data;
        _store.Subjects[subject.Id] = subject;

        var handler = new MoveTopicHandler(_currentUser, _store);
        var command = new MoveTopicCommand(subject.Id, root.Id, child.Id, subject.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task MoveTopic_StoreConflict_ReturnsConflict()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var topic = subject.AddTopic("Topic").Data;
        _store.Subjects[subject.Id] = subject;
        _store.SaveChangesResult = false;

        var handler = new MoveTopicHandler(_currentUser, _store);
        var command = new MoveTopicCommand(subject.Id, topic.Id, null, subject.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task MoveTopic_Valid_MovesTopic()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var root1 = subject.AddTopic("Root 1").Data;
        var root2 = subject.AddTopic("Root 2").Data;
        var child = subject.AddTopic("Child", parentId: root1.Id).Data;
        _store.Subjects[subject.Id] = subject;

        var handler = new MoveTopicHandler(_currentUser, _store);
        var command = new MoveTopicCommand(subject.Id, child.Id, root2.Id, subject.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Equal(root2.Id, child.ParentId);
    }

    [Fact]
    public async Task ReorderTopics_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new ReorderTopicsHandler(_currentUser, _store);
        var command = new ReorderTopicsCommand(SubjectId.New(), null, [], 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task ReorderTopics_SubjectNotFound_ReturnsNotFound()
    {
        var handler = new ReorderTopicsHandler(_currentUser, _store);
        var command = new ReorderTopicsCommand(SubjectId.New(), null, [], 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ReorderTopics_ConcurrencyMismatch_ReturnsConflict()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        _store.Subjects[subject.Id] = subject;
        var handler = new ReorderTopicsHandler(_currentUser, _store);
        var command = new ReorderTopicsCommand(subject.Id, null, [], subject.Version + 1);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task ReorderTopics_InvalidOrder_ReturnsInvalid()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var t1 = subject.AddTopic("T1").Data;
        subject.AddTopic("T2");
        _store.Subjects[subject.Id] = subject;

        var handler = new ReorderTopicsHandler(_currentUser, _store);
        var command = new ReorderTopicsCommand(subject.Id, null, [t1.Id], subject.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task ReorderTopics_StoreConflict_ReturnsConflict()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var t1 = subject.AddTopic("T1").Data;
        var t2 = subject.AddTopic("T2").Data;
        _store.Subjects[subject.Id] = subject;
        _store.SaveChangesResult = false;

        var handler = new ReorderTopicsHandler(_currentUser, _store);
        var command = new ReorderTopicsCommand(subject.Id, null, [t2.Id, t1.Id], subject.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task ReorderTopics_Valid_UpdatesOrder()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var t1 = subject.AddTopic("T1").Data;
        var t2 = subject.AddTopic("T2").Data;
        _store.Subjects[subject.Id] = subject;

        var handler = new ReorderTopicsHandler(_currentUser, _store);
        var command = new ReorderTopicsCommand(subject.Id, null, [t2.Id, t1.Id], subject.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var topics = subject.Topics;
        Assert.Equal(t2.Id, topics[0].Id);
        Assert.Equal(t1.Id, topics[1].Id);
    }

    [Fact]
    public async Task ChangeTopicStatus_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new ChangeTopicStatusHandler(_currentUser, _store);
        var command = new ChangeTopicStatusCommand(SubjectId.New(), TopicId.New(), TopicStatus.Studied);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task ChangeTopicStatus_SubjectNotFound_ReturnsNotFound()
    {
        var handler = new ChangeTopicStatusHandler(_currentUser, _store);
        var command = new ChangeTopicStatusCommand(SubjectId.New(), TopicId.New(), TopicStatus.Studied);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ChangeTopicStatus_TopicNotFound_ReturnsNotFound()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        _store.Subjects[subject.Id] = subject;

        var handler = new ChangeTopicStatusHandler(_currentUser, _store);
        var command = new ChangeTopicStatusCommand(subject.Id, TopicId.New(), TopicStatus.Studied);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ChangeTopicStatus_FirstAttemptSucceeds_UpdatesStatus()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var topic = subject.AddTopic("Topic", initialStatus: TopicStatus.ToStudy).Data;
        _store.Subjects[subject.Id] = subject;

        var handler = new ChangeTopicStatusHandler(_currentUser, _store);
        var command = new ChangeTopicStatusCommand(subject.Id, topic.Id, TopicStatus.Studied);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Equal(TopicStatus.Studied, topic.Status);
        Assert.Equal(1, _store.SaveChangesCalls);
        Assert.Equal(0, _store.ReloadCalls);
    }

    [Fact]
    public async Task ChangeTopicStatus_ConcurrentConflict_RetriesAndSucceeds()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var topic = subject.AddTopic("Topic", initialStatus: TopicStatus.ToStudy).Data;
        _store.Subjects[subject.Id] = subject;

        _store.SaveChangesQueue.Enqueue(false);
        _store.SaveChangesQueue.Enqueue(true);

        var handler = new ChangeTopicStatusHandler(_currentUser, _store);
        var command = new ChangeTopicStatusCommand(subject.Id, topic.Id, TopicStatus.Studied);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Equal(2, _store.SaveChangesCalls);
        Assert.Equal(1, _store.ReloadCalls);
        Assert.Equal(TopicStatus.Studied, topic.Status);
    }

    [Fact]
    public async Task ChangeTopicStatus_AllRetriesFail_ReturnsConflict()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var topic = subject.AddTopic("Topic", initialStatus: TopicStatus.ToStudy).Data;
        _store.Subjects[subject.Id] = subject;
        _store.SaveChangesResult = false;

        var handler = new ChangeTopicStatusHandler(_currentUser, _store);
        var command = new ChangeTopicStatusCommand(subject.Id, topic.Id, TopicStatus.Studied);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("planning.concurrency_conflict", Assert.Single(result.Messages).Code);
        Assert.Equal(4, _store.SaveChangesCalls);
        Assert.Equal(3, _store.ReloadCalls);
    }

    [Fact]
    public async Task ChangeTopicStatus_ReloadReturnsNull_ReturnsNotFound()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var topic = subject.AddTopic("Topic", initialStatus: TopicStatus.ToStudy).Data;
        _store.Subjects[subject.Id] = subject;
        _store.SaveChangesResult = false;
        _store.ReloadOverride = _ => null;

        var handler = new ChangeTopicStatusHandler(_currentUser, _store);
        var command = new ChangeTopicStatusCommand(subject.Id, topic.Id, TopicStatus.Studied);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("planning.subject.not_found", Assert.Single(result.Messages).Code);
    }

    [Fact]
    public async Task ChangeTopicStatus_RetryChangeTopicFails_ReturnsFailure()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var topic = subject.AddTopic("Topic", initialStatus: TopicStatus.ToStudy).Data;
        _store.Subjects[subject.Id] = subject;
        _store.SaveChangesResult = false;

        var emptySubject = Subject.Create(_currentUser.Id, subject.StudyPlanId, "Reloaded Subject").Data;
        _store.ReloadOverride = _ => emptySubject;

        var handler = new ChangeTopicStatusHandler(_currentUser, _store);
        var command = new ChangeTopicStatusCommand(subject.Id, topic.Id, TopicStatus.Studied);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task DeleteTopic_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new DeleteTopicHandler(_currentUser, _store);
        var command = new DeleteTopicCommand(SubjectId.New(), TopicId.New());

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task DeleteTopic_SubjectNotFound_ReturnsNotFound()
    {
        var handler = new DeleteTopicHandler(_currentUser, _store);
        var command = new DeleteTopicCommand(SubjectId.New(), TopicId.New());

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task DeleteTopic_TopicNotFound_ReturnsNotFound()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        _store.Subjects[subject.Id] = subject;

        var handler = new DeleteTopicHandler(_currentUser, _store);
        var command = new DeleteTopicCommand(subject.Id, TopicId.New());

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task DeleteTopic_StoreConflict_ReturnsConflict()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var topic = subject.AddTopic("Topic").Data;
        _store.Subjects[subject.Id] = subject;
        _store.SaveChangesResult = false;

        var handler = new DeleteTopicHandler(_currentUser, _store);
        var command = new DeleteTopicCommand(subject.Id, topic.Id);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task DeleteTopic_Valid_RemovesTopicSubtreeAndNotes()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var root = subject.AddTopic("Root").Data;
        var child = subject.AddTopic("Child", parentId: root.Id).Data;
        _store.Subjects[subject.Id] = subject;

        var note1 = TopicNote.Create(_currentUser.Id, root.Id, "Note 1").Data;
        var note2 = TopicNote.Create(_currentUser.Id, child.Id, "Note 2").Data;
        _store.TopicNotes[note1.Id] = note1;
        _store.TopicNotes[note2.Id] = note2;

        var handler = new DeleteTopicHandler(_currentUser, _store);
        var command = new DeleteTopicCommand(subject.Id, root.Id);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(subject.Topics);
        Assert.Empty(_store.TopicNotes);
    }
}
