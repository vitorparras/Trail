using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Planning;

public sealed record ChangeTopicStatusCommand(
    SubjectId SubjectId,
    TopicId TopicId,
    TopicStatus NewStatus);

public sealed class ChangeTopicStatusHandler
{
    private const int MaxConcurrencyRetries = 3;

    private readonly ICurrentUser _currentUser;
    private readonly IPlanningStore _store;

    public ChangeTopicStatusHandler(ICurrentUser currentUser, IPlanningStore store)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(store);

        _currentUser = currentUser;
        _store = store;
    }

    public async Task<Result> HandleAsync(
        ChangeTopicStatusCommand command,
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

        var changeResult = subject.ChangeTopicStatus(command.TopicId, command.NewStatus);
        if (!changeResult.IsOk)
        {
            return changeResult;
        }

        var saved = await _store.TrySaveChangesAsync(cancellationToken);
        if (saved)
        {
            return Result.Ok();
        }

        for (var attempt = 0; attempt < MaxConcurrencyRetries; attempt++)
        {
            var reloaded = await _store.ReloadSubjectByIdAsync(command.SubjectId, cancellationToken);
            if (reloaded is null)
            {
                return Result.NotFound(Message.Error("planning.subject.not_found"));
            }

            var retryChangeResult = reloaded.ChangeTopicStatus(command.TopicId, command.NewStatus);
            if (!retryChangeResult.IsOk)
            {
                return retryChangeResult;
            }

            if (await _store.TrySaveChangesAsync(cancellationToken))
            {
                return Result.Ok();
            }
        }

        return Result.Conflict(Message.Error("planning.concurrency_conflict"));
    }
}
