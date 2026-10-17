using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Application.Planning;
using Trail.Web.Results;

namespace Trail.Web.Pages.Plans;

public sealed class IndexModel : PageModel
{
    private readonly ListStudyPlansHandler _listStudyPlansHandler;

    public IndexModel(ListStudyPlansHandler listStudyPlansHandler)
    {
        ArgumentNullException.ThrowIfNull(listStudyPlansHandler);
        _listStudyPlansHandler = listStudyPlansHandler;
    }

    public IReadOnlyList<StudyPlanSummaryView> StudyPlans { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        var result = await _listStudyPlansHandler.HandleAsync(new ListStudyPlansQuery(), cancellationToken);
        if (!result.IsOk)
        {
            return this.Respond(result, "planning.list_plans");
        }

        StudyPlans = result.Data;
        return Page();
    }
}
