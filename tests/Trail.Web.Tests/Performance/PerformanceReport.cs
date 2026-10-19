namespace Trail.Web.Tests.Performance;

internal static class PerformanceReport
{
    private const string SummaryVariable = "GITHUB_STEP_SUMMARY";

    private const string SummaryHeader =
        "| Scenario | Provider | p50 (ms) | p95 (ms) | max (ms) | Budget (ms) | Result |\n| --- | --- | ---: | ---: | ---: | ---: | --- |";

    public static void Publish(PerformanceMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);

        var line = measurement.Describe();
        TestContext.Current.TestOutputHelper?.WriteLine(line);
        TestContext.Current.SendDiagnosticMessage(line);
        Console.WriteLine(line);

        AppendToStepSummary(measurement);
    }

    private static void AppendToStepSummary(PerformanceMeasurement measurement)
    {
        var summaryPath = Environment.GetEnvironmentVariable(SummaryVariable);
        if (string.IsNullOrWhiteSpace(summaryPath))
        {
            return;
        }

        try
        {
            var existing = File.Exists(summaryPath) ? File.ReadAllText(summaryPath) : string.Empty;
            var header = existing.Contains(SummaryHeader, StringComparison.Ordinal)
                ? string.Empty
                : $"### Performance budget\n\n{SummaryHeader}\n";
            File.AppendAllText(summaryPath, header + measurement.ToMarkdownRow() + "\n");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
