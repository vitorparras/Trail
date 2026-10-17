using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Trail.Web.Htmx;

public static class HtmxIntegration
{
    public const string AntiforgeryHeaderName = "X-CSRF-TOKEN";

    public const string RequestTypeHeaderName = "HX-Request-Type";

    public const string PartialRequestType = "partial";

    public const string RedirectHeaderName = "HX-Redirect";

    public const string CurrentUrlHeaderName = "HX-Current-URL";

    public static string ClientConfiguration { get; } = JsonSerializer.Serialize(new
    {
        includeIndicatorCSS = true,
        noSwap = new[] { 204, 304 },
    });

    public static IServiceCollection AddHtmx(this IServiceCollection services)
    {
        services.AddAntiforgery(options => options.HeaderName = AntiforgeryHeaderName);
        services.Configure<MvcOptions>(options => options.Filters.Add(new HtmxVaryHeaderFilter()));
        services.ConfigureAll<CookieAuthenticationOptions>(options =>
        {
            var redirectToLogin = options.Events.OnRedirectToLogin;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.Headers.Append(HeaderNames.Vary, RequestTypeHeaderName);
                return context.Request.IsHtmxRequest()
                    ? AskHtmxToNavigateToLogin(context)
                    : redirectToLogin(context);
            };
        });

        return services;
    }

    public static bool IsHtmxRequest(this HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Headers.ContainsKey(RequestTypeHeaderName);
    }

    public static bool IsHtmxPartialRequest(this HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return string.Equals(request.Headers[RequestTypeHeaderName], PartialRequestType, StringComparison.Ordinal);
    }

    private static Task AskHtmxToNavigateToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        var request = context.Request;
        var returnUrl = LocalCurrentUrl(request) ?? $"{request.PathBase}{request.Path}";
        var loginUrl = request.PathBase
            + context.Options.LoginPath
            + QueryString.Create(context.Options.ReturnUrlParameter, returnUrl);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers[RedirectHeaderName] = loginUrl.ToString();
        return Task.CompletedTask;
    }

    private static string? LocalCurrentUrl(HttpRequest request)
    {
        if (!Uri.TryCreate(request.Headers[CurrentUrlHeaderName].ToString(), UriKind.Absolute, out var currentUrl)
            || !string.Equals(currentUrl.Scheme, request.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(currentUrl.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase)
            || currentUrl.Port != (request.Host.Port ?? (request.IsHttps ? 443 : 80)))
        {
            return null;
        }

        var path = PathString.FromUriComponent(currentUrl);
        var pathAndQuery = currentUrl.PathAndQuery;
        var isLocal = pathAndQuery.StartsWith('/')
            && !pathAndQuery.StartsWith("//", StringComparison.Ordinal)
            && !pathAndQuery.StartsWith("/\\", StringComparison.Ordinal)
            && (!request.PathBase.HasValue || path.StartsWithSegments(request.PathBase));

        return isLocal ? pathAndQuery : null;
    }
}
