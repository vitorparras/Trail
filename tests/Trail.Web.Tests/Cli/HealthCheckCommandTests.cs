using System.Net;
using Trail.Web.Cli;

namespace Trail.Web.Tests.Cli;

public sealed class HealthCheckCommandTests
{
    [Fact]
    public void ResolveTargetUri_WhenVariablesAreNullOrWhitespace_ReturnsDefaultUri()
    {
        var uri = HealthCheckCommand.ResolveTargetUri(null, null);

        Assert.Equal("http://127.0.0.1:8080/health/ready", uri.AbsoluteUri);
    }

    [Theory]
    [InlineData("http://+:8080", "http://127.0.0.1:8080/health/ready")]
    [InlineData("http://*:8080", "http://127.0.0.1:8080/health/ready")]
    [InlineData("http://0.0.0.0:8080", "http://127.0.0.1:8080/health/ready")]
    [InlineData("http://[::]:8080", "http://127.0.0.1:8080/health/ready")]
    [InlineData("https://+:8443", "https://127.0.0.1:8443/health/ready")]
    [InlineData("http://127.0.0.1:5000", "http://127.0.0.1:5000/health/ready")]
    [InlineData("http://localhost:5000/health/ready", "http://localhost:5000/health/ready")]
    public void ResolveTargetUri_ReplacesWildcardHosts(string urls, string expected)
    {
        var uri = HealthCheckCommand.ResolveTargetUri(urls, null);

        Assert.Equal(expected, uri.AbsoluteUri);
    }

    [Fact]
    public void ResolveTargetUri_WhenMultipleUrlsProvided_PicksFirst()
    {
        var uri = HealthCheckCommand.ResolveTargetUri("http://+:8080;https://+:8443", null);

        Assert.Equal("http://127.0.0.1:8080/health/ready", uri.AbsoluteUri);
    }

    [Theory]
    [InlineData("9000", "http://127.0.0.1:9000/health/ready")]
    [InlineData("9000;9001", "http://127.0.0.1:9000/health/ready")]
    public void ResolveTargetUri_UsesHttpPortsWhenUrlsNotProvided(string httpPorts, string expected)
    {
        var uri = HealthCheckCommand.ResolveTargetUri(null, httpPorts);

        Assert.Equal(expected, uri.AbsoluteUri);
    }

    [Fact]
    public async Task RunAsync_WhenTargetResponds200Ok_ReturnsZero()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);

        var exitCode = await HealthCheckCommand.RunAsync(
            args: ["--healthcheck"],
            httpMessageHandler: handler,
            getEnvironmentVariable: _ => "http://127.0.0.1:8080",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal(new Uri("http://127.0.0.1:8080/health/ready"), handler.LastRequestUri);
        Assert.Equal(HttpMethod.Get, handler.LastRequestMethod);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task RunAsync_WhenTargetRespondsNon200_ReturnsOne(HttpStatusCode statusCode)
    {
        var handler = new StubHttpMessageHandler(statusCode);

        var exitCode = await HealthCheckCommand.RunAsync(
            args: ["--healthcheck"],
            httpMessageHandler: handler,
            getEnvironmentVariable: _ => "http://127.0.0.1:8080",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_WhenRequestThrowsException_ReturnsOne()
    {
        var handler = new FaultyHttpMessageHandler();

        var exitCode = await HealthCheckCommand.RunAsync(
            args: ["--healthcheck"],
            httpMessageHandler: handler,
            getEnvironmentVariable: _ => "http://127.0.0.1:8080",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
    }

    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        public HttpMethod? LastRequestMethod { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastRequestMethod = request.Method;
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }

    private sealed class FaultyHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Connection refused");
    }
}
