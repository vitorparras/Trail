using Microsoft.EntityFrameworkCore;
using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Infrastructure.Persistence;

namespace Trail.Application.Tests.Persistence;

public readonly record struct TestAggregateId(Guid Value) : ITypedId<TestAggregateId>
{
    public static TestAggregateId New() => new(Guid.CreateVersion7());

    public static TestAggregateId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}

public sealed record TestDomainEvent(string Name) : IDomainEvent;

public sealed class TestChildItem
{
    public string Title { get; set; } = string.Empty;
}

public sealed class TestAggregateRoot : AggregateRoot<TestAggregateId>
{
    private TestAggregateRoot()
        : base(TestAggregateId.New(), UserId.New())
    {
        Name = string.Empty;
        Child = new TestChildItem();
    }

    public TestAggregateRoot(
        TestAggregateId id,
        UserId ownerId,
        string name,
        DateTime createdAt,
        TestChildItem child)
        : base(id, ownerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(child);

        Name = name;
        CreatedAt = createdAt;
        Child = child;
    }

    public string Name { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public TestChildItem Child { get; private set; }

    public void UpdateName(string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        Name = newName;
        Raise(new TestDomainEvent(newName));
    }

    public void UpdateChild(string newTitle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newTitle);
        Child.Title = newTitle;
    }

    public void RaiseEventOnly(string eventName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        Raise(new TestDomainEvent(eventName));
    }
}

public sealed class TestDbContext : AppDbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options, ICurrentUser currentUser)
        : base(options, currentUser)
    {
    }

    public DbSet<TestAggregateRoot> Aggregates => Set<TestAggregateRoot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<TestAggregateRoot>(builder =>
        {
            builder.HasKey(e => e.Id);
            builder.OwnsOne(e => e.Child);
        });

        base.OnModelCreating(modelBuilder);
    }
}

public sealed class TestCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; set; }

    public UserId Id { get; set; }
}
