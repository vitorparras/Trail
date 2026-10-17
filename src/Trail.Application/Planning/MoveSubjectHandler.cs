using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Planning;

public sealed record MoveSubjectCommand(
    StudyPlanId PlanId,
    SubjectId SubjectId,
    SectionId? SectionId,
    int ExpectedPlanVersion);

public sealed class MoveSubjectHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IPlanningStore _store;

    public MoveSubjectHandler(ICurrentUser currentUser, IPlanningStore store)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(store);

        _currentUser = currentUser;
        _store = store;
    }

    public async Task<Result> HandleAsync(
        MoveSubjectCommand command,
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

        if (!plan.IsAt(command.ExpectedPlanVersion))
        {
            return Result.Conflict(Message.Error("planning.concurrency_conflict"));
        }

        var moveResult = plan.MoveSubjectToSection(command.SubjectId, command.SectionId);
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
