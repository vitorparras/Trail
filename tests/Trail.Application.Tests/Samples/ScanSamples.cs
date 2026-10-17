using Trail.Application.Abstractions;
using Trail.Domain.Common;

namespace Trail.Application.Tests.Samples;

public sealed record FirstSampleEvent : IDomainEvent;

public sealed record SecondSampleEvent : IDomainEvent;

public sealed class SampleHandler;

public class OpenSampleHandler;

public sealed class SampleService;

public delegate void SampleCallbackHandler();

internal sealed class InternalSampleHandler;

public sealed class SampleEventsConsumer : IDomainEventConsumer<FirstSampleEvent>, IDomainEventConsumer<SecondSampleEvent>
{
    public Task ConsumeAsync(FirstSampleEvent domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ConsumeAsync(SecondSampleEvent domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;
}

public abstract class SampleEventConsumerBase : IDomainEventConsumer<FirstSampleEvent>
{
    public Task ConsumeAsync(FirstSampleEvent domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class FirstSampleEventMetricsConsumer : SampleEventConsumerBase;

public sealed class GenericSampleEventConsumer<TEvent> : IDomainEventConsumer<TEvent>
    where TEvent : class, IDomainEvent
{
    public Task ConsumeAsync(TEvent domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class InternalSampleEventConsumer : IDomainEventConsumer<FirstSampleEvent>
{
    public Task ConsumeAsync(FirstSampleEvent domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;
}
