using Trail.Domain.Common;
using Trail.Web.Results;

namespace Trail.Web.Tests.Results;

public sealed class ResultResponderMessageResourceTests(ResourceNameLocalizationWebApplicationFactory factory)
    : IClassFixture<ResourceNameLocalizationWebApplicationFactory>
{
    public static TheoryData<string, string, bool> CodesOfEachPrefix => new()
    {
        { "practice.sample.message", nameof(PracticeResource), true },
        { "practice.sample.message", nameof(PracticeResource), false },
        { "fitness.sample.message", nameof(FitnessResource), true },
        { "fitness.sample.message", nameof(FitnessResource), false },
        { "studytime.sample.message", nameof(StudyTimeResource), true },
        { "studytime.sample.message", nameof(StudyTimeResource), false },
        { "pwa.sample.message", nameof(PwaResource), true },
        { "pwa.sample.message", nameof(PwaResource), false },
        { "planning.sample.message", nameof(SharedResource), true },
        { "planning.sample.message", nameof(SharedResource), false },
    };

    [Theory]
    [MemberData(nameof(CodesOfEachPrefix))]
    public async Task Respond_ConflictWithCodeOfPrefix_ShowsTheTextOfItsResource(string code, string resourceName, bool isHtmx)
    {
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx);
        var (context, page) = testContext;
        var expected = ResourceNameLocalizer.TextFor(resourceName, code);

        var actionResult = page.Respond(Result.Conflict(Message.Warning(code)), "UpdateSample");
        var (statusCode, _, _, document) = await ResultTestHelper.ExecuteAsync(actionResult, context);

        Assert.Equal(409, statusCode);
        Assert.Equal(expected, document.QuerySelector("div.alert-warning:not([data-toast]) > span")?.TextContent.Trim());
        var toast = Assert.Single(document.QuerySelectorAll("#toasts > [data-toast], [hx-swap-oob] > [data-toast]"));
        Assert.Equal(expected, toast.QuerySelector("span")?.TextContent.Trim());
    }

    [Theory]
    [MemberData(nameof(CodesOfEachPrefix))]
    public async Task Respond_InvalidWithCodeOfPrefix_AddsTheTextOfItsResourceToModelState(string code, string resourceName, bool isHtmx)
    {
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx, httpMethod: "POST");
        var (_, page) = testContext;

        page.Respond(Result.Invalid(Message.Error(code, target: "Name")), "CreateSample", formPartialViewName: "_SampleItem");

        var error = Assert.Single(page.ModelState["Name"]!.Errors);
        Assert.Equal(ResourceNameLocalizer.TextFor(resourceName, code), error.ErrorMessage);
    }
}
