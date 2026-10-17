using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Trail.Application.Telemetry;
using Trail.Domain.Common;
using Trail.Web.Htmx;
using Trail.Web.Toasts;

namespace Trail.Web.Results;

public static class ResultResponder
{
    public static IActionResult Respond(
        PageModel page,
        Result result,
        string useCase,
        ResultResponseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(useCase);

        RecordTelemetry(page.HttpContext, useCase, result.Status, result.Messages);

        var isHtmx = page.Request.IsHtmxPartialRequest();
        var (toasts, primaryMessage) = TranslateMessages(
            page.HttpContext,
            page.ModelState,
            result.Status,
            result.Messages,
            isHtmx);

        return result.Status switch
        {
            ResultStatus.Ok => HandleOk(page, isHtmx, options, toasts),
            ResultStatus.Invalid => HandleInvalid(page, isHtmx, options, toasts),
            ResultStatus.NotFound => HandleNotFound(isHtmx, primaryMessage, toasts),
            ResultStatus.Conflict => HandleConflict(page, isHtmx, primaryMessage, toasts),
            ResultStatus.Forbidden => HandleForbidden(isHtmx, primaryMessage, toasts),
            _ => throw new ArgumentOutOfRangeException(nameof(result), $"Unsupported result status: {result.Status}")
        };
    }

    public static IActionResult Respond<T>(
        PageModel page,
        Result<T> result,
        string useCase,
        ResultResponseOptions<T>? options = null)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(useCase);

        RecordTelemetry(page.HttpContext, useCase, result.Status, result.Messages);

        var isHtmx = page.Request.IsHtmxPartialRequest();
        var (toasts, primaryMessage) = TranslateMessages(
            page.HttpContext,
            page.ModelState,
            result.Status,
            result.Messages,
            isHtmx);

        return result.Status switch
        {
            ResultStatus.Ok => HandleOkOfT(page, isHtmx, result.Data, options, toasts),
            ResultStatus.Invalid => HandleInvalidOfT(page, isHtmx, options, toasts),
            ResultStatus.NotFound => HandleNotFound(isHtmx, primaryMessage, toasts),
            ResultStatus.Conflict => HandleConflict(page, isHtmx, primaryMessage, toasts),
            ResultStatus.Forbidden => HandleForbidden(isHtmx, primaryMessage, toasts),
            _ => throw new ArgumentOutOfRangeException(nameof(result), $"Unsupported result status: {result.Status}")
        };
    }

    private static IActionResult HandleOk(
        PageModel page,
        bool isHtmx,
        ResultResponseOptions? options,
        IReadOnlyList<Toast> toasts)
    {
        if (isHtmx)
        {
            return new HtmxPartialResult(
                options?.PartialViewName,
                StatusCodes.Status200OK,
                options?.PartialModel,
                toasts);
        }

        if (HttpMethods.IsGet(page.Request.Method))
        {
            ToastExtensions.SetToasts(page.ViewData, toasts);
            return page.Page();
        }

        ToastExtensions.SetToasts(page.TempData, toasts);
        return options?.RedirectPage is not null
            ? page.RedirectToPage(options.RedirectPage, options.RedirectRouteValues)
            : page.RedirectToPage(options?.RedirectRouteValues);
    }

    private static IActionResult HandleOkOfT<T>(
        PageModel page,
        bool isHtmx,
        T data,
        ResultResponseOptions<T>? options,
        IReadOnlyList<Toast> toasts)
        where T : notnull
    {
        if (isHtmx)
        {
            var model = options?.PartialModelFactory?.Invoke(data) ?? data;
            return new HtmxPartialResult(
                options?.PartialViewName,
                StatusCodes.Status200OK,
                model,
                toasts);
        }

        if (HttpMethods.IsGet(page.Request.Method))
        {
            ToastExtensions.SetToasts(page.ViewData, toasts);
            return page.Page();
        }

        ToastExtensions.SetToasts(page.TempData, toasts);
        var routeValues = options?.RedirectRouteValuesFactory?.Invoke(data) ?? options?.RedirectRouteValues;
        return options?.RedirectPage is not null
            ? page.RedirectToPage(options.RedirectPage, routeValues)
            : page.RedirectToPage(routeValues);
    }

    private static IActionResult HandleInvalid(
        PageModel page,
        bool isHtmx,
        ResultResponseOptions? options,
        IReadOnlyList<Toast> toasts)
    {
        if (isHtmx)
        {
            var formPartial = options?.FormPartialViewName ?? options?.PartialViewName;
            return new HtmxPartialResult(
                formPartial,
                StatusCodes.Status422UnprocessableEntity,
                options?.PartialModel,
                toasts);
        }

        page.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return page.Page();
    }

    private static IActionResult HandleInvalidOfT<T>(
        PageModel page,
        bool isHtmx,
        ResultResponseOptions<T>? options,
        IReadOnlyList<Toast> toasts)
        where T : notnull
    {
        if (isHtmx)
        {
            var formPartial = options?.FormPartialViewName ?? options?.PartialViewName;
            return new HtmxPartialResult(
                formPartial,
                StatusCodes.Status422UnprocessableEntity,
                null,
                toasts);
        }

        page.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return page.Page();
    }

    private static IActionResult HandleNotFound(bool isHtmx, string? message, IReadOnlyList<Toast> toasts) =>
        isHtmx
            ? new HtmxPartialResult("_NotFound", StatusCodes.Status404NotFound, new NotFoundViewModel(message), toasts)
            : new StatusViewResult("NotFound", StatusCodes.Status404NotFound, new NotFoundViewModel(message), toasts);

    private static IActionResult HandleConflict(PageModel page, bool isHtmx, string? message, IReadOnlyList<Toast> toasts)
    {
        var reloadUrl = $"{page.Request.PathBase}{page.Request.Path}{page.Request.QueryString}";
        return isHtmx
            ? new HtmxPartialResult("_Conflict", StatusCodes.Status409Conflict, new ConflictViewModel(message, reloadUrl), toasts)
            : new StatusViewResult("Conflict", StatusCodes.Status409Conflict, new ConflictViewModel(message, reloadUrl), toasts);
    }

    private static IActionResult HandleForbidden(bool isHtmx, string? message, IReadOnlyList<Toast> toasts) =>
        isHtmx
            ? new HtmxPartialResult("_Forbidden", StatusCodes.Status403Forbidden, new ForbiddenViewModel(message), toasts)
            : new StatusViewResult("Forbidden", StatusCodes.Status403Forbidden, new ForbiddenViewModel(message), toasts);

    private static void RecordTelemetry(
        HttpContext httpContext,
        string useCase,
        ResultStatus status,
        IReadOnlyList<Message> messages)
    {
        var activity = Activity.Current;
        if (activity is not null)
        {
            activity.SetTag("trail.use_case", useCase);
            activity.SetTag("trail.result.status", status.ToString());
            if (messages.Count > 0)
            {
                activity.SetTag("trail.result.message_codes", messages.Select(m => m.Code).ToArray());
            }
        }

        var metrics = httpContext.RequestServices.GetService<ApplicationMetrics>();
        metrics?.RecordResult(useCase, status);
    }

    private static (IReadOnlyList<Toast> Toasts, string? PrimaryMessage) TranslateMessages(
        HttpContext httpContext,
        ModelStateDictionary modelState,
        ResultStatus status,
        IReadOnlyList<Message> messages,
        bool isHtmx)
    {
        var localizer = httpContext.RequestServices.GetService<IStringLocalizer<SharedResource>>();
        var toasts = new List<Toast>();
        string? primaryMessage = null;

        foreach (var message in messages)
        {
            var localized = localizer?[message.Code];
            var text = localized is not null && !localized.ResourceNotFound
                ? localized.Value
                : message.Code;

            primaryMessage ??= text;

            if (status is ResultStatus.Invalid)
            {
                if (!string.IsNullOrWhiteSpace(message.Target))
                {
                    modelState.AddModelError(message.Target, text);
                }
                else
                {
                    modelState.AddModelError(string.Empty, text);
                    if (isHtmx)
                    {
                        toasts.Add(new Toast(message.Severity, text));
                    }
                }
            }
            else
            {
                toasts.Add(new Toast(message.Severity, text));
            }
        }

        return (toasts, primaryMessage);
    }
}
