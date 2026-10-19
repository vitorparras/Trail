using Trail.Domain.Planning;

namespace Trail.Application.Abstractions;

public sealed record PlanActivity(StudyPlanId PlanId, DateTime LastActivityAtUtc);
