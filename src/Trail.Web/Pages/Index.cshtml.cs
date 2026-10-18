using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Application.Abstractions;
using Trail.Application.Dashboard;
using Trail.Domain.Common;
using Trail.Domain.Planning;
using Trail.Web.Dashboard;
using Trail.Web.Htmx;
using Trail.Web.Results;

namespace Trail.Web.Pages;

[AllowAnonymous]
public sealed class IndexModel : PageModel
{
    public const string DashboardPartialName = "_Dashboard";

    private const string UseCase = "dashboard.get_context";

    private readonly ICurrentUser _currentUser;
    private readonly GetDashboardContextHandler _getDashboardContextHandler;
    private readonly IReadOnlyList<IDashboardBlock> _blocks;

    public IndexModel(
        ICurrentUser currentUser,
        GetDashboardContextHandler getDashboardContextHandler,
        IEnumerable<IDashboardBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(getDashboardContextHandler);
        ArgumentNullException.ThrowIfNull(blocks);

        _currentUser = currentUser;
        _getDashboardContextHandler = getDashboardContextHandler;
        _blocks = [.. blocks
            .OrderBy(block => block.Order)
            .ThenBy(block => block.GetType().FullName, StringComparer.Ordinal)];
    }

    public DashboardViewModel? Dashboard { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = DashboardRoute.PlanParameter)] string? plan,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Request.IsHtmxPartialRequest() ? Challenge() : Page();
        }

        var result = await GetContextAsync(plan, cancellationToken);
        if (result.IsOk)
        {
            Dashboard = new DashboardViewModel(result.Data, _blocks);
        }

        return this.Respond(result, UseCase, options =>
        {
            options.PartialViewName = DashboardPartialName;
            options.PartialModelFactory = context => new DashboardViewModel(context, _blocks);
        });
    }

    private Task<Result<DashboardContext>> GetContextAsync(string? plan, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(plan))
        {
            return _getDashboardContextHandler.HandleAsync(new GetDashboardContextQuery(), cancellationToken);
        }

        return Guid.TryParse(plan, out var planId)
            ? _getDashboardContextHandler.HandleAsync(new GetDashboardContextQuery(StudyPlanId.From(planId)), cancellationToken)
            : Task.FromResult<Result<DashboardContext>>(Result.NotFound(Message.Error("planning.plan.not_found")));
    }
}
