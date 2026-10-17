using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Planning;

public sealed record CreateStudyPlanCommand(
    string? Name,
    string? Description = null,
    ScoringRule ScoringRule = ScoringRule.Cebraspe,
    PerformancePeriodKind PeriodKind = PerformancePeriodKind.All,
    int? PeriodDays = null,
    DateOnly? PeriodStartDate = null,
    DateOnly? PeriodEndDate = null,
    int LowBandThreshold = PerformanceBands.DefaultLowThreshold,
    int HighBandThreshold = PerformanceBands.DefaultHighThreshold,
    DateOnly? ExamDate = null,
    int DefaultMockExamQuestions = StudyPlan.DefaultMockExamQuestionsCount,
    bool FitnessTestEnabled = false);

public sealed class CreateStudyPlanHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IPlanningStore _store;

    public CreateStudyPlanHandler(ICurrentUser currentUser, IPlanningStore store)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(store);

        _currentUser = currentUser;
        _store = store;
    }

    public async Task<Result<StudyPlanId>> HandleAsync(
        CreateStudyPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_currentUser.IsAuthenticated)
        {
            return Result.Forbidden(Message.Error("auth.unauthenticated"));
        }

        var bandsResult = PerformanceBands.Create(command.LowBandThreshold, command.HighBandThreshold, command.ScoringRule);
        if (!bandsResult.IsOk)
        {
            return bandsResult.Failure;
        }

        var periodResult = PerformancePeriod.Create(command.PeriodKind, command.PeriodDays, command.PeriodStartDate, command.PeriodEndDate);
        if (!periodResult.IsOk)
        {
            return periodResult.Failure;
        }

        var planResult = StudyPlan.Create(
            _currentUser.Id,
            command.Name,
            command.Description,
            command.ScoringRule,
            periodResult.Data,
            bandsResult.Data,
            command.ExamDate,
            command.DefaultMockExamQuestions,
            command.FitnessTestEnabled);

        if (!planResult.IsOk)
        {
            return planResult.Failure;
        }

        var plan = planResult.Data;
        await _store.AddStudyPlanAsync(plan, cancellationToken);

        var saved = await _store.TrySaveChangesAsync(cancellationToken);
        if (!saved)
        {
            return Result.Conflict(Message.Error("planning.concurrency_conflict"));
        }

        return Result.Ok(plan.Id);
    }
}
