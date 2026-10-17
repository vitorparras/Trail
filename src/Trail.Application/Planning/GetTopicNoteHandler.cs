using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Planning;

public sealed record GetTopicNoteQuery(
    SubjectId SubjectId,
    TopicId TopicId);

public sealed class GetTopicNoteHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IPlanningStore _store;

    public GetTopicNoteHandler(ICurrentUser currentUser, IPlanningStore store)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(store);

        _currentUser = currentUser;
        _store = store;
    }

    public async Task<Result<TopicNoteView>> HandleAsync(
        GetTopicNoteQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_currentUser.IsAuthenticated)
        {
            return Result.Forbidden(Message.Error("auth.unauthenticated"));
        }

        var subject = await _store.GetSubjectByIdAsync(query.SubjectId, cancellationToken);
        if (subject is null)
        {
            return Result.NotFound(Message.Error("planning.subject.not_found"));
        }

        var topic = subject.Topics.FirstOrDefault(t => t.Id == query.TopicId);
        if (topic is null)
        {
            return Result.NotFound(Message.Error("planning.topic.not_found"));
        }

        var note = await _store.GetTopicNoteByTopicIdAsync(query.TopicId, cancellationToken);
        if (note is null)
        {
            return Result.Ok(new TopicNoteView(
                topic.Id,
                subject.Id,
                subject.StudyPlanId,
                topic.Title,
                topic.Code?.Value,
                null,
                string.Empty,
                0));
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
}
