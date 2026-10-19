using Microsoft.EntityFrameworkCore;
using Trail.Application.Abstractions;
using Trail.Application.Planning;
using Trail.Infrastructure.Persistence;
using Trail.Web.Planning;

namespace Trail.Web.Tests.Performance;

internal sealed class PerformancePlanLinkContributor : IPlanLinkContributor
{
    public int Order => 1;

    public PlanLink? GetLink(StudyPlanDetailView plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new PlanLink(
            "Performance link",
            "/Plans/Details",
            new Dictionary<string, string> { ["planId"] = plan.Id.Value.ToString() },
            PlanLinkIcon.ClipboardCheck);
    }
}

internal sealed class PerformanceDeletionImpactContributor(AppDbContext dbContext) : IDeletionImpactContributor
{
    private const string Code = "planning.deletion.notes";

    public async Task<IReadOnlyList<DeletionImpactItem>> GetImpactAsync(
        DeletionScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var topicIds = scope.TopicIds;
        var count = await dbContext.TopicNotes
            .AsNoTracking()
            .CountAsync(note => topicIds.Contains(note.TopicId), cancellationToken);

        return [DeletionImpactItem.Create(Code, count)];
    }
}
