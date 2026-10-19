
using System.Diagnostics.Metrics;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Telemetry;

public sealed class ApplicationMetrics
{
    public const string ResultsInstrumentName = "trail.usecase.results";
    public const string EmailsSentInstrumentName = "trail.emails.sent";
    public const string EmailsFailedInstrumentName = "trail.emails.failed";
    public const string EmailsCoalescedInstrumentName = "trail.emails.coalesced";
    public const string PlansCreatedInstrumentName = "trail.plans.created";
    public const string TopicStatusChangesInstrumentName = "trail.topics.status_changes";

    private readonly Counter<long> _resultsCounter;
    private readonly Counter<long> _emailsSentCounter;
    private readonly Counter<long> _emailsFailedCounter;
    private readonly Counter<long> _emailsCoalescedCounter;
    private readonly Counter<long> _plansCreatedCounter;
    private readonly Counter<long> _topicStatusChangesCounter;

    public ApplicationMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter = meterFactory.Create(ApplicationTelemetry.Name);
        _resultsCounter = Meter.CreateCounter<long>(ResultsInstrumentName);
        _emailsSentCounter = Meter.CreateCounter<long>(EmailsSentInstrumentName);
        _emailsFailedCounter = Meter.CreateCounter<long>(EmailsFailedInstrumentName);
        _emailsCoalescedCounter = Meter.CreateCounter<long>(EmailsCoalescedInstrumentName);
        _plansCreatedCounter = Meter.CreateCounter<long>(PlansCreatedInstrumentName);
        _topicStatusChangesCounter = Meter.CreateCounter<long>(TopicStatusChangesInstrumentName);
    }

    public Meter Meter { get; }

    public void RecordResult(string useCase, ResultStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(useCase);

        _resultsCounter.Add(
            1,
            new KeyValuePair<string, object?>("use_case", useCase),
            new KeyValuePair<string, object?>("status", status.ToString()));
    }

    public void RecordEmailSent(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        _emailsSentCounter.Add(1, new KeyValuePair<string, object?>("kind", kind));
    }

    public void RecordEmailFailed(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        _emailsFailedCounter.Add(1, new KeyValuePair<string, object?>("kind", kind));
    }

    public void RecordEmailCoalesced(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        _emailsCoalescedCounter.Add(1, new KeyValuePair<string, object?>("kind", kind));
    }

    public void RecordPlanCreated(StudyPlanOrigin origin)
    {
        _plansCreatedCounter.Add(1, new KeyValuePair<string, object?>("origin", origin.ToString()));
    }

    public void RecordTopicStatusChanged(TopicStatus previousStatus, TopicStatus newStatus)
    {
        _topicStatusChangesCounter.Add(
            1,
            new KeyValuePair<string, object?>("from", previousStatus.ToString()),
            new KeyValuePair<string, object?>("to", newStatus.ToString()));
    }
}
