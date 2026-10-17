using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace Trail.Web.Htmx;

internal sealed class HtmxVaryHeaderFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.HttpContext.Response.Headers.Append(HeaderNames.Vary, HtmxIntegration.RequestTypeHeaderName);
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
