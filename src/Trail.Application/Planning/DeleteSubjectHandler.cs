using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Planning;

public sealed record DeleteSubjectCommand(
    StudyPlanId PlanId,
    SubjectId SubjectId);

public sealed class DeleteSubjectHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IPlanningStore _store;

    public DeleteSubjectHandler(ICurrentUser currentUser, IPlanningStore store)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(store);

        _currentUser = currentUser;
        _store = store;
    }

    public async Task<Result> HandleAsync(
        DeleteSubjectCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_currentUser.IsAuthenticated)
        {
            return Result.Forbidden(Message.Error("auth.unauthenticated"));
        }

        var plan = await _store.GetStudyPlanByIdAsync(command.PlanId, cancellationToken);
        if (plan is null)
        {
            return Result.NotFound(Message.Error("planning.plan.not_found"));
        }

        var subject = await _store.GetSubjectByIdAsync(command.SubjectId, cancellationToken);
        if (subject is null || subject.StudyPlanId != command.PlanId)
        {
            return Result.NotFound(Message.Error("planning.subject.not_found"));
        }

        var removePlacementResult = plan.RemoveSubjectPlacement(command.SubjectId);
        if (!removePlacementResult.IsOk)
        {
            return removePlacementResult;
        }

        var topicIds = subject.Topics.Select(t => t.Id).ToList();
        await _store.RemoveTopicNotesByTopicIdsAsync(topicIds, cancellationToken);
        await _store.RemoveSubjectAsync(subject, cancellationToken);

        var saved = await _store.TrySaveChangesAsync(cancellationToken);
        if (!saved)
        {
            return Result.Conflict(Message.Error("planning.concurrency_conflict"));
        }

        return Result.Ok();
    }
}
