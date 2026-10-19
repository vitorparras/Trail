using Microsoft.AspNetCore.Mvc;
using Trail.Application.Dashboard;
using Trail.Web.Charts;
using Trail.Web.Dashboard;

namespace Trail.Web.Tests.Dashboard;

public sealed record SampleChartBlockModel(DashboardPlan Plan, ChartModel Chart);

public sealed class SampleChartDashboardBlock : IDashboardBlock
{
    public const string ViewPath = "~/Dashboard/SampleChartDashboardBlock.cshtml";

    public int Order => 200;

    public Type ViewComponent => typeof(SampleChartDashboardBlockViewComponent);

    public static string ChartTitle(DashboardPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return $"Sample chart for {plan.Name}";
    }
}

public sealed class SampleChartDashboardBlockViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(DashboardContext dashboard)
    {
        ArgumentNullException.ThrowIfNull(dashboard);

        var plan = dashboard.Plan ?? throw new InvalidOperationException("Dashboard blocks are invoked only with a chosen plan.");
        var chart = new ChartModel(
            SampleChartDashboardBlock.ChartTitle(plan),
            "Plan",
            [.. dashboard.Plans.Select(option => option.Name)],
            [new ChartSeries("Chosen", [.. dashboard.Plans.Select(option => (decimal?)(option.Id == plan.Id ? 1 : 0))])]);

        return View(SampleChartDashboardBlock.ViewPath, new SampleChartBlockModel(plan, chart));
    }
}

public sealed class SampleHiddenDashboardBlock : IDashboardBlock
{
    public int Order => 150;

    public Type ViewComponent => typeof(SampleHiddenDashboardBlockViewComponent);
}

public sealed class SampleHiddenDashboardBlockViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(DashboardContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Content(string.Empty);
    }
}

public sealed class SampleNoteDashboardBlock : IDashboardBlock
{
    public const string Marker = "sample-note-block";

    public int Order => 100;

    public Type ViewComponent => typeof(SampleNoteDashboardBlockViewComponent);
}

public sealed class SampleNoteDashboardBlockViewComponent : ViewComponent
{
    public Task<IViewComponentResult> InvokeAsync(DashboardContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Task.FromResult<IViewComponentResult>(Content($"{SampleNoteDashboardBlock.Marker}:{context.Plan?.Id}"));
    }
}
