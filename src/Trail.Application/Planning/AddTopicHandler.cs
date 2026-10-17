using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Planning;

public sealed record AddTopicCommand(
    SubjectId SubjectId,
    string? Title,
    string? Code = null,
    TopicId? ParentId = null,
    TopicStatus InitialStatus = TopicStatus.ToStudy,
    int? ExpectedSubjectVersion = null);

public sealed class AddTopicHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IPlanningStore _store;

    public AddTopicHandler(ICurrentUser currentUser, IPlanningStore store)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(store);

        _currentUser = currentUser;
        _store = store;
    }

    public async Task<Result<TopicId>> HandleAsync(
        AddTopicCommand command,
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

        if (command.ExpectedSubjectVersion.HasValue && !subject.IsAt(command.ExpectedSubjectVersion.Value))
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

        var addResult = subject.AddTopic(command.Title, topicCode, command.ParentId, command.InitialStatus);
        if (!addResult.IsOk)
        {
            return addResult.Failure;
        }

        var saved = await _store.TrySaveChangesAsync(cancellationToken);
        if (!saved)
        {
            return Result.Conflict(Message.Error("planning.concurrency_conflict"));
        }

        return Result.Ok(addResult.Data.Id);
    }
}
