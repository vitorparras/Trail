using System.Diagnostics.Metrics;
using Trail.Domain.Common;

namespace Trail.Application.Telemetry;

public sealed class ApplicationMetrics
{
    public const string ResultsInstrumentName = "trail.usecase.results";
    public const string EmailsSentInstrumentName = "trail.emails.sent";
    public const string EmailsFailedInstrumentName = "trail.emails.failed";

    private readonly Counter<long> _resultsCounter;
    private readonly Counter<long> _emailsSentCounter;
    private readonly Counter<long> _emailsFailedCounter;

    public ApplicationMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter = meterFactory.Create(ApplicationTelemetry.Name);
        _resultsCounter = Meter.CreateCounter<long>(ResultsInstrumentName);
        _emailsSentCounter = Meter.CreateCounter<long>(EmailsSentInstrumentName);
        _emailsFailedCounter = Meter.CreateCounter<long>(EmailsFailedInstrumentName);
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
}
