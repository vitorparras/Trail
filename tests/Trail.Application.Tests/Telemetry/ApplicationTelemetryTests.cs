using System.Diagnostics.Metrics;
using Trail.Application.Telemetry;

namespace Trail.Application.Tests.Telemetry;

public sealed class ApplicationTelemetryTests
{
    [Fact]
    public void ActivitySource_Name_IsTrailApplication()
    {
        Assert.Equal("Trail.Application", ApplicationTelemetry.ActivitySource.Name);
    }

    [Fact]
    public void ApplicationMetrics_CreatedFromAMeterFactory_UsesTheTrailApplicationMeterFromThatFactory()
    {
        using var meterFactory = new RecordingMeterFactory();

        var metrics = new ApplicationMetrics(meterFactory);

        Assert.Equal("Trail.Application", metrics.Meter.Name);
        Assert.Same(metrics.Meter, Assert.Single(meterFactory.Meters));
    }

    private sealed class RecordingMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public IReadOnlyList<Meter> Meters => _meters;

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (var meter in _meters)
            {
                meter.Dispose();
            }
        }
    }
}
