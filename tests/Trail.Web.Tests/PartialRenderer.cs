using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Trail.Web.Tests;

internal static class PartialRenderer
{
    public static async Task<IHtmlDocument> RenderAsync(IServiceProvider services, string path, object model)
    {
        await using var scope = services.CreateAsyncScope();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var view = scope.ServiceProvider.GetRequiredService<IRazorViewEngine>().GetView(null, path, isMainPage: false).View
            ?? throw new InvalidOperationException($"The view {path} was not found.");
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
        var tempData = new TempDataDictionary(httpContext, scope.ServiceProvider.GetRequiredService<ITempDataProvider>());
        await using var writer = new StringWriter();

        await view.RenderAsync(new ViewContext(actionContext, view, viewData, tempData, writer, new HtmlHelperOptions()));

        return new HtmlParser().ParseDocument(writer.ToString());
    }
}
