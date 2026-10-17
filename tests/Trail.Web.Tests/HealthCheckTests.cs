using System.Net;

namespace Trail.Web.Tests;

public sealed class HealthCheckTests(TrailWebApplicationFactory factory) : IClassFixture<TrailWebApplicationFactory>
{
    [Fact]
    public async Task Get_HealthLive_ReturnsOkAndHealthy()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Healthy", body);
    }

    [Fact]
    public async Task Get_HealthReady_ReturnsOkAndHealthy()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Healthy", body);
    }

    [Fact]
    public async Task Get_HealthReady_WhenDatabaseIsUnreachable_ReturnsServiceUnavailable()
    {
        using var unhealthyFactory = new UnreachableDatabaseFactory();
        using var client = unhealthyFactory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Unhealthy", body, StringComparison.Ordinal);
    }

    private sealed class UnreachableDatabaseFactory : TrailWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "PostgreSql",
                    ["ConnectionStrings:Default"] = "Host=127.0.0.1;Port=54321;Database=unreachable;Username=user;Password=pass;Timeout=1;CommandTimeout=1",
                });
            });
        }
    }
}
