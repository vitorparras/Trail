namespace Trail.Domain.Common;

public interface IAggregateRoot
{
    UserId OwnerId { get; }

    int Version { get; }

    IReadOnlyList<IDomainEvent> PullDomainEvents();
}
