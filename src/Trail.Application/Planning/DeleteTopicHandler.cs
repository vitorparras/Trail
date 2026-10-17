using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Planning;

public sealed record DeleteTopicCommand(
    SubjectId SubjectId,
    TopicId TopicId);

public sealed class DeleteTopicHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IPlanningStore _store;

    public DeleteTopicHandler(ICurrentUser currentUser, IPlanningStore store)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(store);

        _currentUser = currentUser;
        _store = store;
    }

    public async Task<Result> HandleAsync(
        DeleteTopicCommand command,
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

        var removeResult = subject.RemoveTopicSubtree(command.TopicId);
        if (!removeResult.IsOk)
        {
            return removeResult.Failure;
        }

        var removedIds = removeResult.Data;
        await _store.RemoveTopicNotesByTopicIdsAsync(removedIds, cancellationToken);

        var saved = await _store.TrySaveChangesAsync(cancellationToken);
        if (!saved)
        {
            return Result.Conflict(Message.Error("planning.concurrency_conflict"));
        }

        return Result.Ok();
    }
}
