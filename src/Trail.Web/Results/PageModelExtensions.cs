using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Domain.Common;

namespace Trail.Web.Results;

public static class PageModelExtensions
{
    public static IActionResult Respond(
        this PageModel page,
        Result result,
        string useCase,
        string? partialViewName = null,
        string? redirectPage = null,
        string? formPartialViewName = null)
    {
        return ResultResponder.Respond(page, result, useCase, new ResultResponseOptions
        {
            PartialViewName = partialViewName,
            RedirectPage = redirectPage,
            FormPartialViewName = formPartialViewName,
        });
    }

    public static IActionResult Respond(
        this PageModel page,
        Result result,
        string useCase,
        Action<ResultResponseOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new ResultResponseOptions();
        configure(options);
        return ResultResponder.Respond(page, result, useCase, options);
    }

    public static IActionResult Respond<T>(
        this PageModel page,
        Result<T> result,
        string useCase,
        string? partialViewName = null,
        string? redirectPage = null,
        string? formPartialViewName = null)
        where T : notnull
    {
        return ResultResponder.Respond(page, result, useCase, new ResultResponseOptions<T>
        {
            PartialViewName = partialViewName,
            RedirectPage = redirectPage,
            FormPartialViewName = formPartialViewName,
        });
    }

    public static IActionResult Respond<T>(
        this PageModel page,
        Result<T> result,
        string useCase,
        Action<ResultResponseOptions<T>> configure)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new ResultResponseOptions<T>();
        configure(options);
        return ResultResponder.Respond(page, result, useCase, options);
    }
}
