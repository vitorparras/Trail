namespace Trail.Domain.Common;

public abstract class AggregateRoot<TId> : IAggregateRoot
    where TId : struct, ITypedId<TId>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot()
    {
    }

    protected AggregateRoot(TId id, UserId ownerId)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("The aggregate identifier cannot be empty.", nameof(id));
        }

        if (ownerId.Value == Guid.Empty)
        {
            throw new ArgumentException("The aggregate owner cannot be empty.", nameof(ownerId));
        }

        Id = id;
        OwnerId = ownerId;
    }

    public TId Id { get; private init; }

    public UserId OwnerId { get; private init; }

    public int Version { get; private set; }

    public bool IsAt(int expectedVersion) => Version == expectedVersion;

    public IReadOnlyList<IDomainEvent> PullDomainEvents()
    {
        IReadOnlyList<IDomainEvent> domainEvents = [.. _domainEvents];
        _domainEvents.Clear();
        return domainEvents;
    }

    protected void Raise(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }
}
