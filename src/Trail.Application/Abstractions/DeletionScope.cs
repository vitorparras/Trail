using Trail.Domain.Planning;

namespace Trail.Application.Abstractions;

public sealed record DeletionScope(
    DeletionTargetKind Kind,
    StudyPlanId PlanId,
    IReadOnlyList<SubjectId> SubjectIds,
    IReadOnlyList<TopicId> TopicIds);
