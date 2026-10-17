using System.Net;

namespace Trail.Web.Cli;

public static class HealthCheckCommand
{
    public const string DefaultTargetUrl = "http://127.0.0.1:8080/health/ready";
    public const string HealthReadyPath = "/health/ready";

    public static Uri ResolveTargetUri(string? urlsEnvironmentVariable, string? httpPortsEnvironmentVariable)
    {
        if (!string.IsNullOrWhiteSpace(urlsEnvironmentVariable))
        {
            var firstUrl = urlsEnvironmentVariable
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(firstUrl))
            {
                return NormalizeAndBuildUri(firstUrl);
            }
        }

        if (!string.IsNullOrWhiteSpace(httpPortsEnvironmentVariable))
        {
            var firstPort = httpPortsEnvironmentVariable
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (int.TryParse(firstPort, out var port) && port is > 0 and <= 65535)
            {
                return new Uri($"http://127.0.0.1:{port}{HealthReadyPath}");
            }
        }

        return new Uri(DefaultTargetUrl);
    }

    public static async Task<int> RunAsync(
        string[]? args = null,
        HttpMessageHandler? httpMessageHandler = null,
        Func<string, string?>? getEnvironmentVariable = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
            var urls = getEnvironmentVariable("ASPNETCORE_URLS");
            var httpPorts = getEnvironmentVariable("ASPNETCORE_HTTP_PORTS");
            var targetUri = ResolveTargetUri(urls, httpPorts);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            using var client = httpMessageHandler is null
                ? new HttpClient()
                : new HttpClient(httpMessageHandler, disposeHandler: false);

            using var response = await client.GetAsync(targetUri, linkedCts.Token);
            return response.StatusCode == HttpStatusCode.OK ? 0 : 1;
        }
        catch
        {
            return 1;
        }
    }

    private static Uri NormalizeAndBuildUri(string rawUrl)
    {
        var url = rawUrl;
        if (!url.Contains("://", StringComparison.Ordinal))
        {
            url = $"http://{url}";
        }

        var schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
        var scheme = url[..schemeEnd];
        var afterScheme = url[(schemeEnd + 3)..];

        string host;
        int? port = null;
        var path = HealthReadyPath;

        var slashIndex = afterScheme.IndexOf('/');
        var hostAndPort = slashIndex >= 0 ? afterScheme[..slashIndex] : afterScheme;
        var existingPath = slashIndex >= 0 ? afterScheme[slashIndex..] : string.Empty;

        if (hostAndPort.StartsWith('[') && hostAndPort.Contains(']'))
        {
            var closingBracket = hostAndPort.IndexOf(']');
            host = hostAndPort[..(closingBracket + 1)];
            var afterHost = hostAndPort[(closingBracket + 1)..];
            if (afterHost.StartsWith(':') && int.TryParse(afterHost[1..], out var parsedPort))
            {
                port = parsedPort;
            }
        }
        else
        {
            var colonIndex = hostAndPort.LastIndexOf(':');
            if (colonIndex >= 0 && int.TryParse(hostAndPort[(colonIndex + 1)..], out var parsedPort))
            {
                host = hostAndPort[..colonIndex];
                port = parsedPort;
            }
            else
            {
                host = hostAndPort;
            }
        }

        if (host is "*" or "+" or "0.0.0.0" or "[::]" or "::")
        {
            host = "127.0.0.1";
        }

        if (!string.IsNullOrEmpty(existingPath) && existingPath != "/")
        {
            path = existingPath.EndsWith(HealthReadyPath, StringComparison.OrdinalIgnoreCase)
                ? existingPath
                : $"{existingPath.TrimEnd('/')}{HealthReadyPath}";
        }

        var builder = new UriBuilder(scheme, host)
        {
            Path = path
        };

        if (port.HasValue)
        {
            builder.Port = port.Value;
        }

        return builder.Uri;
    }
}
