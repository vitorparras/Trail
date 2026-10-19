using System.Collections;
using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using OpenTelemetry.Trace;
using Trail.Application.Accounts;
using Trail.Web.Tests.Results;

namespace Trail.Web.Tests.Observability;

public sealed class LinkTokenTelemetryTests(TrailWebApplicationFactory baseFactory) : IClassFixture<TrailWebApplicationFactory>
{
    private const string HostingDiagnosticsLevel = "Logging:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics";
    private const string AspNetCoreLevel = "Logging:LogLevel:Microsoft.AspNetCore";

    private static readonly string[] LinkTemplates =
    [
        "/Account/ResetPassword?userId={0}&token={1}",
        "/Account/ConfirmEmail?userId={0}&token={1}",
        "/Account/Register?inviteToken={1}",
        "/Account/AcceptInvite?token={1}",
    ];

    [Fact]
    public async Task LinksWithTokens_UnderTheShippedConfiguration_KeepTheTokensOutOfEverySpanAndLogEntry()
    {
        var telemetry = await RequestLinksAsync(new Dictionary<string, string?>());

        Assert.Equal(LinkTemplates.Length, telemetry.ServerSpanQueries.Count);
        Assert.All(telemetry.ServerSpanQueries, query => Assert.Contains("token=", query, StringComparison.OrdinalIgnoreCase));
        Assert.All(telemetry.Tokens, token =>
        {
            Assert.DoesNotContain(telemetry.Spans, entry => entry.Contains(token, StringComparison.Ordinal));
            Assert.DoesNotContain(telemetry.Logs, entry => entry.Contains(token, StringComparison.Ordinal));
        });
    }

    [Fact]
    public async Task LinksWithTokens_WithMicrosoftAspNetCoreLoweredToInformation_KeepTheTokensOutOfTheLogs()
    {
        var telemetry = await RequestLinksAsync(new Dictionary<string, string?> { [AspNetCoreLevel] = "Information" });

        Assert.Contains(telemetry.Logs, entry => entry.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal));
        Assert.All(telemetry.Tokens, token => Assert.DoesNotContain(telemetry.Logs, entry => entry.Contains(token, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task LinksWithTokens_WithTheHostingDiagnosticsAtInformation_ExposeTheTokensInTheLogs()
    {
        var telemetry = await RequestLinksAsync(new Dictionary<string, string?> { [HostingDiagnosticsLevel] = "Information" });

        Assert.All(telemetry.Tokens, token => Assert.Contains(telemetry.Logs, entry => entry.Contains(token, StringComparison.Ordinal)));
    }

    private async Task<RecordedTelemetry> RequestLinksAsync(IReadOnlyDictionary<string, string?> configuration)
    {
        var endedActivities = new EndedActivityRecorder();
        var serverSpans = new ServerSpanRecorder();
        var logs = new RecordingLoggerProvider();

        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(configuration));
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<ILoggerProvider>(logs);
                services.AddOpenTelemetry().WithTracing(tracing => tracing
                    .AddProcessor(endedActivities)
                    .AddProcessor(serverSpans));
            });
        });

        var userId = await CreateUnconfirmedUserAsync(factory);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

        var tokens = new List<string>();
        var traces = new List<TracedRequest>();
        foreach (var template in LinkTemplates)
        {
            var token = $"fake-link-token-{Guid.NewGuid():N}";
            tokens.Add(token);

            using var request = new HttpRequestMessage(HttpMethod.Get, string.Format(CultureInfo.InvariantCulture, template, userId, token));
            var trace = serverSpans.Trace(request);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.True((int)response.StatusCode < 500, $"{template}: {(int)response.StatusCode}");

            await trace.ServerSpanEnded;
            traces.Add(trace);
        }

        var traceIds = traces.Select(trace => trace.TraceId).ToHashSet();
        var spans = endedActivities.Ended.Where(activity => traceIds.Contains(activity.TraceId)).ToList();

        return new RecordedTelemetry(
            tokens,
            [.. spans.SelectMany(Describe)],
            [.. logs.Logs.SelectMany(Describe)],
            [.. spans.Where(activity => activity.Kind == ActivityKind.Server).Select(activity => activity.GetTagItem("url.query") as string ?? string.Empty)]);
    }

    private static async Task<Guid> CreateUnconfirmedUserAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var gateway = scope.ServiceProvider.GetRequiredService<IAccountIdentityGateway>();

        var created = await gateway.CreateUserAsync($"link-{Guid.NewGuid():N}@trail.local", "LinkPass123!", isAdmin: false, emailConfirmed: false, TestContext.Current.CancellationToken);
        Assert.True(created.IsOk);

        return created.Data.Value;
    }

    private static IEnumerable<string> Describe(Activity activity)
    {
        var name = activity.OperationName;

        yield return $"{name} display name: {activity.DisplayName}";
        yield return $"{name} status: {activity.Status} {activity.StatusDescription}";

        foreach (var (key, value) in activity.TagObjects)
        {
            yield return $"{name} tag {key}: {Flatten(value)}";
        }

        foreach (var activityEvent in activity.Events)
        {
            yield return $"{name} event: {activityEvent.Name}";
            foreach (var (key, value) in activityEvent.Tags)
            {
                yield return $"{name} event {activityEvent.Name} tag {key}: {Flatten(value)}";
            }
        }

        foreach (var (key, value) in activity.Baggage)
        {
            yield return $"{name} baggage {key}: {value}";
        }
    }

    private static IEnumerable<string> Describe(RecordedLog log)
    {
        var name = $"{log.Category}[{log.EventId.Id}]";

        yield return $"{name} message: {log.Message}";
        yield return $"{name} exception: {log.Exception}";

        foreach (var (key, value) in log.State)
        {
            yield return $"{name} state {key}: {Flatten(value)}";
        }

        foreach (var scope in log.Scopes ?? [])
        {
            yield return $"{name} scope: {scope}";
        }
    }

    private static string Flatten(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        IEnumerable values => string.Join(",", values.Cast<object?>().Select(Flatten)),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    private sealed record RecordedTelemetry(
        IReadOnlyList<string> Tokens,
        IReadOnlyList<string> Spans,
        IReadOnlyList<string> Logs,
        IReadOnlyList<string> ServerSpanQueries);
}
