using System.Diagnostics.Metrics;

namespace Trail.Application.Telemetry;

public sealed class ApplicationMetrics(IMeterFactory meterFactory)
{
    public Meter Meter { get; } = meterFactory.Create(ApplicationTelemetry.Name);
}
