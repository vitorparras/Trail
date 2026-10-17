using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Planning;

public sealed record MoveTopicCommand(
    SubjectId SubjectId,
    TopicId TopicId,
    TopicId? NewParentId,
    int ExpectedSubjectVersion);

public sealed class MoveTopicHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IPlanningStore _store;

    public MoveTopicHandler(ICurrentUser currentUser, IPlanningStore store)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(store);

        _currentUser = currentUser;
        _store = store;
    }

    public async Task<Result> HandleAsync(
        MoveTopicCommand command,
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

        if (!subject.IsAt(command.ExpectedSubjectVersion))
        {
            return Result.Conflict(Message.Error("planning.concurrency_conflict"));
        }

        var moveResult = subject.MoveTopic(command.TopicId, command.NewParentId);
        if (!moveResult.IsOk)
        {
            return moveResult;
        }

        var saved = await _store.TrySaveChangesAsync(cancellationToken);
        if (!saved)
        {
            return Result.Conflict(Message.Error("planning.concurrency_conflict"));
        }

        return Result.Ok();
    }
}
