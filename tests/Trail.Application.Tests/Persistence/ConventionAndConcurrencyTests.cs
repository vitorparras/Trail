using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Trail.Domain.Common;
using Trail.Infrastructure.Persistence;

namespace Trail.Application.Tests.Persistence;

public sealed class ConventionAndConcurrencyTests
{
    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task TypedId_IsStoredAndRetrievedCorrectly(DatabaseProvider provider)
    {
        await using var harness = await TestDbContextFixture.CreateAsync(provider, null, TestContext.Current.CancellationToken);
        var user = new TestCurrentUser { IsAuthenticated = true, Id = UserId.New() };
        var aggregateId = TestAggregateId.New();

        await using (var context = harness.CreateContext(user))
        {
            var aggregate = new TestAggregateRoot(
                aggregateId,
                user.Id,
                "Initial Name",
                DateTime.UtcNow,
                new TestChildItem { Title = "Child Title" });

            context.Aggregates.Add(aggregate);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = harness.CreateContext(user))
        {
            var loaded = await context.Aggregates.FirstOrDefaultAsync(x => x.Id == aggregateId, TestContext.Current.CancellationToken);

            Assert.NotNull(loaded);
            Assert.Equal(aggregateId, loaded.Id);
            Assert.Equal(user.Id, loaded.OwnerId);
        }
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task UtcDateTime_IsNormalizedOnWriteAndUtcOnRead(DatabaseProvider provider)
    {
        await using var harness = await TestDbContextFixture.CreateAsync(provider, null, TestContext.Current.CancellationToken);
        var user = new TestCurrentUser { IsAuthenticated = true, Id = UserId.New() };
        var aggregateId = TestAggregateId.New();
        var unspecifiedTime = new DateTime(2026, 10, 17, 10, 30, 0, DateTimeKind.Unspecified);

        await using (var context = harness.CreateContext(user))
        {
            var aggregate = new TestAggregateRoot(
                aggregateId,
                user.Id,
                "DateTime Aggregate",
                unspecifiedTime,
                new TestChildItem { Title = "Child Title" });

            context.Aggregates.Add(aggregate);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = harness.CreateContext(user))
        {
            var loaded = await context.Aggregates.FirstAsync(x => x.Id == aggregateId, TestContext.Current.CancellationToken);

            Assert.Equal(DateTimeKind.Utc, loaded.CreatedAt.Kind);
            Assert.Equal(DateTime.SpecifyKind(unspecifiedTime, DateTimeKind.Utc), loaded.CreatedAt);
        }
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task OwnerId_GlobalQueryFilter_IsolatesDataBetweenUsers(DatabaseProvider provider)
    {
        await using var harness = await TestDbContextFixture.CreateAsync(provider, null, TestContext.Current.CancellationToken);
        var user1 = new TestCurrentUser { IsAuthenticated = true, Id = UserId.New() };
        var user2 = new TestCurrentUser { IsAuthenticated = true, Id = UserId.New() };
        var anonymous = new TestCurrentUser { IsAuthenticated = false, Id = default };

        var aggregateId1 = TestAggregateId.New();
        var aggregateId2 = TestAggregateId.New();

        await using (var context = harness.CreateContext(user1))
        {
            context.Aggregates.Add(new TestAggregateRoot(
                aggregateId1,
                user1.Id,
                "User 1 Aggregate",
                DateTime.UtcNow,
                new TestChildItem { Title = "Item 1" }));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = harness.CreateContext(user2))
        {
            context.Aggregates.Add(new TestAggregateRoot(
                aggregateId2,
                user2.Id,
                "User 2 Aggregate",
                DateTime.UtcNow,
                new TestChildItem { Title = "Item 2" }));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = harness.CreateContext(user1))
        {
            var user1Aggregates = await context.Aggregates.ToListAsync(TestContext.Current.CancellationToken);
            Assert.Single(user1Aggregates);
            Assert.Equal(aggregateId1, user1Aggregates[0].Id);
        }

        await using (var context = harness.CreateContext(user2))
        {
            var user2Aggregates = await context.Aggregates.ToListAsync(TestContext.Current.CancellationToken);
            Assert.Single(user2Aggregates);
            Assert.Equal(aggregateId2, user2Aggregates[0].Id);
        }

        await using (var context = harness.CreateContext(anonymous))
        {
            var anonymousAggregates = await context.Aggregates.ToListAsync(TestContext.Current.CancellationToken);
            Assert.Empty(anonymousAggregates);

            var allAggregates = await context.Aggregates.IgnoreQueryFilters().ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, allAggregates.Count);
        }
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task OptimisticConcurrency_RootModification_IncrementsVersion(DatabaseProvider provider)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        using var serviceProvider = services.BuildServiceProvider();
        var interceptor = new Trail.Infrastructure.Persistence.Interceptors.DomainEventDispatchInterceptor(
            serviceProvider,
            serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Trail.Infrastructure.Persistence.Interceptors.DomainEventDispatchInterceptor>>());

        await using var harness = await TestDbContextFixture.CreateAsync(provider, interceptor, TestContext.Current.CancellationToken);
        var user = new TestCurrentUser { IsAuthenticated = true, Id = UserId.New() };
        var aggregateId = TestAggregateId.New();

        await using (var context = harness.CreateContext(user))
        {
            var aggregate = new TestAggregateRoot(
                aggregateId,
                user.Id,
                "Initial",
                DateTime.UtcNow,
                new TestChildItem { Title = "Child" });

            context.Aggregates.Add(aggregate);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.Equal(0, aggregate.Version);
        }

        await using (var context = harness.CreateContext(user))
        {
            var loaded = await context.Aggregates.FirstAsync(x => x.Id == aggregateId, TestContext.Current.CancellationToken);
            Assert.Equal(0, loaded.Version);

            loaded.UpdateName("Updated Root");
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.Equal(1, loaded.Version);
        }

        await using (var context = harness.CreateContext(user))
        {
            var reloaded = await context.Aggregates.FirstAsync(x => x.Id == aggregateId, TestContext.Current.CancellationToken);
            Assert.Equal(1, reloaded.Version);

            reloaded.UpdateName("Updated Again");
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, reloaded.Version);
        }
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task OptimisticConcurrency_OwnedEntityModification_IncrementsVersion(DatabaseProvider provider)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        using var serviceProvider = services.BuildServiceProvider();
        var interceptor = new Trail.Infrastructure.Persistence.Interceptors.DomainEventDispatchInterceptor(
            serviceProvider,
            serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Trail.Infrastructure.Persistence.Interceptors.DomainEventDispatchInterceptor>>());

        await using var harness = await TestDbContextFixture.CreateAsync(provider, interceptor, TestContext.Current.CancellationToken);
        var user = new TestCurrentUser { IsAuthenticated = true, Id = UserId.New() };
        var aggregateId = TestAggregateId.New();

        await using (var context = harness.CreateContext(user))
        {
            var aggregate = new TestAggregateRoot(
                aggregateId,
                user.Id,
                "Initial",
                DateTime.UtcNow,
                new TestChildItem { Title = "Initial Child" });

            context.Aggregates.Add(aggregate);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            Assert.Equal(0, aggregate.Version);
        }

        await using (var context = harness.CreateContext(user))
        {
            var loaded = await context.Aggregates.FirstAsync(x => x.Id == aggregateId, TestContext.Current.CancellationToken);
            Assert.Equal(0, loaded.Version);

            loaded.UpdateChild("Updated Child Title Only");
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.Equal(1, loaded.Version);
        }

        await using (var context = harness.CreateContext(user))
        {
            var reloaded = await context.Aggregates.FirstAsync(x => x.Id == aggregateId, TestContext.Current.CancellationToken);
            Assert.Equal(1, reloaded.Version);
            Assert.Equal("Updated Child Title Only", reloaded.Child.Title);
        }
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task OptimisticConcurrency_StaleUpdate_ThrowsDbUpdateConcurrencyException(DatabaseProvider provider)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        using var serviceProvider = services.BuildServiceProvider();
        var interceptor = new Trail.Infrastructure.Persistence.Interceptors.DomainEventDispatchInterceptor(
            serviceProvider,
            serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Trail.Infrastructure.Persistence.Interceptors.DomainEventDispatchInterceptor>>());

        await using var harness = await TestDbContextFixture.CreateAsync(provider, interceptor, TestContext.Current.CancellationToken);
        var user = new TestCurrentUser { IsAuthenticated = true, Id = UserId.New() };
        var aggregateId = TestAggregateId.New();

        await using (var context = harness.CreateContext(user))
        {
            var aggregate = new TestAggregateRoot(
                aggregateId,
                user.Id,
                "Initial",
                DateTime.UtcNow,
                new TestChildItem { Title = "Child" });

            context.Aggregates.Add(aggregate);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var contextA = harness.CreateContext(user);
        await using var contextB = harness.CreateContext(user);

        var loadedA = await contextA.Aggregates.FirstAsync(x => x.Id == aggregateId, TestContext.Current.CancellationToken);
        var loadedB = await contextB.Aggregates.FirstAsync(x => x.Id == aggregateId, TestContext.Current.CancellationToken);

        loadedA.UpdateName("Update from context A");
        await contextA.SaveChangesAsync(TestContext.Current.CancellationToken);

        loadedB.UpdateName("Conflicting update from context B");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextB.SaveChangesAsync(TestContext.Current.CancellationToken));
    }
}
