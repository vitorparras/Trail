using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Application.Planning;
using Trail.Domain.Common;
using Trail.Domain.Planning;
using Trail.Web.Results;

namespace Trail.Web.Pages.Plans;

public sealed class EditModel : PageModel
{
    private readonly GetStudyPlanHandler _getStudyPlanHandler;
    private readonly UpdateStudyPlanHandler _updateStudyPlanHandler;

    public EditModel(
        GetStudyPlanHandler getStudyPlanHandler,
        UpdateStudyPlanHandler updateStudyPlanHandler)
    {
        ArgumentNullException.ThrowIfNull(getStudyPlanHandler);
        ArgumentNullException.ThrowIfNull(updateStudyPlanHandler);

        _getStudyPlanHandler = getStudyPlanHandler;
        _updateStudyPlanHandler = updateStudyPlanHandler;
    }

    [BindProperty]
    public EditPlanInput Input { get; set; } = new();

    public Guid PlanId { get; private set; }

    public bool HasConcurrencyConflict { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        PlanId = planId;
        var result = await _getStudyPlanHandler.HandleAsync(new GetStudyPlanQuery(StudyPlanId.From(planId)), cancellationToken);
        if (!result.IsOk)
        {
            return this.Respond(result, "planning.get_plan");
        }

        var plan = result.Data;
        Input = new EditPlanInput
        {
            ExpectedVersion = plan.Version,
            Name = plan.Name,
            Description = plan.Description,
            ScoringRule = plan.ScoringRule,
            PeriodKind = plan.DefaultPerformancePeriod.Kind,
            PeriodDays = plan.DefaultPerformancePeriod.Days,
            PeriodStartDate = plan.DefaultPerformancePeriod.StartDate,
            PeriodEndDate = plan.DefaultPerformancePeriod.EndDate,
            LowBandThreshold = plan.PerformanceBands.LowThreshold,
            HighBandThreshold = plan.PerformanceBands.HighThreshold,
            ExamDate = plan.ExamDate,
            DefaultMockExamQuestions = plan.DefaultMockExamQuestions,
            FitnessTestEnabled = plan.FitnessTestEnabled,
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        PlanId = planId;
        var command = new UpdateStudyPlanCommand(
            StudyPlanId.From(planId),
            Input.ExpectedVersion,
            Input.Name,
            Input.Description,
            Input.ScoringRule,
            Input.PeriodKind,
            Input.PeriodDays,
            Input.PeriodStartDate,
            Input.PeriodEndDate,
            Input.LowBandThreshold,
            Input.HighBandThreshold,
            Input.ExamDate,
            Input.DefaultMockExamQuestions,
            Input.FitnessTestEnabled);

        var result = await _updateStudyPlanHandler.HandleAsync(command, cancellationToken);
        if (result.Status == ResultStatus.Conflict)
        {
            var currentPlanResult = await _getStudyPlanHandler.HandleAsync(new GetStudyPlanQuery(StudyPlanId.From(planId)), cancellationToken);
            if (currentPlanResult.IsOk)
            {
                Input.ExpectedVersion = currentPlanResult.Data.Version;
            }

            HasConcurrencyConflict = true;
            Response.StatusCode = StatusCodes.Status409Conflict;
            return Page();
        }

        if (!result.IsOk)
        {
            return this.Respond(result, "planning.update_plan");
        }

        return RedirectToPage("/Plans/Details", new { planId });
    }

    public sealed class EditPlanInput
    {
        public int ExpectedVersion { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public ScoringRule ScoringRule { get; set; } = ScoringRule.Cebraspe;

        public PerformancePeriodKind PeriodKind { get; set; } = PerformancePeriodKind.All;

        public int? PeriodDays { get; set; }

        public DateOnly? PeriodStartDate { get; set; }

        public DateOnly? PeriodEndDate { get; set; }

        public int LowBandThreshold { get; set; } = PerformanceBands.DefaultLowThreshold;

        public int HighBandThreshold { get; set; } = PerformanceBands.DefaultHighThreshold;

        public DateOnly? ExamDate { get; set; }

        public int DefaultMockExamQuestions { get; set; } = StudyPlan.DefaultMockExamQuestionsCount;

        public bool FitnessTestEnabled { get; set; }
    }
}
