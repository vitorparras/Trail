using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Trail.Domain.Common;
using Trail.Web.Results;
using Trail.Web.Toasts;

namespace Trail.Web.Tests.Results;

public sealed class ResultResponderOkTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Respond_Ok_WithHtmx_Returns200WithPartialView()
    {
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: true);
        var (context, page) = testContext;
        var result = Result.Ok("My Item");

        var actionResult = page.Respond(result, "GetItem", partialViewName: "_SampleItem");
        var (statusCode, contentType, body, doc) = await ResultTestHelper.ExecuteAsync(actionResult, context);

        Assert.Equal(200, statusCode);
        Assert.StartsWith("text/html", contentType);
        Assert.NotNull(doc.QuerySelector("#sample-item"));
        Assert.Equal("My Item", doc.QuerySelector("#sample-item span")?.TextContent);
        Assert.Empty(doc.QuerySelectorAll("[hx-swap-oob]"));
    }

    [Fact]
    public async Task Respond_Ok_WithHtmxAndMessages_RendersPartialAndOobToasts()
    {
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: true);
        var (context, page) = testContext;
        var result = Result.Ok("My Item", Message.Success("ItemCreated"), Message.Info("NextStep"));

        var actionResult = page.Respond(result, "CreateItem", partialViewName: "_SampleItem");
        var (statusCode, contentType, body, doc) = await ResultTestHelper.ExecuteAsync(actionResult, context);

        Assert.Equal(200, statusCode);
        Assert.NotNull(doc.QuerySelector("#sample-item"));

        var oob = Assert.Single(doc.QuerySelectorAll("[hx-swap-oob]"));
        Assert.Equal("beforeend:#toasts", oob.GetAttribute("hx-swap-oob"));
        var toastElements = oob.QuerySelectorAll("[data-toast]").ToList();
        Assert.Equal(2, toastElements.Count);
        Assert.Equal("success", toastElements[0].GetAttribute("data-toast"));
        Assert.Equal("info", toastElements[1].GetAttribute("data-toast"));
    }

    [Fact]
    public async Task Respond_Ok_WithoutHtmx_GetRequest_ReturnsPageAndSetsViewDataToasts()
    {
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: false, httpMethod: "GET");
        var (context, page) = testContext;
        var result = Result.Ok("My Item", Message.Success("ItemLoaded"));

        var actionResult = page.Respond(result, "GetItem", partialViewName: "_SampleItem");

        Assert.IsType<PageResult>(actionResult);
        var toasts = page.ViewData[ToastExtensions.ViewDataKey] as IReadOnlyList<Toast>;
        Assert.NotNull(toasts);
        var toast = Assert.Single(toasts);
        Assert.Equal(MessageSeverity.Success, toast.Severity);
        Assert.Equal("ItemLoaded", toast.Text);
    }

    [Fact]
    public async Task Respond_Ok_WithoutHtmx_PostRequest_RedirectsAndStoresToastsInTempData()
    {
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: false, httpMethod: "POST");
        var (context, page) = testContext;
        var result = Result.Ok("My Item", Message.Success("ItemCreated"));

        var actionResult = page.Respond(result, "CreateItem", partialViewName: "_SampleItem", redirectPage: "/Index");

        var redirect = Assert.IsType<RedirectToPageResult>(actionResult);
        Assert.Equal("/Index", redirect.PageName);

        var tempToasts = ToastExtensions.GetToasts(page.ViewData, page.TempData).ToList();
        var toast = Assert.Single(tempToasts);
        Assert.Equal(MessageSeverity.Success, toast.Severity);
        Assert.Equal("ItemCreated", toast.Text);

        using var layoutDoc = await PartialRenderer.RenderAsync(factory.Services, "/Pages/Shared/_Toasts.cshtml", tempToasts);
        var renderedToast = Assert.Single(layoutDoc.QuerySelectorAll("[data-toast]"));
        Assert.Equal("success", renderedToast.GetAttribute("data-toast"));
    }

    [Fact]
    public async Task Respond_Ok_NonGeneric_WithHtmxAndMessages_RendersOobToasts()
    {
        await using var testContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: true);
        var (context, page) = testContext;
        var result = Result.Ok(Message.Success("OperationCompleted"));

        var actionResult = page.Respond(result, "DoWork");
        var (statusCode, contentType, body, doc) = await ResultTestHelper.ExecuteAsync(actionResult, context);

        Assert.Equal(200, statusCode);
        var oob = Assert.Single(doc.QuerySelectorAll("[hx-swap-oob]"));
        Assert.Equal("beforeend:#toasts", oob.GetAttribute("hx-swap-oob"));
    }
}
