using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Trail.Application.Telemetry;

namespace Trail.Web.Tests.Observability;

internal sealed class UseCaseResultRecorder : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly ConcurrentQueue<(string? UseCase, string? Status)> _results = new();

    public UseCaseResultRecorder(ApplicationMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics.Meter)
                && instrument.Name == ApplicationMetrics.ResultsInstrumentName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            string? useCase = null;
            string? status = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "use_case")
                {
                    useCase = tag.Value as string;
                }
                else if (tag.Key == "status")
                {
                    status = tag.Value as string;
                }
            }

            _results.Enqueue((useCase, status));
        });

        _listener.Start();
    }

    public int Count(string useCase, string status) =>
        _results.Count(result => result.UseCase == useCase && result.Status == status);

    public void Dispose() => _listener.Dispose();
}
