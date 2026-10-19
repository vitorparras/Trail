using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Trail.Web.Security;
using Trail.Web.Tests.Accounts;

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

    [Theory]
    [InlineData("/Account/Register", "Input.Email")]
    [InlineData("/Account/ResendConfirmation", "Email")]
    public async Task AccountEmailPage_WhenTheAuthLimitIsExceeded_Returns429TooManyRequests(string path, string emailField)
    {
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Registration:Mode"] = "Open",
                ["Security:AuthRateLimitPermitLimit"] = "2",
            })));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var form = new Dictionary<string, string> { [emailField] = "victim@example.com" };

        var (page, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, path);
        using (page)
        {
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        }

        using var allowed = await AccountTestHelper.PostFormWithAntiforgeryAsync(client, path, form, csrfHeader, csrfToken);
        using var rejected = await AccountTestHelper.PostFormWithAntiforgeryAsync(client, path, form, csrfHeader, csrfToken);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
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
                next(app);
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/test-auth-endpoint", () => "ok")
                        .RequireRateLimiting(RateLimitingExtensions.AuthPolicyName);
                });
            };
        }
    }
}
