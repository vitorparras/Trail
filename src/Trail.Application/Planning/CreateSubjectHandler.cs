using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Planning;

public sealed record CreateSubjectCommand(
    StudyPlanId PlanId,
    string? Name,
    SectionId? SectionId = null,
    int? ExpectedPlanVersion = null);

public sealed class CreateSubjectHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IPlanningStore _store;

    public CreateSubjectHandler(ICurrentUser currentUser, IPlanningStore store)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(store);

        _currentUser = currentUser;
        _store = store;
    }

    public async Task<Result<SubjectId>> HandleAsync(
        CreateSubjectCommand command,
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

        if (command.ExpectedPlanVersion.HasValue && !plan.IsAt(command.ExpectedPlanVersion.Value))
        {
            return Result.Conflict(Message.Error("planning.concurrency_conflict"));
        }

        var subjectResult = Subject.Create(_currentUser.Id, plan.Id, command.Name);
        if (!subjectResult.IsOk)
        {
            return subjectResult.Failure;
        }

        var subject = subjectResult.Data;
        var placeResult = plan.PlaceSubject(subject.Id, command.SectionId);
        if (!placeResult.IsOk)
        {
            return placeResult.Failure;
        }

        await _store.AddSubjectAsync(subject, cancellationToken);

        var saved = await _store.TrySaveChangesAsync(cancellationToken);
        if (!saved)
        {
            return Result.Conflict(Message.Error("planning.concurrency_conflict"));
        }

        return Result.Ok(subject.Id);
    }
}
