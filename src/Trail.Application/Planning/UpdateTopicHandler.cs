using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Planning;

public sealed record UpdateTopicCommand(
    SubjectId SubjectId,
    TopicId TopicId,
    string? Title,
    string? Code,
    int ExpectedSubjectVersion);

public sealed class UpdateTopicHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IPlanningStore _store;

    public UpdateTopicHandler(ICurrentUser currentUser, IPlanningStore store)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(store);

        _currentUser = currentUser;
        _store = store;
    }

    public async Task<Result> HandleAsync(
        UpdateTopicCommand command,
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

        TopicCode? topicCode = null;
        if (!string.IsNullOrWhiteSpace(command.Code))
        {
            var codeResult = TopicCode.Create(command.Code);
            if (!codeResult.IsOk)
            {
                return codeResult.Failure;
            }

            topicCode = codeResult.Data;
        }

        var updateResult = subject.UpdateTopic(command.TopicId, command.Title, topicCode);
        if (!updateResult.IsOk)
        {
            return updateResult;
        }

        var saved = await _store.TrySaveChangesAsync(cancellationToken);
        if (!saved)
        {
            return Result.Conflict(Message.Error("planning.concurrency_conflict"));
        }

        return Result.Ok();
    }
}
