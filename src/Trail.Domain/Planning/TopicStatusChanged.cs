
using Trail.Domain.Common;

namespace Trail.Domain.Planning;

public sealed record TopicStatusChanged(
    SubjectId SubjectId,
    TopicId TopicId,
    TopicStatus PreviousStatus,
    TopicStatus NewStatus) : IDomainEvent;
