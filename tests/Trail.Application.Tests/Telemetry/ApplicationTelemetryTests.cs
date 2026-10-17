using System.Diagnostics.Metrics;
using Trail.Application.Telemetry;
using Trail.Domain.Common;

namespace Trail.Application.Tests.Telemetry;

public sealed class ApplicationTelemetryTests
{
    [Fact]
    public void ActivitySource_Name_IsTrailApplication()
    {
        Assert.Equal("Trail.Application", ApplicationTelemetry.ActivitySource.Name);
    }

    [Fact]
    public void ApplicationMetrics_Constructor_WithNullMeterFactory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ApplicationMetrics(null!));
    }

    [Fact]
    public void ApplicationMetrics_CreatedFromAMeterFactory_UsesTheTrailApplicationMeterFromThatFactory()
    {
        using var meterFactory = new RecordingMeterFactory();

        var metrics = new ApplicationMetrics(meterFactory);

        Assert.Equal("Trail.Application", metrics.Meter.Name);
        Assert.Same(metrics.Meter, Assert.Single(meterFactory.Meters));
    }

    [Fact]
    public void RecordResult_IncrementsResultsCounterWithUseCaseAndStatusTags()
    {
        using var meterFactory = new RecordingMeterFactory();
        var metrics = new ApplicationMetrics(meterFactory);

        long? recordedValue = null;
        string? recordedUseCase = null;
        string? recordedStatus = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ApplicationTelemetry.Name
                && instrument.Name == ApplicationMetrics.ResultsInstrumentName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            recordedValue = measurement;
            foreach (var tag in tags)
            {
                if (tag.Key == "use_case")
                {
                    recordedUseCase = tag.Value as string;
                }
                else if (tag.Key == "status")
                {
                    recordedStatus = tag.Value as string;
                }
            }
        });

        listener.Start();

        metrics.RecordResult("RecordQuestions", ResultStatus.Ok);

        Assert.Equal(1L, recordedValue);
        Assert.Equal("RecordQuestions", recordedUseCase);
        Assert.Equal("Ok", recordedStatus);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RecordResult_WithNullOrWhitespaceUseCase_ThrowsArgumentException(string? useCase)
    {
        using var meterFactory = new RecordingMeterFactory();
        var metrics = new ApplicationMetrics(meterFactory);

        Assert.ThrowsAny<ArgumentException>(() => metrics.RecordResult(useCase!, ResultStatus.Ok));
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
