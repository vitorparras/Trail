using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Domain.Common;
using Trail.Web.Results;

namespace Trail.Web.Tests.Results;

public sealed class ResultResponderInvalidTests(TrailWebApplicationFactory factory) : IClassFixture<TrailWebApplicationFactory>
{
    [Fact]
    public async Task Respond_Invalid_WithTargetedMessages_AddsErrorsToModelStateUnderTargetKey()
    {
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: false);
        var (context, page) = testContext;
        var result = Result.Invalid(Message.Error("TitleRequired", target: "Title"));

        page.Respond(result, "CreateItem");

        Assert.True(page.ModelState.ContainsKey("Title"));
        var error = Assert.Single(page.ModelState["Title"]!.Errors);
        Assert.Equal("TitleRequired", error.ErrorMessage);
    }

    [Fact]
    public async Task Respond_Invalid_WithUntargetedMessages_AddsModelLevelError()
    {
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: false);
        var (context, page) = testContext;
        var result = Result.Invalid(Message.Error("FormInvalid"));

        page.Respond(result, "CreateItem");

        Assert.True(page.ModelState.ContainsKey(string.Empty));
        var error = Assert.Single(page.ModelState[string.Empty]!.Errors);
        Assert.Equal("FormInvalid", error.ErrorMessage);
    }

    [Fact]
    public async Task Respond_Invalid_WithHtmx_Returns422WithFormPartialAndUntargetedToasts()
    {
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: true);
        var (context, page) = testContext;
        var result = Result.Invalid(
            Message.Error("TitleRequired", target: "Title"),
            Message.Error("FormInvalid"));

        var actionResult = page.Respond(result, "CreateItem", formPartialViewName: "_SampleItem");
        var (statusCode, contentType, body, doc) = await ResultTestHelper.ExecuteAsync(actionResult, context);

        Assert.Equal(422, statusCode);
        Assert.NotNull(doc.QuerySelector("#sample-item"));

        var oob = Assert.Single(doc.QuerySelectorAll("[hx-swap-oob]"));
        Assert.Equal("beforeend:#toasts", oob.GetAttribute("hx-swap-oob"));
        var toast = Assert.Single(oob.QuerySelectorAll("[data-toast]"));
        Assert.Equal("error", toast.GetAttribute("data-toast"));
        Assert.Equal("FormInvalid", toast.QuerySelector("span")?.TextContent);
    }

    [Fact]
    public async Task Respond_Invalid_WithoutHtmx_Returns422WithPage()
    {
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: false);
        var (context, page) = testContext;
        var result = Result.Invalid(Message.Error("FormInvalid"));

        var actionResult = page.Respond(result, "CreateItem");

        Assert.IsType<PageResult>(actionResult);
        Assert.Equal(422, page.Response.StatusCode);
    }
}
