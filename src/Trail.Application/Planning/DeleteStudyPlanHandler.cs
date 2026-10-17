using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Planning;

public sealed record DeleteStudyPlanCommand(StudyPlanId PlanId);

public sealed class DeleteStudyPlanHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IPlanningStore _store;

    public DeleteStudyPlanHandler(ICurrentUser currentUser, IPlanningStore store)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(store);

        _currentUser = currentUser;
        _store = store;
    }

    public async Task<Result> HandleAsync(
        DeleteStudyPlanCommand command,
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

        var subjects = await _store.ListSubjectsByPlanIdAsync(command.PlanId, cancellationToken);
        var topicIds = subjects.SelectMany(s => s.Topics.Select(t => t.Id)).ToList();

        await _store.RemoveTopicNotesByTopicIdsAsync(topicIds, cancellationToken);

        foreach (var subject in subjects)
        {
            await _store.RemoveSubjectAsync(subject, cancellationToken);
        }

        await _store.RemoveStudyPlanAsync(plan, cancellationToken);

        var saved = await _store.TrySaveChangesAsync(cancellationToken);
        if (!saved)
        {
            return Result.Conflict(Message.Error("planning.concurrency_conflict"));
        }

        return Result.Ok();
    }
}
