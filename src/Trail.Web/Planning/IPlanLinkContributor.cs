using Trail.Application.Planning;

namespace Trail.Web.Planning;

public interface IPlanLinkContributor
{
    int Order { get; }

    PlanLink? GetLink(StudyPlanDetailView plan);
}
