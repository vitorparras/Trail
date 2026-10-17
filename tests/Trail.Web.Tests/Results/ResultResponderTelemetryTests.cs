using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Mvc.Testing;
using Trail.Application.Telemetry;
using Trail.Domain.Common;
using Trail.Web.Results;

namespace Trail.Web.Tests.Results;

public sealed class ResultResponderTelemetryTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Respond_SetsActivityCurrentTags()
    {
        using var activity = new Activity("TestActivity").Start();
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: false);
        var (context, page) = testContext;
        var result = Result.Ok(Message.Success("Code1"), Message.Info("Code2"));

        page.Respond(result, "MyUseCase");

        Assert.Equal("MyUseCase", activity.GetTagItem("trail.use_case"));
        Assert.Equal("Ok", activity.GetTagItem("trail.result.status"));
        var codes = activity.GetTagItem("trail.result.message_codes") as string[];
        Assert.NotNull(codes);
        Assert.Equal(["Code1", "Code2"], codes);
    }

    [Fact]
    public async Task Respond_RecordsApplicationMetricsMeasurement()
    {
        var testUseCase = $"FindNonExistent_{Guid.NewGuid():N}";
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: false);
        var (context, page) = testContext;
        var result = Result.NotFound();

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
            string? currentUseCase = null;
            string? currentStatus = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "use_case")
                {
                    currentUseCase = tag.Value as string;
                }
                else if (tag.Key == "status")
                {
                    currentStatus = tag.Value as string;
                }
            }

            if (currentUseCase == testUseCase)
            {
                recordedValue = measurement;
                recordedUseCase = currentUseCase;
                recordedStatus = currentStatus;
            }
        });

        listener.Start();

        page.Respond(result, testUseCase);

        Assert.Equal(1L, recordedValue);
        Assert.Equal(testUseCase, recordedUseCase);
        Assert.Equal("NotFound", recordedStatus);
    }
}
