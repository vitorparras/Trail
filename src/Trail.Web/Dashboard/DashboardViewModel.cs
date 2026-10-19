using Trail.Application.Dashboard;

namespace Trail.Web.Dashboard;

public sealed record DashboardViewModel(DashboardContext Context, IReadOnlyList<IDashboardBlock> Blocks);
