using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Trail.Web.Forms;

namespace Trail.Web.Tests.Forms;

public sealed class UnrenderedModelStateErrorException(IReadOnlyList<string> messages)
    : Exception($"These ModelState errors were not rendered anywhere in the response: {string.Join(" | ", messages)}")
{
    public IReadOnlyList<string> Messages { get; } = messages;
}

internal static partial class RenderedErrorsGuard
{
    private const string ModelStateItemKey = "Trail.Tests.ModelState";

    private static readonly int[] StatusesThatDropFormErrorsByDesign =
    [
        StatusCodes.Status401Unauthorized,
        StatusCodes.Status403Forbidden,
        StatusCodes.Status404NotFound,
        StatusCodes.Status409Conflict,
    ];

    public static void Register(IServiceCollection services)
    {
        services.AddTransient<IStartupFilter, GuardStartupFilter>();
        services.Configure<MvcOptions>(options => options.Filters.Add<ModelStateCaptureFilter>());
    }

    [GeneratedRegex("\\s+")]
    private static partial Regex Whitespace();

    private static string Normalize(string text) => Whitespace().Replace(text, " ").Trim();

    private static string RenderedText(string html) =>
        Normalize(new HtmlParser().ParseDocument(html).DocumentElement.TextContent);

    private sealed class ModelStateCaptureFilter : IAsyncPageFilter
    {
        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
        {
            context.HttpContext.Items[ModelStateItemKey] = context.ModelState;
            return Task.CompletedTask;
        }

        public Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next) =>
            next();
    }

    private sealed class GuardStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, pipeline) =>
            {
                var original = context.Response.Body;
                using var buffer = new MemoryStream();
                context.Response.Body = buffer;

                try
                {
                    await pipeline();
                    EnsureEveryErrorIsRendered(context, buffer);
                }
                finally
                {
                    context.Response.Body = original;
                }

                buffer.Position = 0;
                await buffer.CopyToAsync(original);
            });

            next(app);
        };

        private static void EnsureEveryErrorIsRendered(HttpContext context, MemoryStream buffer)
        {
            var mismatches = RenderedFieldErrors.Mismatches(context);
            if (mismatches.Count > 0)
            {
                throw new InvalidOperationException(string.Join(" | ", mismatches));
            }

            var status = context.Response.StatusCode;
            if (context.Items[ModelStateItemKey] is not ModelStateDictionary modelState
                || StatusesThatDropFormErrorsByDesign.Contains(status)
                || status >= StatusCodes.Status500InternalServerError)
            {
                return;
            }

            var messages = modelState.Values
                .SelectMany(entry => entry.Errors)
                .Select(error => error.ErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(Normalize)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (messages.Count == 0)
            {
                return;
            }

            var isRedirect = status is >= 300 and < 400;
            var isHtml = context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true;
            if (!isRedirect && !isHtml)
            {
                return;
            }

            var rendered = isRedirect ? string.Empty : RenderedText(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
            var missing = messages.Where(message => !rendered.Contains(message, StringComparison.Ordinal)).ToList();
            if (missing.Count > 0)
            {
                throw new UnrenderedModelStateErrorException(missing);
            }
        }
    }
}
