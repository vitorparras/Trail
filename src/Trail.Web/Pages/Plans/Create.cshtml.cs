using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Application.Abstractions;
using Trail.Application.Planning;
using Trail.Domain.Common;
using Trail.Domain.Planning;
using Trail.Web.Results;

namespace Trail.Web.Pages.Plans;

public sealed class CreateModel : PageModel
{
    private readonly CreateStudyPlanHandler _createStudyPlanHandler;
    private readonly ICurrentUser _currentUser;

    public CreateModel(CreateStudyPlanHandler createStudyPlanHandler, ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(createStudyPlanHandler);
        ArgumentNullException.ThrowIfNull(currentUser);
        _createStudyPlanHandler = createStudyPlanHandler;
        _currentUser = currentUser;
    }

    [BindProperty]
    public CreatePlanInput Input { get; set; } = new();

    public IActionResult OnGet()
    {
        if (!_currentUser.IsAuthenticated)
        {
            return this.Respond(Result.Forbidden(Message.Error("auth.unauthenticated")), "planning.create_plan");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        var command = new CreateStudyPlanCommand(
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

        var result = await _createStudyPlanHandler.HandleAsync(command, cancellationToken);
        if (!result.IsOk)
        {
            return this.Respond(result, "planning.create_plan", options => options.Fields = PlanFormFields.Plan);
        }

        return RedirectToPage("/Plans/Details", new { planId = result.Data.Value });
    }

    public sealed class CreatePlanInput
    {
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
