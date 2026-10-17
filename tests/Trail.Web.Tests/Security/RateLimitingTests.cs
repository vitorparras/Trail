using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.RateLimiting;
using Trail.Web.Security;

namespace Trail.Web.Tests.Security;

public sealed class RateLimitingTests(TrailWebApplicationFactory baseFactory) : IClassFixture<TrailWebApplicationFactory>
{
    [Fact]
    public async Task AuthRateLimit_WhenExceeded_Returns429TooManyRequestsWithRetryAfterHeader()
    {
        const int permitLimit = 2;

        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:AuthRateLimitPermitLimit"] = permitLimit.ToString(CultureInfo.InvariantCulture),
                });
            });

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IStartupFilter>(new AuthRateLimitEndpointStartupFilter());
            });
        });

        using var client = factory.CreateClient();

        for (var i = 0; i < permitLimit; i++)
        {
            using var okResponse = await client.GetAsync("/test-auth-endpoint", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);
        }

        using var rejectedResponse = await client.GetAsync("/test-auth-endpoint", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
        Assert.NotNull(rejectedResponse.Headers.RetryAfter);

        var body = await rejectedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Muitas tentativas", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonAuthEndpoint_IsNotSubjectToAuthRateLimit()
    {
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:AuthRateLimitPermitLimit"] = "1",
                });
            });

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IStartupFilter>(new AuthRateLimitEndpointStartupFilter());
            });
        });

        using var client = factory.CreateClient();

        using var firstResponse = await client.GetAsync("/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        using var secondResponse = await client.GetAsync("/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
    }

    private sealed class AuthRateLimitEndpointStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    if (context.Request.Path == "/test-auth-endpoint")
                    {
                        context.SetEndpoint(new Endpoint(
                            httpContext =>
                            {
                                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                                return Task.CompletedTask;
                            },
                            new EndpointMetadataCollection(new EnableRateLimitingAttribute(RateLimitingExtensions.AuthPolicyName)),
                            "AuthRateLimitTestEndpoint"));
                    }

                    await nextMiddleware(context);
                });

                next(app);
            };
        }
    }
}
