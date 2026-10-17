
using Trail.Domain.Common;

namespace Trail.Domain.Planning;

public sealed record StudyPlanCreated(StudyPlanId StudyPlanId, UserId OwnerId, StudyPlanOrigin Origin) : IDomainEvent;
