using Trail.Application.Planning;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Tests.Planning;

public sealed class TopicNoteHandlersTests
{
    private readonly TestCurrentUser _currentUser = new();
    private readonly FakePlanningStore _store = new();

    [Fact]
    public async Task GetTopicNote_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new GetTopicNoteHandler(_currentUser, _store);
        var query = new GetTopicNoteQuery(SubjectId.New(), TopicId.New());

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task GetTopicNote_SubjectNotFound_ReturnsNotFound()
    {
        var handler = new GetTopicNoteHandler(_currentUser, _store);
        var query = new GetTopicNoteQuery(SubjectId.New(), TopicId.New());

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetTopicNote_TopicNotFound_ReturnsNotFound()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        _store.Subjects[subject.Id] = subject;

        var handler = new GetTopicNoteHandler(_currentUser, _store);
        var query = new GetTopicNoteQuery(subject.Id, TopicId.New());

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetTopicNote_NoteDoesNotExist_ReturnsEmptyNoteView()
    {
        var planId = StudyPlanId.New();
        var subject = Subject.Create(_currentUser.Id, planId, "Subject").Data;
        var topic = subject.AddTopic("Topic Title", TopicCode.Create("1.1").Data).Data;
        _store.Subjects[subject.Id] = subject;

        var handler = new GetTopicNoteHandler(_currentUser, _store);
        var query = new GetTopicNoteQuery(subject.Id, topic.Id);

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var view = result.Data;
        Assert.Equal(topic.Id, view.TopicId);
        Assert.Equal(subject.Id, view.SubjectId);
        Assert.Equal(planId, view.StudyPlanId);
        Assert.Equal("Topic Title", view.TopicTitle);
        Assert.Equal("1.1", view.TopicCode);
        Assert.Null(view.NoteId);
        Assert.Equal(string.Empty, view.Markdown);
        Assert.Equal(0, view.Version);
    }

    [Fact]
    public async Task GetTopicNote_NoteExists_ReturnsPopulatedNoteView()
    {
        var planId = StudyPlanId.New();
        var subject = Subject.Create(_currentUser.Id, planId, "Subject").Data;
        var topic = subject.AddTopic("Topic Title", TopicCode.Create("1.1").Data).Data;
        var note = TopicNote.Create(_currentUser.Id, topic.Id, "# Summary of topic").Data;
        _store.Subjects[subject.Id] = subject;
        _store.TopicNotes[note.Id] = note;

        var handler = new GetTopicNoteHandler(_currentUser, _store);
        var query = new GetTopicNoteQuery(subject.Id, topic.Id);

        var result = await handler.HandleAsync(query, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var view = result.Data;
        Assert.Equal(topic.Id, view.TopicId);
        Assert.Equal(note.Id, view.NoteId);
        Assert.Equal("# Summary of topic", view.Markdown);
        Assert.Equal(note.Version, view.Version);
    }

    [Fact]
    public async Task SaveTopicNote_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new SaveTopicNoteHandler(_currentUser, _store);
        var command = new SaveTopicNoteCommand(SubjectId.New(), TopicId.New(), "text", 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task SaveTopicNote_SubjectNotFound_ReturnsNotFound()
    {
        var handler = new SaveTopicNoteHandler(_currentUser, _store);
        var command = new SaveTopicNoteCommand(SubjectId.New(), TopicId.New(), "text", 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task SaveTopicNote_TopicNotFound_ReturnsNotFound()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        _store.Subjects[subject.Id] = subject;

        var handler = new SaveTopicNoteHandler(_currentUser, _store);
        var command = new SaveTopicNoteCommand(subject.Id, TopicId.New(), "text", 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task SaveTopicNote_NewNote_ExpectedVersionNonZero_ReturnsConflict()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var topic = subject.AddTopic("Topic").Data;
        _store.Subjects[subject.Id] = subject;

        var handler = new SaveTopicNoteHandler(_currentUser, _store);
        var command = new SaveTopicNoteCommand(subject.Id, topic.Id, "text", 1);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("planning.concurrency_conflict", Assert.Single(result.Messages).Code);
    }

    [Fact]
    public async Task SaveTopicNote_NewNote_TooLong_ReturnsInvalid()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var topic = subject.AddTopic("Topic").Data;
        _store.Subjects[subject.Id] = subject;

        var handler = new SaveTopicNoteHandler(_currentUser, _store);
        var longMarkdown = new string('x', TopicNote.MaxMarkdownLength + 1);
        var command = new SaveTopicNoteCommand(subject.Id, topic.Id, longMarkdown, 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task SaveTopicNote_NewNote_StoreConflict_ReturnsConflict()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var topic = subject.AddTopic("Topic").Data;
        _store.Subjects[subject.Id] = subject;
        _store.SaveChangesResult = false;

        var handler = new SaveTopicNoteHandler(_currentUser, _store);
        var command = new SaveTopicNoteCommand(subject.Id, topic.Id, "Note", 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task SaveTopicNote_NewNote_Valid_CreatesNoteAndReturnsView()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var topic = subject.AddTopic("Topic").Data;
        _store.Subjects[subject.Id] = subject;

        var handler = new SaveTopicNoteHandler(_currentUser, _store);
        var command = new SaveTopicNoteCommand(subject.Id, topic.Id, "# Important notes", 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var view = result.Data;
        Assert.NotNull(view.NoteId);
        Assert.Equal("# Important notes", view.Markdown);
        var stored = Assert.Single(_store.TopicNotes.Values);
        Assert.Equal(view.NoteId, stored.Id);
    }

    [Fact]
    public async Task SaveTopicNote_ExistingNote_VersionMismatch_ReturnsConflict()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var topic = subject.AddTopic("Topic").Data;
        var note = TopicNote.Create(_currentUser.Id, topic.Id, "Original").Data;
        _store.Subjects[subject.Id] = subject;
        _store.TopicNotes[note.Id] = note;

        var handler = new SaveTopicNoteHandler(_currentUser, _store);
        var command = new SaveTopicNoteCommand(subject.Id, topic.Id, "Updated", note.Version + 1);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("planning.concurrency_conflict", Assert.Single(result.Messages).Code);
    }

    [Fact]
    public async Task SaveTopicNote_ExistingNote_TooLong_ReturnsInvalid()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var topic = subject.AddTopic("Topic").Data;
        var note = TopicNote.Create(_currentUser.Id, topic.Id, "Original").Data;
        _store.Subjects[subject.Id] = subject;
        _store.TopicNotes[note.Id] = note;

        var handler = new SaveTopicNoteHandler(_currentUser, _store);
        var longMarkdown = new string('x', TopicNote.MaxMarkdownLength + 1);
        var command = new SaveTopicNoteCommand(subject.Id, topic.Id, longMarkdown, note.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task SaveTopicNote_ExistingNote_StoreConflict_ReturnsConflict()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var topic = subject.AddTopic("Topic").Data;
        var note = TopicNote.Create(_currentUser.Id, topic.Id, "Original").Data;
        _store.Subjects[subject.Id] = subject;
        _store.TopicNotes[note.Id] = note;
        _store.SaveChangesResult = false;

        var handler = new SaveTopicNoteHandler(_currentUser, _store);
        var command = new SaveTopicNoteCommand(subject.Id, topic.Id, "Updated", note.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task SaveTopicNote_ExistingNote_Valid_UpdatesNoteAndReturnsView()
    {
        var subject = Subject.Create(_currentUser.Id, StudyPlanId.New(), "Subject").Data;
        var topic = subject.AddTopic("Topic").Data;
        var note = TopicNote.Create(_currentUser.Id, topic.Id, "Original").Data;
        _store.Subjects[subject.Id] = subject;
        _store.TopicNotes[note.Id] = note;

        var handler = new SaveTopicNoteHandler(_currentUser, _store);
        var command = new SaveTopicNoteCommand(subject.Id, topic.Id, "# Updated Markdown", note.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var view = result.Data;
        Assert.Equal(note.Id, view.NoteId);
        Assert.Equal("# Updated Markdown", view.Markdown);
        Assert.Equal("# Updated Markdown", note.Markdown);
    }
}
