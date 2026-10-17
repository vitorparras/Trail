using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Trail.Web.Tests.Results;

public static class ResultTestHelper
{
    public static ResultTestContext CreateContext(
        IServiceProvider services,
        bool isHtmx = false,
        string httpMethod = "GET",
        string path = "/test")
    {
        var scope = services.CreateAsyncScope();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider
        };
        httpContext.Request.Method = httpMethod;
        httpContext.Request.Path = path;

        if (isHtmx)
        {
            httpContext.Request.Headers["HX-Request-Type"] = "partial";
        }

        httpContext.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "TestEndpoint"));

        var routeData = new RouteData();
        var actionDescriptor = new PageActionDescriptor();
        var actionContext = new ActionContext(httpContext, routeData, actionDescriptor);
        var modelMetadataProvider = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Mvc.ModelBinding.IModelMetadataProvider>();
        var tempDataProvider = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>();
        var viewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(modelMetadataProvider, actionContext.ModelState);
        var tempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(httpContext, tempDataProvider);
        var pageContext = new PageContext(actionContext)
        {
            ViewData = viewData
        };
        var pageModel = new TestPageModel
        {
            PageContext = pageContext,
            TempData = tempData
        };

        return new ResultTestContext(actionContext, pageModel, scope);
    }

    public static async Task<(int StatusCode, string ContentType, string Body, IHtmlDocument Document)> ExecuteAsync(
        IActionResult result,
        ActionContext actionContext)
    {
        var originalBody = actionContext.HttpContext.Response.Body;
        using var memoryStream = new MemoryStream();
        actionContext.HttpContext.Response.Body = memoryStream;

        await result.ExecuteResultAsync(actionContext);

        memoryStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(memoryStream);
        var body = await reader.ReadToEndAsync();
        actionContext.HttpContext.Response.Body = originalBody;

        var document = new HtmlParser().ParseDocument(body);
        return (
            actionContext.HttpContext.Response.StatusCode,
            actionContext.HttpContext.Response.ContentType ?? string.Empty,
            body,
            document);
    }
}

public sealed class ResultTestContext(ActionContext actionContext, TestPageModel pageModel, AsyncServiceScope scope) : IAsyncDisposable
{
    public ActionContext ActionContext { get; } = actionContext;

    public TestPageModel PageModel { get; } = pageModel;

    public void Deconstruct(out ActionContext context, out TestPageModel page)
    {
        context = ActionContext;
        page = PageModel;
    }

    public ValueTask DisposeAsync() => scope.DisposeAsync();
}

public sealed class TestPageModel : PageModel;
