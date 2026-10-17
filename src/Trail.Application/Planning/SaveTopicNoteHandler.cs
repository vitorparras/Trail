using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Planning;

public sealed record SaveTopicNoteCommand(
    SubjectId SubjectId,
    TopicId TopicId,
    string? Markdown,
    int ExpectedVersion);

public sealed class SaveTopicNoteHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IPlanningStore _store;

    public SaveTopicNoteHandler(ICurrentUser currentUser, IPlanningStore store)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(store);

        _currentUser = currentUser;
        _store = store;
    }

    public async Task<Result<TopicNoteView>> HandleAsync(
        SaveTopicNoteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_currentUser.IsAuthenticated)
        {
            return Result.Forbidden(Message.Error("auth.unauthenticated"));
        }

        var subject = await _store.GetSubjectByIdAsync(command.SubjectId, cancellationToken);
        if (subject is null)
        {
            return Result.NotFound(Message.Error("planning.subject.not_found"));
        }

        var topic = subject.Topics.FirstOrDefault(t => t.Id == command.TopicId);
        if (topic is null)
        {
            return Result.NotFound(Message.Error("planning.topic.not_found"));
        }

        var existing = await _store.GetTopicNoteByTopicIdAsync(command.TopicId, cancellationToken);
        if (existing is null)
        {
            if (command.ExpectedVersion != 0)
            {
                return Result.Conflict(Message.Error("planning.concurrency_conflict"));
            }

            var createResult = TopicNote.Create(_currentUser.Id, command.TopicId, command.Markdown);
            if (!createResult.IsOk)
            {
                return createResult.Failure;
            }

            var note = createResult.Data;
            await _store.AddTopicNoteAsync(note, cancellationToken);

            var saved = await _store.TrySaveChangesAsync(cancellationToken);
            if (!saved)
            {
                return Result.Conflict(Message.Error("planning.concurrency_conflict"));
            }

            return Result.Ok(new TopicNoteView(
                topic.Id,
                subject.Id,
                subject.StudyPlanId,
                topic.Title,
                topic.Code?.Value,
                note.Id,
                note.Markdown,
                note.Version));
        }

        if (!existing.IsAt(command.ExpectedVersion))
        {
            return Result.Conflict(Message.Error("planning.concurrency_conflict"));
        }

        var updateResult = existing.Update(command.Markdown);
        if (!updateResult.IsOk)
        {
            return updateResult.Failure;
        }

        var updatedSaved = await _store.TrySaveChangesAsync(cancellationToken);
        if (!updatedSaved)
        {
            return Result.Conflict(Message.Error("planning.concurrency_conflict"));
        }

        return Result.Ok(new TopicNoteView(
            topic.Id,
            subject.Id,
            subject.StudyPlanId,
            topic.Title,
            topic.Code?.Value,
            existing.Id,
            existing.Markdown,
            existing.Version));
    }
}
