
using Trail.Application.Abstractions;
using Trail.Domain.Planning;

namespace Trail.Application.Telemetry;

public sealed class StudyPlanCreatedMetricsConsumer : IDomainEventConsumer<StudyPlanCreated>
{
    private readonly ApplicationMetrics _metrics;

    public StudyPlanCreatedMetricsConsumer(ApplicationMetrics metrics)
    {
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    public Task ConsumeAsync(StudyPlanCreated domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _metrics.RecordPlanCreated(domainEvent.Origin);
        return Task.CompletedTask;
    }
}
