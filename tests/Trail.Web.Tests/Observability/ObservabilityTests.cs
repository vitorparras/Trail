using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Trail.Application.Telemetry;

namespace Trail.Web.Tests.Observability;

public sealed class ObservabilityTests(TrailWebApplicationFactory baseFactory) : IClassFixture<TrailWebApplicationFactory>
{
    [Fact]
    public async Task Request_ProducesActivityAndIncrementsMetrics()
    {
        var metrics = new List<Metric>();
        var serverSpans = new ServerSpanRecorder();

        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddOpenTelemetry()
                    .WithTracing(tracing => tracing.AddProcessor(serverSpans))
                    .WithMetrics(meter => meter.AddInMemoryExporter(metrics));
            });
        });

        using var client = factory.CreateClient();

        var hostMeterFactory = factory.Services.GetRequiredService<IMeterFactory>();
        var requestDurations = 0;
        using var hostMeterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Scope == hostMeterFactory && instrument.Name == "http.server.request.duration")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        hostMeterListener.SetMeasurementEventCallback<double>((_, _, _, _) => Interlocked.Increment(ref requestDurations));
        hostMeterListener.Start();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        var trace = serverSpans.Trace(request);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var requestActivity = await trace.ServerSpanEnded;
        Assert.True(requestActivity.DisplayName.Contains('/') || requestActivity.OperationName.Contains("HttpRequestIn", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, Volatile.Read(ref requestDurations));

        var meterProvider = factory.Services.GetRequiredService<MeterProvider>();
        meterProvider.ForceFlush();

        Assert.Contains(metrics, metric => metric.Name.StartsWith("http.server", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApplicationTelemetryAndMetrics_AreCapturedByPipeline()
    {
        var endedActivities = new EndedActivityRecorder();
        var metrics = new List<Metric>();

        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddOpenTelemetry()
                    .WithTracing(tracing => tracing.AddProcessor(endedActivities))
                    .WithMetrics(meter => meter.AddInMemoryExporter(metrics));
            });
        });

        _ = factory.CreateClient();

        ActivityTraceId customTraceId;
        using (var activity = ApplicationTelemetry.ActivitySource.StartActivity("CustomTestActivity"))
        {
            Assert.NotNull(activity);
            activity.SetTag("test.tag", "custom-value");
            customTraceId = activity.TraceId;
        }

        var appMetrics = factory.Services.GetRequiredService<ApplicationMetrics>();
        var counter = appMetrics.Meter.CreateCounter<long>("trail.application.test_counter");
        counter.Add(1);

        var meterProvider = factory.Services.GetRequiredService<MeterProvider>();
        meterProvider.ForceFlush();

        var recordedActivity = Assert.Single(
            endedActivities.Ended,
            a => a.TraceId == customTraceId && a.OperationName == "CustomTestActivity");
        Assert.Equal("custom-value", recordedActivity.GetTagItem("test.tag"));

        Assert.Contains(metrics, m => m.Name == "trail.application.test_counter");
    }

    [Fact]
    public void StructuredJsonConsoleLogging_OutputsValidJsonWithTraceId_DuringActiveRequest()
    {
        var loggerProviders = baseFactory.Services.GetServices<ILoggerProvider>().ToList();
        Assert.Contains(loggerProviders, provider => provider.GetType().Name.Contains("Console", StringComparison.OrdinalIgnoreCase));

        var options = baseFactory.Services.GetRequiredService<IOptionsMonitor<JsonConsoleFormatterOptions>>().CurrentValue;
        Assert.True(options.IncludeScopes);

        var formatter = baseFactory.Services.GetServices<ConsoleFormatter>()
            .Single(f => f.Name == ConsoleFormatterNames.Json);

        using var writer = new StringWriter();
        var scopeProvider = new LoggerExternalScopeProvider();

        using (var activity = ApplicationTelemetry.ActivitySource.StartActivity("StructuredLoggingTest"))
        {
            Assert.NotNull(activity);

            using (scopeProvider.Push(new Dictionary<string, object>
            {
                ["TraceId"] = activity.TraceId.ToString(),
                ["SpanId"] = activity.SpanId.ToString(),
            }))
            {
                var entry = new LogEntry<string>(
                    LogLevel.Information,
                    "Trail.Observability.Tests",
                    new EventId(1, "TestEvent"),
                    "Structured log test payload",
                    exception: null,
                    formatter: (state, _) => state);

                formatter.Write(in entry, scopeProvider, writer);
            }
        }

        var jsonOutput = writer.ToString().Trim();
        Assert.False(string.IsNullOrWhiteSpace(jsonOutput));

        using var document = JsonDocument.Parse(jsonOutput);
        var root = document.RootElement;

        Assert.Equal("Information", root.GetProperty("LogLevel").GetString());
        Assert.Equal("Structured log test payload", root.GetProperty("Message").GetString());
        Assert.True(jsonOutput.Contains("\"TraceId\":", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WithoutOtlpEndpoint_RequestsSucceedWithoutOtlpExporter()
    {
        using var client = baseFactory.CreateClient();
        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var loggerProviders = baseFactory.Services.GetServices<ILoggerProvider>().ToList();
        Assert.Contains(loggerProviders, provider => provider.GetType().Name.Contains("Console", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(loggerProviders, provider => provider.GetType().Name.Contains("OpenTelemetry", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WithOtlpEndpoint_RegistersOtlpExporter()
    {
        using var factoryWithOtlp = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", "http://127.0.0.1:4317");
        });

        _ = factoryWithOtlp.CreateClient();

        var loggerProviders = factoryWithOtlp.Services.GetServices<ILoggerProvider>();
        Assert.Contains(loggerProviders, provider => provider.GetType().Name.Contains("OpenTelemetry", StringComparison.OrdinalIgnoreCase));
    }
}
