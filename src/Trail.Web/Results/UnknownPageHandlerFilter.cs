using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Domain.Common;

namespace Trail.Web.Results;

internal sealed class UnknownPageHandlerFilter : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (context.HandlerMethod is null
            && context.ActionDescriptor.HandlerMethods.Count > 0
            && context.HandlerInstance is PageModel page)
        {
            context.Result = page.Respond(Result.NotFound(), "page.unknown_handler");
            return;
        }

        await next();
    }
}
