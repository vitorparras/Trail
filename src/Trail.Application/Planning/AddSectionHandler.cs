using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Planning;

public sealed record AddSectionCommand(
    StudyPlanId PlanId,
    string? Name,
    int ExpectedPlanVersion);

public sealed class AddSectionHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IPlanningStore _store;

    public AddSectionHandler(ICurrentUser currentUser, IPlanningStore store)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(store);

        _currentUser = currentUser;
        _store = store;
    }

    public async Task<Result<SectionId>> HandleAsync(
        AddSectionCommand command,
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

        var addResult = plan.AddSection(command.Name);
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
