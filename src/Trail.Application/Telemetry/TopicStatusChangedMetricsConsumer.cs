
using Trail.Application.Abstractions;
using Trail.Domain.Planning;

namespace Trail.Application.Telemetry;

public sealed class TopicStatusChangedMetricsConsumer : IDomainEventConsumer<TopicStatusChanged>
{
    private readonly ApplicationMetrics _metrics;

    public TopicStatusChangedMetricsConsumer(ApplicationMetrics metrics)
    {
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    public Task ConsumeAsync(TopicStatusChanged domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _metrics.RecordTopicStatusChanged(domainEvent.PreviousStatus, domainEvent.NewStatus);
        return Task.CompletedTask;
    }
}
