using Microsoft.Extensions.Localization;
using Trail.Domain.Common;
using Trail.Web.Results;

namespace Trail.Web.Tests.Results;

public sealed class ResultResponderStatusTests(TrailWebApplicationFactory factory) : IClassFixture<TrailWebApplicationFactory>
{
    private readonly IStringLocalizer<SharedResource> _localizer = factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>();

    [Fact]
    public async Task Respond_NotFound_WithHtmx_Returns404WithPartial()
    {
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: true);
        var (context, page) = testContext;
        var result = Result.NotFound();

        var actionResult = page.Respond(result, "FindItem");
        var (statusCode, contentType, body, doc) = await ResultTestHelper.ExecuteAsync(actionResult, context);

        Assert.Equal(404, statusCode);
        Assert.Equal(_localizer["NotFoundTitle"].Value, doc.QuerySelector("h1")?.TextContent.Trim());
        Assert.Equal(_localizer["NotFoundDescription"].Value, doc.QuerySelector("p")?.TextContent.Trim());
        Assert.Equal(_localizer["BackToHome"].Value, doc.QuerySelector("a")?.TextContent.Trim());
        Assert.Null(doc.QuerySelector("header"));
    }

    [Fact]
    public async Task Respond_NotFound_WithoutHtmx_Returns404WithFullPage()
    {
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: false);
        var (context, page) = testContext;
        var result = Result.NotFound();

        var actionResult = page.Respond(result, "FindItem");
        var (statusCode, contentType, body, doc) = await ResultTestHelper.ExecuteAsync(actionResult, context);

        Assert.Equal(404, statusCode);
        Assert.NotNull(doc.QuerySelector("html"));
        Assert.NotNull(doc.QuerySelector("header"));
        Assert.NotNull(doc.QuerySelector("main"));
        Assert.NotNull(doc.QuerySelector("#toasts"));
        Assert.Equal(_localizer["NotFoundTitle"].Value, doc.QuerySelector("main h1")?.TextContent.Trim());
    }

    [Fact]
    public async Task Respond_Conflict_WithHtmx_Returns409WithPartialReloadButtonAndToasts()
    {
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: true, path: "/items/123/edit");
        var (context, page) = testContext;
        var result = Result.ConcurrencyConflict();

        var actionResult = page.Respond(result, "UpdateItem");
        var (statusCode, contentType, body, doc) = await ResultTestHelper.ExecuteAsync(actionResult, context);

        Assert.Equal(409, statusCode);
        Assert.Equal(_localizer["ConflictTitle"].Value, doc.QuerySelector("h1")?.TextContent.Trim());

        var alert = doc.QuerySelector(".alert-warning");
        Assert.NotNull(alert);
        Assert.Equal(_localizer["Concurrency.Conflict"].Value, alert.QuerySelector("span")?.TextContent.Trim());

        var reloadLink = doc.QuerySelector("a.btn");
        Assert.NotNull(reloadLink);
        Assert.Equal(_localizer["Reload"].Value, reloadLink.TextContent.Trim());
        Assert.Equal("/items/123/edit", reloadLink.GetAttribute("href"));

        var oob = Assert.Single(doc.QuerySelectorAll("[hx-swap-oob]"));
        Assert.Equal("beforeend:#toasts", oob.GetAttribute("hx-swap-oob"));
    }

    [Fact]
    public async Task Respond_Conflict_WithoutHtmx_Returns409WithFullPage()
    {
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: false, path: "/items/123/edit");
        var (context, page) = testContext;
        var result = Result.ConcurrencyConflict();

        var actionResult = page.Respond(result, "UpdateItem");
        var (statusCode, contentType, body, doc) = await ResultTestHelper.ExecuteAsync(actionResult, context);

        Assert.Equal(409, statusCode);
        Assert.NotNull(doc.QuerySelector("html"));
        Assert.NotNull(doc.QuerySelector("main .alert-warning"));
        Assert.Equal(_localizer["Concurrency.Conflict"].Value, doc.QuerySelector("main .alert-warning span")?.TextContent.Trim());
    }

    [Fact]
    public async Task Respond_Forbidden_WithHtmx_Returns403WithPartial()
    {
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: true);
        var (context, page) = testContext;
        var result = Result.Forbidden();

        var actionResult = page.Respond(result, "GetAdmin");
        var (statusCode, contentType, body, doc) = await ResultTestHelper.ExecuteAsync(actionResult, context);

        Assert.Equal(403, statusCode);
        Assert.Equal(_localizer["ForbiddenTitle"].Value, doc.QuerySelector("h1")?.TextContent.Trim());
        Assert.Equal(_localizer["ForbiddenDescription"].Value, doc.QuerySelector("p")?.TextContent.Trim());
        Assert.Null(doc.QuerySelector("header"));
    }

    [Fact]
    public async Task Respond_Forbidden_WithoutHtmx_Returns403WithFullPage()
    {
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: false);
        var (context, page) = testContext;
        var result = Result.Forbidden();

        var actionResult = page.Respond(result, "GetAdmin");
        var (statusCode, contentType, body, doc) = await ResultTestHelper.ExecuteAsync(actionResult, context);

        Assert.Equal(403, statusCode);
        Assert.NotNull(doc.QuerySelector("html"));
        Assert.Equal(_localizer["ForbiddenTitle"].Value, doc.QuerySelector("main h1")?.TextContent.Trim());
    }
}
