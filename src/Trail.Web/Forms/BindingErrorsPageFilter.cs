using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Domain.Common;
using Trail.Web.Htmx;
using Trail.Web.Results;
using Trail.Web.Toasts;

namespace Trail.Web.Forms;

[AttributeUsage(AttributeTargets.Method)]
public sealed class BindingErrorPartialAttribute(string viewName) : Attribute
{
    public string ViewName { get; } = viewName;

    public string? ModelProperty { get; init; }
}

public sealed partial class BindingErrorsPageFilter : IAsyncPageFilter, IOrderedFilter
{
    private const string ItemKey = "Trail.Forms.BindingErrors";
    private const int MaxModelDepth = 4;

    public int Order => int.MaxValue;

    public static IReadOnlyList<string> UnrenderedMessages(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.Items[ItemKey] is not IReadOnlyList<KeyValuePair<string, string>> errors)
        {
            return [];
        }

        return [.. errors
            .Where(error => !RenderedFieldErrors.IsRendered(httpContext, error.Key))
            .Select(error => error.Value)
            .Distinct(StringComparer.Ordinal)];
    }

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (IsSafeMethod(context.HttpContext.Request.Method) || context.ModelState.IsValid || context.HandlerInstance is not PageModel page)
        {
            await next();
            return;
        }

        var errors = context.ModelState
            .SelectMany(entry => entry.Value!.Errors.Select(error => KeyValuePair.Create(entry.Key, error.ErrorMessage)))
            .Where(error => !string.IsNullOrWhiteSpace(error.Value))
            .ToList();

        var booleanFields = BooleanFields(context);
        foreach (var (key, entry) in context.ModelState.Where(entry => entry.Value!.Errors.Count > 0).Select(entry => (entry.Key, entry.Value!)))
        {
            if (booleanFields.IsMatch(key))
            {
                entry.RawValue = null;
                entry.AttemptedValue = null;
            }
        }

        context.Result = Respond(context, page, errors);
    }

    private static BooleanFieldKeys BooleanFields(PageHandlerExecutingContext context)
    {
        var provider = context.HttpContext.RequestServices.GetRequiredService<IModelMetadataProvider>();
        var keys = new BooleanFieldKeys();
        var roots = context.ActionDescriptor.BoundProperties
            .Concat(context.HandlerMethod?.Parameters ?? []);

        foreach (var root in roots)
        {
            Collect(provider.GetMetadataForType(root.ParameterType), root.Name, keys, depth: 0);
        }

        return keys;
    }

    private static void Collect(ModelMetadata metadata, string key, BooleanFieldKeys keys, int depth)
    {
        if ((Nullable.GetUnderlyingType(metadata.ModelType) ?? metadata.ModelType) == typeof(bool))
        {
            keys.Add(key);
            return;
        }

        if (depth >= MaxModelDepth)
        {
            return;
        }

        if (metadata.IsEnumerableType)
        {
            if (metadata.ElementMetadata is { } element)
            {
                Collect(element, key + "[]", keys, depth + 1);
            }

            return;
        }

        if (metadata.IsComplexType)
        {
            foreach (var property in metadata.Properties)
            {
                Collect(property, $"{key}.{property.Name}", keys, depth + 1);
            }
        }
    }

    private sealed class BooleanFieldKeys
    {
        private readonly HashSet<string> _keys = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string key) => _keys.Add(key);

        public bool IsMatch(string modelStateKey) =>
            _keys.Contains(IndexPattern().Replace(modelStateKey, "[]"));
    }

    [GeneratedRegex("\\[[^\\]]*\\]")]
    private static partial Regex IndexPattern();

    private static IActionResult Respond(PageHandlerExecutingContext context, PageModel page, List<KeyValuePair<string, string>> errors)
    {
        if (context.HttpContext.Request.IsHtmxPartialRequest())
        {
            var partial = context.HandlerMethod?.MethodInfo.GetCustomAttribute<BindingErrorPartialAttribute>();
            if (partial is not null)
            {
                var model = partial.ModelProperty is null ? null : page.GetType().GetProperty(partial.ModelProperty)?.GetValue(page);
                return new HtmxPartialResult(partial.ViewName, StatusCodes.Status422UnprocessableEntity, model);
            }

            var toasts = errors
                .Select(error => error.Value)
                .Distinct(StringComparer.Ordinal)
                .Select(message => new Toast(MessageSeverity.Error, message))
                .ToList();
            return new HtmxPartialResult(null, StatusCodes.Status422UnprocessableEntity, null, toasts);
        }

        context.HttpContext.Items[ItemKey] = errors;
        context.HttpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return page.Page();
    }

    private static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);
}
