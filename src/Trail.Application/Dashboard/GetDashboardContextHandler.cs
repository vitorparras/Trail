using Trail.Application.Abstractions;
using Trail.Application.Planning;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Dashboard;

public sealed record GetDashboardContextQuery(StudyPlanId? PlanId = null);

public sealed class GetDashboardContextHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IPlanningStore _planningStore;
    private readonly IReadOnlyList<IPlanActivityContributor> _activityContributors;

    public GetDashboardContextHandler(
        ICurrentUser currentUser,
        IPlanningStore planningStore,
        IEnumerable<IPlanActivityContributor> activityContributors)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(planningStore);
        ArgumentNullException.ThrowIfNull(activityContributors);

        _currentUser = currentUser;
        _planningStore = planningStore;
        _activityContributors = [.. activityContributors];
    }

    public async Task<Result<DashboardContext>> HandleAsync(
        GetDashboardContextQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_currentUser.IsAuthenticated)
        {
            return Result.Forbidden(Message.Error("auth.unauthenticated"));
        }

        var options = await _planningStore.ListStudyPlanOptionsAsync(cancellationToken);
        var plans = options.Select(option => new DashboardPlan(option.Id, option.Name)).ToList();

        if (query.PlanId is { } planId)
        {
            var chosen = plans.Find(plan => plan.Id == planId);
            return chosen is null
                ? Result.NotFound(Message.Error("planning.plan.not_found"))
                : Result.Ok(new DashboardContext(plans, chosen));
        }

        if (plans.Count == 0)
        {
            return Result.Ok(new DashboardContext(plans, null));
        }

        var mostRecentlyActive = await FindMostRecentlyActiveAsync(plans, cancellationToken);
        return Result.Ok(new DashboardContext(plans, mostRecentlyActive ?? plans[0]));
    }

    private async Task<DashboardPlan?> FindMostRecentlyActiveAsync(
        List<DashboardPlan> plans,
        CancellationToken cancellationToken)
    {
        var positions = new Dictionary<StudyPlanId, int>(plans.Count);
        for (var position = 0; position < plans.Count; position++)
        {
            positions[plans[position].Id] = position;
        }

        int? latestPosition = null;
        var latestActivityAtUtc = DateTime.MinValue;

        foreach (var contributor in _activityContributors)
        {
            var activities = await contributor.GetLatestActivityAsync(cancellationToken);
            foreach (var activity in activities)
            {
                if (!positions.TryGetValue(activity.PlanId, out var position))
                {
                    continue;
                }

                var isMoreRecent = activity.LastActivityAtUtc > latestActivityAtUtc;
                var isTieEarlierInList = activity.LastActivityAtUtc == latestActivityAtUtc && position < latestPosition;
                if (latestPosition is null || isMoreRecent || isTieEarlierInList)
                {
                    latestPosition = position;
                    latestActivityAtUtc = activity.LastActivityAtUtc;
                }
            }
        }

        return latestPosition is { } found ? plans[found] : null;
    }
}
