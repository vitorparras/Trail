using System.Diagnostics.Metrics;
using Trail.Application.Abstractions;
using Trail.Application.Telemetry;
using Trail.Domain.Common;
using Trail.Domain.Planning;

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
            if (instrument.Meter == metrics.Meter
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

    [Fact]
    public void RecordEmailSent_IncrementsEmailsSentCounterWithKindTag()
    {
        using var meterFactory = new RecordingMeterFactory();
        var metrics = new ApplicationMetrics(meterFactory);

        long? recordedValue = null;
        string? recordedKind = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter == metrics.Meter
                && instrument.Name == ApplicationMetrics.EmailsSentInstrumentName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            recordedValue = measurement;
            foreach (var tag in tags)
            {
                if (tag.Key == "kind")
                {
                    recordedKind = tag.Value as string;
                }
            }
        });

        listener.Start();

        metrics.RecordEmailSent("Confirmation");

        Assert.Equal(1L, recordedValue);
        Assert.Equal("Confirmation", recordedKind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RecordEmailSent_WithNullOrWhitespaceKind_ThrowsArgumentException(string? kind)
    {
        using var meterFactory = new RecordingMeterFactory();
        var metrics = new ApplicationMetrics(meterFactory);

        Assert.ThrowsAny<ArgumentException>(() => metrics.RecordEmailSent(kind!));
    }

    [Fact]
    public void RecordEmailFailed_IncrementsEmailsFailedCounterWithKindTag()
    {
        using var meterFactory = new RecordingMeterFactory();
        var metrics = new ApplicationMetrics(meterFactory);

        long? recordedValue = null;
        string? recordedKind = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter == metrics.Meter
                && instrument.Name == ApplicationMetrics.EmailsFailedInstrumentName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            recordedValue = measurement;
            foreach (var tag in tags)
            {
                if (tag.Key == "kind")
                {
                    recordedKind = tag.Value as string;
                }
            }
        });

        listener.Start();

        metrics.RecordEmailFailed("PasswordReset");

        Assert.Equal(1L, recordedValue);
        Assert.Equal("PasswordReset", recordedKind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RecordEmailFailed_WithNullOrWhitespaceKind_ThrowsArgumentException(string? kind)
    {
        using var meterFactory = new RecordingMeterFactory();
        var metrics = new ApplicationMetrics(meterFactory);

        Assert.ThrowsAny<ArgumentException>(() => metrics.RecordEmailFailed(kind!));
    }

    [Fact]
    public void RecordPlanCreated_IncrementsPlansCreatedCounterWithOriginTag()
    {
        using var meterFactory = new RecordingMeterFactory();
        var metrics = new ApplicationMetrics(meterFactory);

        long? recordedValue = null;
        string? recordedOrigin = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter == metrics.Meter
                && instrument.Name == ApplicationMetrics.PlansCreatedInstrumentName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            recordedValue = measurement;
            foreach (var tag in tags)
            {
                if (tag.Key == "origin")
                {
                    recordedOrigin = tag.Value as string;
                }
            }
        });

        listener.Start();

        metrics.RecordPlanCreated(StudyPlanOrigin.Import);

        Assert.Equal(1L, recordedValue);
        Assert.Equal("Import", recordedOrigin);
    }

    [Fact]
    public void RecordTopicStatusChanged_IncrementsTopicStatusChangesCounterWithFromAndToTags()
    {
        using var meterFactory = new RecordingMeterFactory();
        var metrics = new ApplicationMetrics(meterFactory);

        long? recordedValue = null;
        string? recordedFrom = null;
        string? recordedTo = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter == metrics.Meter
                && instrument.Name == ApplicationMetrics.TopicStatusChangesInstrumentName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            recordedValue = measurement;
            foreach (var tag in tags)
            {
                if (tag.Key == "from")
                {
                    recordedFrom = tag.Value as string;
                }
                else if (tag.Key == "to")
                {
                    recordedTo = tag.Value as string;
                }
            }
        });

        listener.Start();

        metrics.RecordTopicStatusChanged(TopicStatus.ToStudy, TopicStatus.Studying);

        Assert.Equal(1L, recordedValue);
        Assert.Equal("ToStudy", recordedFrom);
        Assert.Equal("Studying", recordedTo);
    }

    [Fact]
    public async Task StudyPlanCreatedMetricsConsumer_ConsumesEventAndRecordsMetric()
    {
        using var meterFactory = new RecordingMeterFactory();
        var metrics = new ApplicationMetrics(meterFactory);
        var consumer = new StudyPlanCreatedMetricsConsumer(metrics);

        long? recordedValue = null;
        string? recordedOrigin = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter == metrics.Meter
                && instrument.Name == ApplicationMetrics.PlansCreatedInstrumentName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            recordedValue = measurement;
            foreach (var tag in tags)
            {
                if (tag.Key == "origin")
                {
                    recordedOrigin = tag.Value as string;
                }
            }
        });

        listener.Start();

        var domainEvent = new StudyPlanCreated(StudyPlanId.New(), UserId.New(), StudyPlanOrigin.Restore);
        await consumer.ConsumeAsync(domainEvent, CancellationToken.None);

        Assert.Equal(1L, recordedValue);
        Assert.Equal("Restore", recordedOrigin);
    }

    [Fact]
    public void StudyPlanCreatedMetricsConsumer_ConstructorWithNullMetrics_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new StudyPlanCreatedMetricsConsumer(null!));
    }

    [Fact]
    public async Task StudyPlanCreatedMetricsConsumer_ConsumeAsyncWithNullEvent_ThrowsArgumentNullException()
    {
        using var meterFactory = new RecordingMeterFactory();
        var metrics = new ApplicationMetrics(meterFactory);
        var consumer = new StudyPlanCreatedMetricsConsumer(metrics);

        await Assert.ThrowsAsync<ArgumentNullException>(() => consumer.ConsumeAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task TopicStatusChangedMetricsConsumer_ConsumesEventAndRecordsMetric()
    {
        using var meterFactory = new RecordingMeterFactory();
        var metrics = new ApplicationMetrics(meterFactory);
        var consumer = new TopicStatusChangedMetricsConsumer(metrics);

        long? recordedValue = null;
        string? recordedFrom = null;
        string? recordedTo = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter == metrics.Meter
                && instrument.Name == ApplicationMetrics.TopicStatusChangesInstrumentName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            recordedValue = measurement;
            foreach (var tag in tags)
            {
                if (tag.Key == "from")
                {
                    recordedFrom = tag.Value as string;
                }
                else if (tag.Key == "to")
                {
                    recordedTo = tag.Value as string;
                }
            }
        });

        listener.Start();

        var domainEvent = new TopicStatusChanged(SubjectId.New(), TopicId.New(), TopicStatus.Studied, TopicStatus.Reviewing);
        await consumer.ConsumeAsync(domainEvent, CancellationToken.None);

        Assert.Equal(1L, recordedValue);
        Assert.Equal("Studied", recordedFrom);
        Assert.Equal("Reviewing", recordedTo);
    }

    [Fact]
    public void TopicStatusChangedMetricsConsumer_ConstructorWithNullMetrics_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new TopicStatusChangedMetricsConsumer(null!));
    }

    [Fact]
    public async Task TopicStatusChangedMetricsConsumer_ConsumeAsyncWithNullEvent_ThrowsArgumentNullException()
    {
        using var meterFactory = new RecordingMeterFactory();
        var metrics = new ApplicationMetrics(meterFactory);
        var consumer = new TopicStatusChangedMetricsConsumer(metrics);

        await Assert.ThrowsAsync<ArgumentNullException>(() => consumer.ConsumeAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void ICurrentUser_DefaultIsAdmin_ReturnsFalse()
    {
        ICurrentUser user = new DefaultCurrentUserStub();

        Assert.False(user.IsAdmin);
    }

    private sealed class DefaultCurrentUserStub : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public UserId Id => UserId.New();
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
