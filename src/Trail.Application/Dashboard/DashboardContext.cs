using System.Diagnostics.CodeAnalysis;
using Trail.Domain.Planning;

namespace Trail.Application.Dashboard;

public sealed record DashboardPlan(StudyPlanId Id, string Name);

public sealed record DashboardContext(IReadOnlyList<DashboardPlan> Plans, DashboardPlan? Plan)
{
    [MemberNotNullWhen(true, nameof(Plan))]
    public bool HasPlan => Plan is not null;
}
