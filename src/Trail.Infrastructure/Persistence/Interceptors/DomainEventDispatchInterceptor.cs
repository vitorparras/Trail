using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trail.Application.Abstractions;
using Trail.Domain.Common;

namespace Trail.Infrastructure.Persistence.Interceptors;

public sealed partial class DomainEventDispatchInterceptor : SaveChangesInterceptor
{
    private static readonly ConcurrentDictionary<Type, Func<object, IDomainEvent, CancellationToken, Task>> ConsumerInvokers = new();

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DomainEventDispatchInterceptor> _logger;
    private readonly List<IDomainEvent> _pendingEvents = [];

    public DomainEventDispatchInterceptor(
        IServiceProvider serviceProvider,
        ILogger<DomainEventDispatchInterceptor> logger)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is not null)
        {
            ProcessBeforeSaving(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is not null)
        {
            ProcessBeforeSaving(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        DispatchPendingEventsAsync(CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await DispatchPendingEventsAsync(cancellationToken).ConfigureAwait(false);
        return await base.SavedChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pendingEvents.Clear();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _pendingEvents.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void ProcessBeforeSaving(DbContext context)
    {
        UpdateAggregateVersions(context);
        CollectDomainEvents(context);
    }

    private static void UpdateAggregateVersions(DbContext context)
    {
        var rootsToIncrement = new HashSet<IAggregateRoot>(ReferenceEqualityComparer.Instance);

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (entry.Entity is IAggregateRoot aggregateRoot)
            {
                if (entry.State == EntityState.Modified)
                {
                    rootsToIncrement.Add(aggregateRoot);
                }
            }
            else
            {
                var root = FindAggregateRootEntry(context, entry);
                if (root is { State: EntityState.Unchanged or EntityState.Modified } && root.Entity is IAggregateRoot parentRoot)
                {
                    rootsToIncrement.Add(parentRoot);
                }
            }
        }

        foreach (var root in rootsToIncrement)
        {
            var entry = context.Entry(root);
            var versionProperty = entry.Property(nameof(IAggregateRoot.Version));
            var currentVersion = (int)(versionProperty.CurrentValue ?? 0);
            versionProperty.OriginalValue = currentVersion;
            versionProperty.CurrentValue = currentVersion + 1;
            versionProperty.IsModified = true;
        }
    }

    private static EntityEntry? FindAggregateRootEntry(DbContext context, EntityEntry entry)
    {
        foreach (var foreignKey in entry.Metadata.GetForeignKeys())
        {
            var principalType = foreignKey.PrincipalEntityType;
            var fkProps = foreignKey.Properties;
            var pkProps = foreignKey.PrincipalKey.Properties;

            foreach (var potentialRoot in context.ChangeTracker.Entries())
            {
                if (!principalType.IsAssignableFrom(potentialRoot.Metadata))
                {
                    continue;
                }

                var matches = true;
                for (var i = 0; i < fkProps.Count; i++)
                {
                    var fkProp = entry.Property(fkProps[i].Name);
                    var fkVal = fkProp.CurrentValue ?? fkProp.OriginalValue;
                    var pkProp = potentialRoot.Property(pkProps[i].Name);
                    var pkVal = pkProp.CurrentValue ?? pkProp.OriginalValue;
                    if (!Equals(fkVal, pkVal))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    return potentialRoot.Entity is IAggregateRoot
                        ? potentialRoot
                        : FindAggregateRootEntry(context, potentialRoot);
                }
            }
        }

        foreach (var root in context.ChangeTracker.Entries<IAggregateRoot>())
        {
            if (IsOwnedBy(root, entry.Entity))
            {
                return root;
            }
        }

        return null;
    }

    private static bool IsOwnedBy(EntityEntry parent, object childEntity)
    {
        foreach (var navigation in parent.Navigations)
        {
            if (navigation.CurrentValue is null)
            {
                continue;
            }

            if (ReferenceEquals(navigation.CurrentValue, childEntity))
            {
                return true;
            }

            if (navigation.CurrentValue is System.Collections.IEnumerable enumerable and not string)
            {
                foreach (var item in enumerable)
                {
                    if (ReferenceEquals(item, childEntity))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void CollectDomainEvents(DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries<IAggregateRoot>())
        {
            var domainEvents = entry.Entity.PullDomainEvents();
            if (domainEvents.Count > 0)
            {
                _pendingEvents.AddRange(domainEvents);
            }
        }
    }

    private async Task DispatchPendingEventsAsync(CancellationToken cancellationToken)
    {
        if (_pendingEvents.Count == 0)
        {
            return;
        }

        var eventsToDispatch = _pendingEvents.ToArray();
        _pendingEvents.Clear();

        foreach (var domainEvent in eventsToDispatch)
        {
            var eventType = domainEvent.GetType();
            LogDomainEventDispatched(_logger, eventType.Name);

            var consumerServiceType = typeof(IDomainEventConsumer<>).MakeGenericType(eventType);
            var consumers = _serviceProvider.GetServices(consumerServiceType);

            foreach (var consumer in consumers)
            {
                if (consumer is null)
                {
                    continue;
                }

                try
                {
                    await InvokeConsumerAsync(consumer, domainEvent, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var actualException = (ex is TargetInvocationException { InnerException: not null } tie
                        ? tie.InnerException
                        : ex) ?? ex;

                    if (actualException is OperationCanceledException && cancellationToken.IsCancellationRequested)
                    {
                        throw actualException;
                    }

                    LogDomainEventConsumerFailed(
                        _logger,
                        actualException,
                        consumer.GetType().FullName ?? consumer.GetType().Name,
                        eventType.Name);
                }
            }
        }
    }

    private static Task InvokeConsumerAsync(object consumer, IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var eventType = domainEvent.GetType();
        var invoker = ConsumerInvokers.GetOrAdd(eventType, static type =>
        {
            var consumerInterface = typeof(IDomainEventConsumer<>).MakeGenericType(type);
            var method = consumerInterface.GetMethod(nameof(IDomainEventConsumer<IDomainEvent>.ConsumeAsync), [type, typeof(CancellationToken)])!;

            return (c, e, ct) => (Task)method.Invoke(c, [e, ct])!;
        });

        return invoker(consumer, domainEvent, cancellationToken);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Dispatched domain event {EventType}.")]
    private static partial void LogDomainEventDispatched(ILogger logger, string eventType);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Consumer {ConsumerType} failed while handling domain event {EventType}.")]
    private static partial void LogDomainEventConsumerFailed(
        ILogger logger,
        Exception exception,
        string consumerType,
        string eventType);
}
