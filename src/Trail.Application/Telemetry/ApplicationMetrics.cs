using System.Diagnostics.Metrics;
using Trail.Domain.Common;

namespace Trail.Application.Telemetry;

public sealed class ApplicationMetrics
{
    public const string ResultsInstrumentName = "trail.usecase.results";

    private readonly Counter<long> _resultsCounter;

    public ApplicationMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter = meterFactory.Create(ApplicationTelemetry.Name);
        _resultsCounter = Meter.CreateCounter<long>(ResultsInstrumentName);
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
}
