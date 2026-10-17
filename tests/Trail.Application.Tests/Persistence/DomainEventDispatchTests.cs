using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Infrastructure.Persistence;
using Trail.Infrastructure.Persistence.Interceptors;

namespace Trail.Application.Tests.Persistence;

public sealed class DomainEventDispatchTests
{
    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task SavedChanges_DispatchesEventsToConsumersInOrderAndLogs(DatabaseProvider provider)
    {
        var recordingConsumer = new RecordingConsumer();
        var testLogger = new TestLogger<DomainEventDispatchInterceptor>();

        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventConsumer<TestDomainEvent>>(recordingConsumer);
        services.AddSingleton<ILogger<DomainEventDispatchInterceptor>>(testLogger);

        using var serviceProvider = services.BuildServiceProvider();
        var interceptor = new DomainEventDispatchInterceptor(serviceProvider, testLogger);

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

        await using (var context = harness.CreateContext(user))
        {
            var loaded = await context.Aggregates.FirstAsync(x => x.Id == aggregateId, TestContext.Current.CancellationToken);
            loaded.UpdateName("Dispatched Name");

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Single(recordingConsumer.ReceivedEvents);
        Assert.Equal("Dispatched Name", recordingConsumer.ReceivedEvents[0].Name);

        Assert.Contains(testLogger.Logs, log =>
            log.Level == LogLevel.Information &&
            log.Message.Contains("Dispatched domain event TestDomainEvent", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task SavedChanges_WhenConsumerThrows_LogsErrorAndContinuesRemainingConsumersWithoutFailingCommit(DatabaseProvider provider)
    {
        var throwingConsumer = new ThrowingConsumer();
        var recordingConsumer = new RecordingConsumer();
        var testLogger = new TestLogger<DomainEventDispatchInterceptor>();

        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventConsumer<TestDomainEvent>>(throwingConsumer);
        services.AddSingleton<IDomainEventConsumer<TestDomainEvent>>(recordingConsumer);
        services.AddSingleton<ILogger<DomainEventDispatchInterceptor>>(testLogger);

        using var serviceProvider = services.BuildServiceProvider();
        var interceptor = new DomainEventDispatchInterceptor(serviceProvider, testLogger);

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

        await using (var context = harness.CreateContext(user))
        {
            var loaded = await context.Aggregates.FirstAsync(x => x.Id == aggregateId, TestContext.Current.CancellationToken);
            loaded.UpdateName("Persisted Despite Consumer Error");

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = harness.CreateContext(user))
        {
            var reloaded = await context.Aggregates.FirstAsync(x => x.Id == aggregateId, TestContext.Current.CancellationToken);
            Assert.Equal("Persisted Despite Consumer Error", reloaded.Name);
        }

        Assert.Single(recordingConsumer.ReceivedEvents);
        Assert.Equal("Persisted Despite Consumer Error", recordingConsumer.ReceivedEvents[0].Name);

        Assert.Contains(testLogger.Logs, log =>
            log.Level == LogLevel.Error &&
            log.Message.Contains("failed while handling domain event TestDomainEvent", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task SaveChangesFailed_ClearsPendingEventsWithoutDispatching(DatabaseProvider provider)
    {
        var recordingConsumer = new RecordingConsumer();
        var testLogger = new TestLogger<DomainEventDispatchInterceptor>();

        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventConsumer<TestDomainEvent>>(recordingConsumer);
        services.AddSingleton<ILogger<DomainEventDispatchInterceptor>>(testLogger);

        using var serviceProvider = services.BuildServiceProvider();
        var interceptor = new DomainEventDispatchInterceptor(serviceProvider, testLogger);

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

        await using (var contextA = harness.CreateContext(user))
        await using (var contextB = harness.CreateContext(user))
        {
            var loadedA = await contextA.Aggregates.FirstAsync(x => x.Id == aggregateId, TestContext.Current.CancellationToken);
            var loadedB = await contextB.Aggregates.FirstAsync(x => x.Id == aggregateId, TestContext.Current.CancellationToken);

            loadedA.UpdateName("Update A");
            await contextA.SaveChangesAsync(TestContext.Current.CancellationToken);

            loadedB.UpdateName("Update B with event");
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextB.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        Assert.Single(recordingConsumer.ReceivedEvents);
        Assert.Equal("Update A", recordingConsumer.ReceivedEvents[0].Name);
    }

    private sealed class RecordingConsumer : IDomainEventConsumer<TestDomainEvent>
    {
        public List<TestDomainEvent> ReceivedEvents { get; } = [];

        public Task ConsumeAsync(TestDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            ReceivedEvents.Add(domainEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingConsumer : IDomainEventConsumer<TestDomainEvent>
    {
        public Task ConsumeAsync(TestDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Consumer deliberate failure.");
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Logs { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Logs.Add((logLevel, formatter(state, exception), exception));
        }
    }
}
