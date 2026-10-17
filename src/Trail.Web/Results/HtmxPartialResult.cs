using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Trail.Web.Toasts;

namespace Trail.Web.Results;

public sealed class HtmxPartialResult(
    string? partialViewName,
    int statusCode,
    object? model = null,
    IReadOnlyList<Toast>? toasts = null) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var httpContext = context.HttpContext;
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "text/html; charset=utf-8";

        var viewEngine = httpContext.RequestServices.GetRequiredService<IRazorViewEngine>();
        var tempDataProvider = httpContext.RequestServices.GetRequiredService<ITempDataProvider>();
        var modelMetadataProvider = httpContext.RequestServices.GetRequiredService<IModelMetadataProvider>();

        await using var writer = new StringWriter();
        var tempData = new TempDataDictionary(httpContext, tempDataProvider);
        var viewData = new ViewDataDictionary(modelMetadataProvider, context.ModelState)
        {
            Model = model
        };

        if (!string.IsNullOrWhiteSpace(partialViewName))
        {
            var view = FindView(viewEngine, context, partialViewName, isMainPage: false);
            var viewContext = new ViewContext(context, view, viewData, tempData, writer, new HtmlHelperOptions());
            await view.RenderAsync(viewContext);
        }

        if (toasts is { Count: > 0 })
        {
            var toastsView = FindView(viewEngine, context, "_Toasts", isMainPage: false);
            var toastsViewData = new ViewDataDictionary<IEnumerable<Toast>>(viewData, toasts);
            var toastsViewContext = new ViewContext(context, toastsView, toastsViewData, tempData, writer, new HtmlHelperOptions());
            await toastsView.RenderAsync(toastsViewContext);
        }

        await httpContext.Response.WriteAsync(writer.ToString());
    }

    private static IView FindView(IRazorViewEngine viewEngine, ActionContext context, string viewName, bool isMainPage)
    {
        if (viewName.StartsWith('/') || viewName.StartsWith('~'))
        {
            var result = viewEngine.GetView(null, viewName, isMainPage);
            if (result.Success && result.View is not null)
            {
                return result.View;
            }
        }

        var findResult = viewEngine.FindView(context, viewName, isMainPage);
        if (findResult.Success && findResult.View is not null)
        {
            return findResult.View;
        }

        var fallbackLocations = new[]
        {
            $"/Pages/Shared/{viewName}.cshtml",
            $"/Pages/{viewName}.cshtml",
            $"/Views/Shared/{viewName}.cshtml"
        };

        foreach (var location in fallbackLocations)
        {
            var getResult = viewEngine.GetView(null, location, isMainPage);
            if (getResult.Success && getResult.View is not null)
            {
                return getResult.View;
            }
        }

        throw new InvalidOperationException($"The view '{viewName}' was not found.");
    }
}
