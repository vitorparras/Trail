using Trail.Domain.Common;

namespace Trail.Application.Abstractions;

public interface IDomainEventConsumer<TEvent>
    where TEvent : class, IDomainEvent
{
    Task ConsumeAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
