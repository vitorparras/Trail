using System.Globalization;
using System.Net;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Localization;
using Trail.Domain.Planning;
using Trail.Web.Htmx;
using Trail.Web.Tests.Accounts;

namespace Trail.Web.Tests.Planning;

public sealed class PlanFormErrorsTests(TrailWebApplicationFactory baseFactory) : IClassFixture<TrailWebApplicationFactory>
{
    private const string EnglishRequiredFragment = "field is required";

    public static TheoryData<string, string, string, string> Scenarios => new()
    {
        { "blank-name", "Input.Name", " ", "planning.plan.name_required" },
        { "long-name", "Input.Name", new string('n', StudyPlan.MaxNameLength + 1), "planning.plan.name_too_long" },
        { "long-description", "Input.Description", new string('d', StudyPlan.MaxDescriptionLength + 1), "planning.plan.description_too_long" },
        { "no-questions", "Input.DefaultMockExamQuestions", "0", "planning.plan.mock_exam_questions_must_be_positive" },
        { "low-out-of-range", "Input.LowBandThreshold", "101", "planning.bands.low_out_of_range" },
        { "high-out-of-range", "Input.HighBandThreshold", "101", "planning.bands.high_out_of_range" },
        { "low-not-below-high", "Input.LowBandThreshold", "80", "planning.bands.low_must_be_less_than_high" },
        { "days-missing", "Input.PeriodDays", string.Empty, "planning.period.days_must_be_positive" },
        { "range-reversed", "Input.PeriodEndDate", "2026-01-01", "planning.period.invalid_date_range" },
    };

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task Create_WithInvalidInput_Returns422AndShowsTheLocalizedErrorUnderTheMappedField(string scenario, string field, string value, string messageCode)
    {
        using var setup = Prepare();
        var form = ValidForm();
        Apply(form, scenario, field, value);

        await SubmitAsync(setup, "/planos/novo", form, "/planos/novo", messageCode, field);

    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task Edit_WithInvalidInput_Returns422AndShowsTheLocalizedErrorUnderTheMappedField(string scenario, string field, string value, string messageCode)
    {
        using var setup = Prepare();
        var plan = StudyPlan.Create(setup.CurrentUser.Id, "Existing plan", "Description", ScoringRule.Cebraspe, PerformancePeriod.All(), PerformanceBands.CreateDefault(), null, 120, false).Data;
        await setup.Store.AddStudyPlanAsync(plan, TestContext.Current.CancellationToken);
        var form = ValidForm();
        form["Input.ExpectedVersion"] = plan.Version.ToString(CultureInfo.InvariantCulture);
        Apply(form, scenario, field, value);
        var url = $"/planos/{plan.Id.Value}/editar";

        await SubmitAsync(setup, url, form, url, messageCode, field);

    }

    [Theory]
    [InlineData("/planos/novo", "Input.LowBandThreshold", "abc")]
    [InlineData("/planos/novo", "Input.PeriodKind", "99")]
    [InlineData("/planos/novo", "Input.DefaultMockExamQuestions", "many")]
    [InlineData("edit", "Input.LowBandThreshold", "abc")]
    public async Task PlanForm_WithUnparsableValue_ShowsTheLocalizedBindingMessageUnderTheField(string target, string field, string value)
    {
        using var setup = Prepare();
        var url = target;
        string? planPath = null;
        var form = ValidForm();
        if (target == "edit")
        {
            var plan = StudyPlan.Create(setup.CurrentUser.Id, "Existing plan", "Description", ScoringRule.Cebraspe, PerformancePeriod.All(), PerformanceBands.CreateDefault(), null, 120, false).Data;
            await setup.Store.AddStudyPlanAsync(plan, TestContext.Current.CancellationToken);
            form["Input.ExpectedVersion"] = plan.Version.ToString(CultureInfo.InvariantCulture);
            planPath = $"/planos/{plan.Id.Value}";
            url = $"{planPath}/editar";
        }

        form[field] = value;
        var (_, csrfHeader, csrfToken) = await LoadAsync(setup, url);
        using var response = await PlanningTestHelper.PostFormWithAntiforgeryAsync(setup.Client, url, form, csrfHeader, csrfToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var document = new HtmlParser().ParseDocument(html);
        var expected = setup.Localizer["ModelBindingAttemptedValueIsInvalid", value].Value;
        Assert.Contains(expected, AccountTestHelper.FieldErrors(document, field));
        var control = AccountTestHelper.InvalidField(document, field)!;
        Assert.True(control.TagName != "INPUT" || control.GetAttribute("value") == value);
        Assert.DoesNotContain("is not valid", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("is invalid", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Guid.Empty.ToString(), html, StringComparison.Ordinal);
        Assert.True(planPath is null || document.QuerySelector($"main a[href='{planPath}']") is not null);
    }

    [Fact]
    public async Task AddSection_WithHtmxAndABlankName_ShowsTheErrorOnceInTheFormWithoutAToast()
    {
        using var setup = Prepare();
        var plan = StudyPlan.Create(setup.CurrentUser.Id, "Existing plan", "Description", ScoringRule.Cebraspe, PerformancePeriod.All(), PerformanceBands.CreateDefault(), null, 120, false).Data;
        await setup.Store.AddStudyPlanAsync(plan, TestContext.Current.CancellationToken);
        var url = $"/planos/{plan.Id.Value}";
        var (_, csrfHeader, csrfToken) = await LoadAsync(setup, url);
        var message = setup.Localizer["planning.section.name_required"].Value;

        using var htmx = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            setup.Client,
            $"{url}?handler=AddSection",
            new() { ["name"] = " ", ["expectedPlanVersion"] = plan.Version.ToString(CultureInfo.InvariantCulture) },
            csrfHeader,
            csrfToken,
            new() { [HtmxIntegration.RequestTypeHeaderName] = HtmxIntegration.PartialRequestType });
        var htmxHtml = await htmx.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, htmx.StatusCode);
        Assert.Equal(1, Occurrences(htmxHtml, message));
        Assert.Empty(new HtmlParser().ParseDocument(htmxHtml).QuerySelectorAll("[data-toast]"));

        using var full = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            setup.Client,
            $"{url}?handler=AddSection",
            new() { ["name"] = " ", ["expectedPlanVersion"] = plan.Version.ToString(CultureInfo.InvariantCulture) },
            csrfHeader,
            csrfToken);
        var fullHtml = await full.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, full.StatusCode);
        Assert.Equal(1, Occurrences(fullHtml, message));
        Assert.Empty(new HtmlParser().ParseDocument(fullHtml).QuerySelectorAll("[data-toast]"));
    }

    private static int Occurrences(string html, string text) =>
        html.Split(text, StringSplitOptions.None).Length - 1;

    private static Dictionary<string, string> ValidForm() => new()
    {
        ["Input.Name"] = "Valid plan",
        ["Input.Description"] = "Valid description",
        ["Input.ScoringRule"] = "Cebraspe",
        ["Input.PeriodKind"] = "All",
        ["Input.LowBandThreshold"] = "50",
        ["Input.HighBandThreshold"] = "70",
        ["Input.DefaultMockExamQuestions"] = "120",
    };

    private static void Apply(Dictionary<string, string> form, string scenario, string field, string value)
    {
        form[field] = value;
        switch (scenario)
        {
            case "low-not-below-high":
                form["Input.HighBandThreshold"] = "70";
                break;
            case "days-missing":
                form["Input.PeriodKind"] = "LastDays";
                break;
            case "range-reversed":
                form["Input.PeriodKind"] = "DateRange";
                form["Input.PeriodStartDate"] = "2026-02-01";
                break;
            default:
                break;
        }
    }

    private Setup Prepare()
    {
        var store = new TestPlanningStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true };
        var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, currentUser);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        return new Setup(factory, client, store, currentUser, factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>());
    }

    private static async Task<(IHtmlDocument Document, string? CsrfHeader, string? CsrfToken)> LoadAsync(Setup setup, string url)
    {
        var (_, document, csrfHeader, csrfToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(setup.Client, url);
        return (document, csrfHeader, csrfToken);
    }

    private static async Task SubmitAsync(
        Setup setup,
        string getUrl,
        Dictionary<string, string> form,
        string postUrl,
        string messageCode,
        string field)
    {
        var (_, csrfHeader, csrfToken) = await LoadAsync(setup, getUrl);
        using var response = await PlanningTestHelper.PostFormWithAntiforgeryAsync(setup.Client, postUrl, form, csrfHeader, csrfToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var document = new HtmlParser().ParseDocument(html);
        var expected = setup.Localizer[messageCode].Value;

        Assert.NotNull(AccountTestHelper.InvalidField(document, field));
        Assert.Contains(expected, AccountTestHelper.FieldErrors(document, field));
        Assert.Empty(AccountTestHelper.SummaryErrors(document));
        Assert.DoesNotContain(EnglishRequiredFragment, html, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Setup(
        WebApplicationFactory<Program> factory,
        HttpClient client,
        TestPlanningStore store,
        TestCurrentUser currentUser,
        IStringLocalizer<SharedResource> localizer) : IDisposable
    {
        public HttpClient Client { get; } = client;

        public TestPlanningStore Store { get; } = store;

        public TestCurrentUser CurrentUser { get; } = currentUser;

        public IStringLocalizer<SharedResource> Localizer { get; } = localizer;

        public void Dispose()
        {
            Client.Dispose();
            factory.Dispose();
        }
    }
}
