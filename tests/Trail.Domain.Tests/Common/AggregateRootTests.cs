using Trail.Domain.Common;

namespace Trail.Domain.Tests.Common;

public sealed class AggregateRootTests
{
    [Fact]
    public void Constructor_WithIdAndOwner_SetsIdentityWithoutEventsAtVersionZero()
    {
        var id = SampleId.New();
        var ownerId = UserId.New();

        var aggregate = new SampleAggregate(id, ownerId);

        Assert.Equal(id, aggregate.Id);
        Assert.Equal(ownerId, aggregate.OwnerId);
        Assert.Equal(0, aggregate.Version);
        Assert.Empty(aggregate.PullDomainEvents());
    }

    [Fact]
    public void Constructor_WithEmptyId_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SampleAggregate(default, UserId.New()));

        Assert.Equal("id", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithEmptyOwner_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SampleAggregate(SampleId.New(), default));

        Assert.Equal("ownerId", exception.ParamName);
    }

    [Fact]
    public void PullDomainEvents_AfterSeveralRaises_ReturnsThemInOrder()
    {
        var aggregate = NewAggregate();

        aggregate.Happen(1);
        aggregate.Happen(2);

        Assert.Equal<IDomainEvent>([new SampleHappened(1), new SampleHappened(2)], aggregate.PullDomainEvents());
    }

    [Fact]
    public void PullDomainEvents_CalledTwice_ReturnsEachEventOnlyOnce()
    {
        var aggregate = NewAggregate();
        aggregate.Happen(1);

        var first = aggregate.PullDomainEvents();
        aggregate.Happen(2);
        var second = aggregate.PullDomainEvents();

        Assert.Equal<IDomainEvent>([new SampleHappened(1)], first);
        Assert.Equal<IDomainEvent>([new SampleHappened(2)], second);
        Assert.Empty(aggregate.PullDomainEvents());
    }

    [Fact]
    public void PullDomainEvents_ThroughTheAggregateRootInterface_ReturnsTheRaisedEvents()
    {
        var aggregate = NewAggregate();
        IAggregateRoot root = aggregate;

        aggregate.Happen(1);

        Assert.Equal(aggregate.OwnerId, root.OwnerId);
        Assert.Equal(aggregate.Version, root.Version);
        Assert.Equal<IDomainEvent>([new SampleHappened(1)], root.PullDomainEvents());
    }

    [Fact]
    public void Raise_NullEvent_ThrowsArgumentNullException()
    {
        var aggregate = NewAggregate();

        Assert.Throws<ArgumentNullException>(aggregate.RaiseNull);
    }

    [Fact]
    public void IsAt_CurrentVersion_ReturnsTrue()
    {
        var aggregate = NewAggregate();

        Assert.True(aggregate.IsAt(aggregate.Version));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void IsAt_AnotherVersion_ReturnsFalse(int expectedVersion)
    {
        var aggregate = NewAggregate();

        Assert.False(aggregate.IsAt(expectedVersion));
    }

    private static SampleAggregate NewAggregate() => new(SampleId.New(), UserId.New());

    private readonly record struct SampleId(Guid Value) : ITypedId<SampleId>
    {
        public static SampleId New() => new(Guid.CreateVersion7());

        public static SampleId From(Guid value) => new(value);
    }

    private sealed record SampleHappened(int Number) : IDomainEvent;

    private sealed class SampleAggregate(SampleId id, UserId ownerId) : AggregateRoot<SampleId>(id, ownerId)
    {
        public void Happen(int number) => Raise(new SampleHappened(number));

        public void RaiseNull() => Raise(null!);
    }
}
