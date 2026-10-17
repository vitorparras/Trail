using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Localization;

namespace Trail.Web.Security;

public static class RateLimitingExtensions
{
    public const string AuthPolicyName = "auth";

    public static IServiceCollection AddTrailRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                    context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    context.HttpContext.Response.Headers.RetryAfter = "60";
                }

                var localizer = context.HttpContext.RequestServices.GetService<IStringLocalizer<SharedResource>>();
                var message = localizer?["RateLimitExceeded"].Value ?? "Muitas tentativas. Tente novamente mais tarde.";
                context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
                await context.HttpContext.Response.WriteAsync(message, cancellationToken);
            };

            options.AddPolicy(AuthPolicyName, httpContext =>
            {
                var config = httpContext.RequestServices.GetService<IConfiguration>() ?? configuration;
                var permitLimit = config.GetValue<int?>("Security:AuthRateLimitPermitLimit") ?? 10;
                var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(remoteIp, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromSeconds(60),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
            });
        });

        return services;
    }
}
