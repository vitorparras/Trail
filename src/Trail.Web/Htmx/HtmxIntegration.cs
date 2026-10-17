using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Trail.Web.Htmx;

public static class HtmxIntegration
{
    public const string AntiforgeryHeaderName = "X-CSRF-TOKEN";

    public const string RequestTypeHeaderName = "HX-Request-Type";

    public const string PartialRequestType = "partial";

    public static string ClientConfiguration { get; } = JsonSerializer.Serialize(new
    {
        includeIndicatorCSS = false,
        noSwap = new[] { 204, 304 },
    });

    public static IServiceCollection AddHtmx(this IServiceCollection services)
    {
        services.AddAntiforgery(options => options.HeaderName = AntiforgeryHeaderName);
        services.Configure<MvcOptions>(options => options.Filters.Add(new HtmxVaryHeaderFilter()));

        return services;
    }

    public static bool IsHtmxPartialRequest(this HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return string.Equals(request.Headers[RequestTypeHeaderName], PartialRequestType, StringComparison.Ordinal);
    }
}
