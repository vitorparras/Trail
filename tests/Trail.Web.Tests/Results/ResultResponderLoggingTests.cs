using Trail.Domain.Common;
using Trail.Web.Results;

namespace Trail.Web.Tests.Results;

public sealed class ResultResponderLoggingTests(LogRecordingWebApplicationFactory factory) : IClassFixture<LogRecordingWebApplicationFactory>
{
    private static readonly string ResponderCategory = typeof(ResultResponder).FullName!;

    [Theory]
    [InlineData("practice.sample.untranslated", nameof(PracticeResource), true)]
    [InlineData("practice.sample.untranslated", nameof(PracticeResource), false)]
    [InlineData("planning.sample.untranslated", nameof(SharedResource), true)]
    [InlineData("planning.sample.untranslated", nameof(SharedResource), false)]
    public async Task Respond_UntranslatedCode_ShowsTheCodeAndLogsAWarningWithItsResource(string code, string resourceName, bool isHtmx)
    {
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx);
        var (context, page) = testContext;

        var actionResult = page.Respond(Result.Conflict(Message.Warning(code)), "UpdateSample");
        var (_, _, _, document) = await ResultTestHelper.ExecuteAsync(actionResult, context);

        Assert.Equal(code, document.QuerySelector("div.alert-warning:not([data-toast]) > span")?.TextContent.Trim());
        Assert.Contains(UntranslatedWarnings(), log =>
            Equals(log.State["Code"], code) && Equals(log.State["Resource"], resourceName));
    }

    [Fact]
    public async Task Respond_TranslatedCode_LogsNoWarning()
    {
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: true);
        var (context, page) = testContext;

        var actionResult = page.Respond(Result.ConcurrencyConflict(), "UpdateSample");
        await ResultTestHelper.ExecuteAsync(actionResult, context);

        Assert.DoesNotContain(UntranslatedWarnings(), log => Equals(log.State["Code"], Result.ConcurrencyConflictCode));
    }

    private List<RecordedLog> UntranslatedWarnings() =>
        [.. factory.Recorder.Logs.Where(log => log.Category == ResponderCategory && log.Level == LogLevel.Warning && log.EventId.Id == 1)];
}
