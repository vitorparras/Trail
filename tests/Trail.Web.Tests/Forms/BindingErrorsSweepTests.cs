using System.Collections;
using System.Net;
using System.Text;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trail.Application.Accounts;
using Trail.Application.Planning;
using Trail.Domain.Accounts;
using Trail.Domain.Common;
using Trail.Domain.Planning;
using Trail.Web.Htmx;
using Trail.Web.Tests.Accounts;
using Trail.Web.Tests.Planning;

namespace Trail.Web.Tests.Forms;

public sealed class BindingErrorsSweepTests : IDisposable
{
    private const string Unparsable = "not-a-value";
    private const int MaxDepth = 4;

    private readonly TestPlanningStore _store = new();
    private readonly TestAccountIdentityGateway _gateway = new();
    private readonly TestUserSettingsStore _settings = new();
    private readonly TestEmailSender _email = new();
    private readonly TestCurrentUser _currentUser = new() { IsAuthenticated = true, IsAdmin = true };
    private readonly TrailWebApplicationFactory _baseFactory;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Guid _planId;
    private readonly Guid _subjectId;

    public BindingErrorsSweepTests()
    {
        _baseFactory = TrailWebApplicationFactory.Create(new Dictionary<string, string?> { ["Security:AuthRateLimitPermitLimit"] = "10000" });
        _gateway.Users.Add(new AccountUserDto(_currentUser.Id, "admin@example.com", true, true, true, DateTime.UtcNow));
        _gateway.Users.Add(new AccountUserDto(UserId.New(), "member@example.com", false, true, true, DateTime.UtcNow));
        _settings.Settings = UserSettings.CreateDefault(_currentUser.Id);
        Assert.True(_settings.Settings.Update("America/Sao_Paulo", 50, 7).IsOk);
        _factory = AccountTestHelper.CreateTestFactory(_baseFactory, _gateway, _email, _settings, _currentUser)
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPlanningStore>();
                services.AddScoped<IPlanningStore>(_ => _store);
            }));

        var plan = StudyPlan.Create(_currentUser.Id, "Sweep plan", "Description", ScoringRule.Cebraspe, PerformancePeriod.All(), PerformanceBands.CreateDefault(), null, 120, false).Data;
        plan.AddSection("Section");
        _store.AddStudyPlanAsync(plan, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        var subject = Subject.Create(_currentUser.Id, plan.Id, "Sweep subject").Data;
        plan.PlaceSubject(subject.Id, null);
        subject.AddTopic("Sweep topic", null, null, TopicStatus.ToStudy);
        _store.AddSubjectAsync(subject, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        _planId = plan.Id.Value;
        _subjectId = subject.Id.Value;
    }

    public void Dispose()
    {
        _factory.Dispose();
        _baseFactory.Dispose();
    }

    public static IEnumerable<string> FieldNames(Type type, string prefix, int depth = 0)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(CancellationToken) || underlying == typeof(string) || depth > MaxDepth)
        {
            yield break;
        }

        if (underlying.IsValueType || underlying.IsEnum)
        {
            yield return prefix;
            yield break;
        }

        if (typeof(IEnumerable).IsAssignableFrom(underlying))
        {
            var element = underlying.IsArray
                ? underlying.GetElementType()
                : underlying.GetInterfaces().Append(underlying)
                    .Where(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    .Select(candidate => candidate.GetGenericArguments()[0])
                    .FirstOrDefault();
            if (element is not null)
            {
                foreach (var name in FieldNames(element, $"{prefix}[0]", depth + 1))
                {
                    yield return name;
                }
            }

            yield break;
        }

        foreach (var property in underlying.GetProperties().Where(property => property.CanWrite && property.GetIndexParameters().Length == 0))
        {
            foreach (var name in FieldNames(property.PropertyType, $"{prefix}.{property.Name}", depth + 1))
            {
                yield return name;
            }
        }
    }

    [Fact]
    public async Task EveryPostHandler_WithAnUnparsableValue_AnswersUnprocessableKeepsItsPageAndChangesNothing()
    {
        var cases = await DiscoverCasesAsync();
        Assert.NotEmpty(cases);

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var (_, _, csrfHeader, csrfToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, "/planos");
        Assert.False(string.IsNullOrEmpty(csrfToken));

        var baselines = new Dictionary<string, PageSignature?>(StringComparer.Ordinal);
        var failures = new List<string>();
        foreach (var testCase in cases)
        {
            if (!baselines.ContainsKey(testCase.Url))
            {
                using var baseline = await client.GetAsync(testCase.Url, TestContext.Current.CancellationToken);
                baselines[testCase.Url] = baseline.StatusCode == HttpStatusCode.OK
                    ? PageSignature.Of(await baseline.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
                    : null;
            }

            foreach (var htmx in new[] { false, true })
            {
                var before = await SnapshotAsync();
                var url = testCase.Url + (testCase.Handler is null ? string.Empty : $"?handler={testCase.Handler}");
                using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
                    client,
                    url,
                    new Dictionary<string, string> { [testCase.Field] = Unparsable },
                    csrfHeader,
                    csrfToken,
                    htmx ? new Dictionary<string, string> { [HtmxIntegration.RequestTypeHeaderName] = HtmxIntegration.PartialRequestType } : null);
                var after = await SnapshotAsync();

                var label = $"{testCase.Page} {testCase.Handler ?? "(default)"} {testCase.Field} htmx={htmx}";
                if (response.StatusCode != HttpStatusCode.UnprocessableEntity)
                {
                    failures.Add($"{label}: expected 422, got {(int)response.StatusCode}");
                }
                else if (!htmx)
                {
                    CheckFullPage(label, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), baselines[testCase.Url], failures);
                }

                if (!string.Equals(before, after, StringComparison.Ordinal))
                {
                    failures.Add($"{label}: the state changed");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task Discovery_FindsTheKnownRiskyHandlers()
    {
        var cases = await DiscoverCasesAsync();

        Assert.Contains(cases, c => c.Page == "/Admin/Users" && c.Handler == "ToggleActive" && c.Field == "isActive");
        Assert.Contains(cases, c => c.Page == "/Admin/Users" && c.Handler == "ManualLink" && c.Field == "kind");
        Assert.Contains(cases, c => c.Page == "/Plans/Details" && c.Handler == "AddSubject" && c.Field == "sectionId");
        Assert.Contains(cases, c => c.Page == "/Plans/Details" && c.Handler == "AddSection" && c.Field == "expectedPlanVersion");
        Assert.Contains(cases, c => c.Page == "/Plans/Subject" && c.Handler == "AddTopic" && c.Field == "initialStatus");
        Assert.Contains(cases, c => c.Page == "/Plans/Subject" && c.Handler == "MoveTopic" && c.Field == "newParentId");
        Assert.Contains(cases, c => c.Page == "/Account/Settings" && c.Handler == "Settings" && c.Field == "SettingsInput.PomodoroFocusMinutes");
        Assert.DoesNotContain(cases, c => c.Page == "/Plans/Edit" && c.Field == "PlanId");
    }

    [Fact]
    public void FieldNames_ExpandComplexParametersAndCollectionsToTheirScalarMembers()
    {
        Assert.Equal(["ids[0]"], FieldNames(typeof(List<int>), "ids"));
        Assert.Equal(["flags[0]"], FieldNames(typeof(bool[]), "flags"));
        Assert.Equal(["maybe"], FieldNames(typeof(Guid?), "maybe"));
        Assert.Empty(FieldNames(typeof(string), "text"));
        Assert.Equal(
            ["record.Count", "record.Id", "record.Flags[0]", "record.Items[0].Status", "record.Items[0].Position", "record.Inner.Moment"],
            FieldNames(typeof(ProbeRecord), "record"));
    }

    private static void CheckFullPage(string label, string html, PageSignature? baseline, List<string> failures)
    {
        var document = new HtmlParser().ParseDocument(html);
        if (document.QuerySelectorAll("[role='alert'] li, .field-validation-error").Length == 0)
        {
            failures.Add($"{label}: no error is shown");
        }

        if (html.Contains(Guid.Empty.ToString(), StringComparison.Ordinal))
        {
            failures.Add($"{label}: the page links to an empty id");
        }

        var signature = PageSignature.Of(html);
        if (baseline is not null && signature != baseline)
        {
            failures.Add($"{label}: the page lost its content (expected {baseline}, got {signature})");
        }
    }

    private async Task<string> SnapshotAsync()
    {
        var text = new StringBuilder();
        var plan = await _store.GetStudyPlanByIdAsync(StudyPlanId.From(_planId), TestContext.Current.CancellationToken);
        text.Append(plan is null ? "no-plan" : $"{plan.Version}|{plan.Name}|{plan.Sections.Count}|{plan.Outline.Count}|{string.Join(',', plan.Sections.Select(section => section.Name))}|{string.Join(',', plan.Outline.Select(placement => $"{placement.SubjectId.Value}:{placement.SectionId?.Value}"))}");
        var subject = await _store.GetSubjectByIdAsync(SubjectId.From(_subjectId), TestContext.Current.CancellationToken);
        text.Append(subject is null ? "|no-subject" : $"|{subject.Version}|{subject.Name}|{string.Join(',', subject.Topics.Select(topic => $"{topic.Id.Value}:{topic.Title}:{topic.Status}:{topic.ParentId?.Value}"))}");
        text.Append('|').Append(string.Join(',', _gateway.Users.Select(user => $"{user.Id.Value}:{user.IsActive}")));
        text.Append('|').Append(string.Join(',', _gateway.Invitations.Select(invitation => invitation.Email)));
        text.Append('|').Append(_settings.Settings is { } settings ? $"{settings.TimeZoneId}:{settings.PomodoroFocusMinutes}:{settings.PomodoroBreakMinutes}:{_settings.Saved}" : "no-settings");
        text.Append('|').Append(_email.SentMessages.Count);
        return text.ToString();
    }

    private async Task<List<SweepCase>> DiscoverCasesAsync()
    {
        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();
        var loader = _factory.Services.GetRequiredService<PageLoader>();
        var cases = new List<SweepCase>();

        foreach (var endpoint in dataSource.Endpoints.OfType<RouteEndpoint>())
        {
            var descriptor = endpoint.Metadata.GetMetadata<PageActionDescriptor>();
            if (descriptor is null || endpoint.Metadata.GetMetadata<ISuppressMatchingMetadata>()?.SuppressMatching == true)
            {
                continue;
            }

            var compiled = await loader.LoadAsync(descriptor, endpoint.Metadata);
            var routeNames = endpoint.RoutePattern.Parameters.Select(parameter => parameter.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var url = UrlFor(endpoint.RoutePattern);
            var properties = compiled.BoundProperties
                .Where(property => property.BindingInfo?.BindingSource != BindingSource.Path && !routeNames.Contains(property.Name))
                .SelectMany(property => FieldNames(property.ParameterType, property.Name))
                .ToList();

            foreach (var handler in compiled.HandlerMethods.Where(handler => string.Equals(handler.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)))
            {
                var parameters = handler.Parameters
                    .Where(parameter => !routeNames.Contains(parameter.Name))
                    .SelectMany(parameter => FieldNames(parameter.ParameterType, parameter.Name));

                foreach (var field in parameters.Concat(properties).Distinct(StringComparer.Ordinal))
                {
                    cases.Add(new SweepCase(descriptor.ViewEnginePath!, url, handler.Name, field));
                }
            }
        }

        return cases;
    }

    private string UrlFor(RoutePattern pattern) =>
        "/" + string.Join('/', pattern.PathSegments.Select(segment => string.Concat(segment.Parts.Select(part => part switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternSeparatorPart separator => separator.Content,
            RoutePatternParameterPart parameter => RouteValueFor(parameter.Name),
            _ => throw new InvalidOperationException($"Unsupported route part in '{pattern.RawText}'."),
        }))));

    private string RouteValueFor(string name) => name switch
    {
        "planId" => _planId.ToString(),
        "subjectId" => _subjectId.ToString(),
        _ => Guid.NewGuid().ToString(),
    };

    private sealed record SweepCase(string Page, string Url, string? Handler, string Field);

    private sealed record PageSignature(string Heading, int Forms, int Tables)
    {
        public static PageSignature Of(string html)
        {
            var main = new HtmlParser().ParseDocument(html).QuerySelector("main");
            return new PageSignature(
                main?.QuerySelector("h1")?.TextContent.Trim() ?? string.Empty,
                main?.QuerySelectorAll("form:not(#topic-notes-container form)").Length ?? 0,
                main?.QuerySelectorAll("table").Length ?? 0);
        }
    }

    public sealed record ProbeRecord(int Count, Guid Id, string Name, List<bool> Flags, List<ProbeItem> Items, ProbeInner Inner);

    public sealed record ProbeItem(TopicStatus Status, int Position);

    public sealed record ProbeInner(DateOnly Moment);
}
