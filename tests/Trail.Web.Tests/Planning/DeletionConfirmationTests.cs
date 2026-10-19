using System.Globalization;
using System.Net;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Trail.Application.Abstractions;
using Trail.Domain.Planning;
using Trail.Web.Htmx;
using Trail.Web.Pages.Plans;
using Trail.Web.Tests.Accounts;

namespace Trail.Web.Tests.Planning;

public sealed class DeletionConfirmationTests(TrailWebApplicationFactory baseFactory) : IClassFixture<TrailWebApplicationFactory>
{
    private const string SampleCode = "practice.sample_deletion.entries";

    [Fact]
    public async Task PlanPage_DeletePlanDialog_ListsThePhaseTwoCountsInSingularAndPlural()
    {
        var (store, user, seed) = await SeedAsync();
        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, user);
        using var client = CreateClient(factory);

        var (response, document, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, PlanUrl(seed));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            [
                Counted(factory, "planning.deletion.sections.other", 2),
                Counted(factory, "planning.deletion.subjects.one", 1),
                Counted(factory, "planning.deletion.topics.other", 3),
                Counted(factory, "planning.deletion.notes.one", 1),
            ],
            ImpactTexts(document.QuerySelector("dialog#delete-plan-dialog")!));
        Assert.Equal(
            ["planning.deletion.sections", "planning.deletion.subjects", "planning.deletion.topics", "planning.deletion.notes"],
            document.QuerySelectorAll("dialog#delete-plan-dialog [data-deletion-impact] li").Select(item => item.GetAttribute("data-code")));
    }

    [Fact]
    public async Task PlanPage_DeletePlanDialog_IsDescribedByThePlanTheWarningAndTheCountsAndStartsOnCancel()
    {
        var (store, user, seed) = await SeedAsync();
        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, user);
        using var client = CreateClient(factory);

        var (_, document, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, PlanUrl(seed));

        var dialog = document.QuerySelector("dialog#delete-plan-dialog")!;
        AssertDescribedBy(factory, dialog, dialog, "delete-plan-description", seed.Plan.Name);
        var cancel = Assert.Single(dialog.QuerySelectorAll("[autofocus]"));
        Assert.Equal(Text(factory, "Cancel"), cancel.TextContent.Trim());
        Assert.True(cancel.HasAttribute("data-dialog-close"));
    }

    [Fact]
    public async Task PlanPage_PlanWithoutNotes_StillListsTheZeroCountInThePlural()
    {
        var store = new TestPlanningStore();
        var user = new TestCurrentUser();
        var plan = StudyPlan.Create(user.Id, "Empty plan").Data;
        await store.AddStudyPlanAsync(plan, TestContext.Current.CancellationToken);
        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, user);
        using var client = CreateClient(factory);

        var (_, document, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, $"/planos/{plan.Id.Value}");

        Assert.Equal(
            [
                Counted(factory, "planning.deletion.sections.other", 0),
                Counted(factory, "planning.deletion.subjects.other", 0),
                Counted(factory, "planning.deletion.topics.other", 0),
                Counted(factory, "planning.deletion.notes.other", 0),
            ],
            ImpactTexts(document.QuerySelector("dialog#delete-plan-dialog")!));
    }

    [Fact]
    public async Task PlanPage_EachSubject_OffersALinkThatLoadsItsConfirmationIntoTheDialog()
    {
        var (store, user, seed) = await SeedAsync();
        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, user);
        using var client = CreateClient(factory);

        var (_, document, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, PlanUrl(seed));

        var link = Assert.Single(document.QuerySelectorAll("a[data-dialog-open-after-swap='delete-subject-dialog']"));
        AssertHandlerUrl(link.GetAttribute("href"), PlanUrl(seed), "DeleteSubjectImpact", "subjectId", seed.Subject.Id.Value);
        Assert.Equal(link.GetAttribute("href"), link.GetAttribute("hx-get"));
        Assert.Equal("#delete-subject-confirmation", link.GetAttribute("hx-target"));
        Assert.Contains(HtmxIntegration.PartialRequestType, link.GetAttribute("hx-headers"), StringComparison.Ordinal);
        Assert.Equal("dialog", link.GetAttribute("aria-haspopup"));
        Assert.Equal(Text(factory, "DeleteNamed", seed.Subject.Name), link.GetAttribute("aria-label"));
        Assert.Null(document.QuerySelector("form[action*='handler=DeleteSubject']"));

        var dialog = document.QuerySelector("dialog#delete-subject-dialog")!;
        Assert.Equal(Text(factory, "DeleteSubject"), dialog.QuerySelector("#" + dialog.GetAttribute("aria-labelledby"))!.TextContent.Trim());
        Assert.Equal(DeletionConfirmationModel.SubjectDialogDescriptionId, dialog.GetAttribute("aria-describedby"));
        Assert.NotNull(dialog.QuerySelector("#delete-subject-confirmation"));
    }

    [Fact]
    public async Task DeleteSubjectImpact_WithHtmx_ReturnsTheConfirmationPartialWithTheCountsAndTheForm()
    {
        var (store, user, seed) = await SeedAsync();
        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, user);
        using var client = CreateClient(factory);

        var (response, document) = await GetPartialAsync(client, $"{PlanUrl(seed)}?handler=DeleteSubjectImpact&subjectId={seed.Subject.Id.Value}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(document.QuerySelector("main"));
        Assert.Contains(seed.Subject.Name, document.Body!.TextContent, StringComparison.Ordinal);
        Assert.Equal(
            [
                Counted(factory, "planning.deletion.subjects.one", 1),
                Counted(factory, "planning.deletion.topics.other", 3),
                Counted(factory, "planning.deletion.notes.one", 1),
            ],
            ImpactTexts(document.Body));
        AssertConfirmationForm(document.Body, $"{PlanUrl(seed)}?handler=DeleteSubject", "subjectId", seed.Subject.Id.Value, PlanUrl(seed));
        AssertDescription(factory, document.Body, DeletionConfirmationModel.SubjectDialogDescriptionId, seed.Subject.Name);
    }

    [Fact]
    public async Task DeleteSubjectImpact_WithoutHtmx_ShowsTheConfirmationOnThePlanPage()
    {
        var (store, user, seed) = await SeedAsync();
        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, user);
        using var client = CreateClient(factory);

        var (response, document, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(
            client,
            $"{PlanUrl(seed)}?handler=DeleteSubjectImpact&subjectId={seed.Subject.Id.Value}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var section = document.QuerySelector("main section[aria-labelledby='subject-deletion-heading']");
        Assert.NotNull(section);
        Assert.Equal(Text(factory, "DeleteSubject"), section.QuerySelector("#subject-deletion-heading")!.TextContent.Trim());
        Assert.Equal(
            [
                Counted(factory, "planning.deletion.subjects.one", 1),
                Counted(factory, "planning.deletion.topics.other", 3),
                Counted(factory, "planning.deletion.notes.one", 1),
            ],
            ImpactTexts(section));
        AssertConfirmationForm(section, $"{PlanUrl(seed)}?handler=DeleteSubject", "subjectId", seed.Subject.Id.Value, PlanUrl(seed));
        AssertDescribedBy(factory, document, section, DeletionConfirmationModel.SubjectSectionDescriptionId, seed.Subject.Name);
        Assert.NotNull(document.QuerySelector("dialog#delete-plan-dialog [data-deletion-impact]"));
        Assert.Empty(document.QuerySelector("#delete-subject-confirmation")!.Children);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeleteSubjectConfirmation_WhenSubmitted_DeletesTheSubjectAndReturnsToThePlan(bool loadWithHtmx)
    {
        var (store, user, seed) = await SeedAsync();
        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, user);
        using var client = CreateClient(factory);
        var impactUrl = $"{PlanUrl(seed)}?handler=DeleteSubjectImpact&subjectId={seed.Subject.Id.Value}";
        var (_, _, csrfHeader, csrfToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, PlanUrl(seed));
        var form = loadWithHtmx
            ? (await GetPartialAsync(client, impactUrl)).Document.QuerySelector("form")!
            : (await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, impactUrl)).Document.QuerySelector("main section form")!;

        using var response = await SubmitAsync(client, form, csrfHeader, csrfToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(PlanUrl(seed), response.Headers.Location?.OriginalString);
        Assert.Null(await store.GetSubjectByIdAsync(seed.Subject.Id, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetTopicNoteByTopicIdAsync(seed.Subtopic.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SubjectPage_EachTopic_OffersALinkThatLoadsItsConfirmationIntoTheDialog()
    {
        var (store, user, seed) = await SeedAsync();
        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, user);
        using var client = CreateClient(factory);

        var (_, document, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, SubjectUrl(seed));

        var links = document.QuerySelectorAll("a[data-dialog-open-after-swap='delete-topic-dialog']");
        Assert.Equal(3, links.Length);
        var link = Assert.Single(links, candidate => candidate.GetAttribute("href")!.Contains(seed.Subtopic.Id.Value.ToString(), StringComparison.Ordinal));
        AssertHandlerUrl(link.GetAttribute("href"), SubjectUrl(seed), "DeleteTopicImpact", "topicId", seed.Subtopic.Id.Value);
        Assert.Equal(link.GetAttribute("href"), link.GetAttribute("hx-get"));
        Assert.Equal("#delete-topic-confirmation", link.GetAttribute("hx-target"));
        Assert.Equal(Text(factory, "DeleteNamed", seed.Subtopic.Title), link.GetAttribute("aria-label"));
        Assert.Null(document.QuerySelector("form[action*='handler=DeleteTopic']"));

        var dialog = document.QuerySelector("dialog#delete-topic-dialog")!;
        Assert.Equal(Text(factory, "DeleteTopic"), dialog.QuerySelector("#" + dialog.GetAttribute("aria-labelledby"))!.TextContent.Trim());
        Assert.Equal(DeletionConfirmationModel.TopicDialogDescriptionId, dialog.GetAttribute("aria-describedby"));
        Assert.NotNull(dialog.QuerySelector("#delete-topic-confirmation"));
        Assert.Null(document.QuerySelector("#topic-tree-container #delete-topic-dialog"));
    }

    [Fact]
    public async Task DeleteTopicImpact_WithHtmx_ReturnsTheConfirmationPartialWithTheSubtreeCounts()
    {
        var (store, user, seed) = await SeedAsync();
        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, user);
        using var client = CreateClient(factory);

        var (response, document) = await GetPartialAsync(client, $"{SubjectUrl(seed)}?handler=DeleteTopicImpact&topicId={seed.Topic.Id.Value}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(document.QuerySelector("main"));
        Assert.Contains(seed.Topic.Title, document.Body!.TextContent, StringComparison.Ordinal);
        Assert.Equal(
            [Counted(factory, "planning.deletion.topics.other", 2), Counted(factory, "planning.deletion.notes.one", 1)],
            ImpactTexts(document.Body));
        AssertConfirmationForm(document.Body, $"{SubjectUrl(seed)}?handler=DeleteTopic", "topicId", seed.Topic.Id.Value, SubjectUrl(seed));
        AssertDescription(factory, document.Body, DeletionConfirmationModel.TopicDialogDescriptionId, seed.Topic.Title);
    }

    [Fact]
    public async Task DeleteTopicImpact_TopicWithoutSubtopicsOrNote_ListsTheSingularAndTheZeroCount()
    {
        var (store, user, seed) = await SeedAsync();
        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, user);
        using var client = CreateClient(factory);

        var (_, document) = await GetPartialAsync(client, $"{SubjectUrl(seed)}?handler=DeleteTopicImpact&topicId={seed.Sibling.Id.Value}");

        Assert.Equal(
            [Counted(factory, "planning.deletion.topics.one", 1), Counted(factory, "planning.deletion.notes.other", 0)],
            ImpactTexts(document.Body!));
    }

    [Fact]
    public async Task DeleteTopicImpact_WithoutHtmx_ShowsTheConfirmationOnTheSubjectPage()
    {
        var (store, user, seed) = await SeedAsync();
        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, user);
        using var client = CreateClient(factory);

        var (response, document, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(
            client,
            $"{SubjectUrl(seed)}?handler=DeleteTopicImpact&topicId={seed.Topic.Id.Value}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/html", response.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);
        var section = document.QuerySelector("main section[aria-labelledby='topic-deletion-heading']");
        Assert.NotNull(section);
        Assert.Equal(Text(factory, "DeleteTopic"), section.QuerySelector("#topic-deletion-heading")!.TextContent.Trim());
        Assert.Equal(
            [Counted(factory, "planning.deletion.topics.other", 2), Counted(factory, "planning.deletion.notes.one", 1)],
            ImpactTexts(section));
        AssertConfirmationForm(section, $"{SubjectUrl(seed)}?handler=DeleteTopic", "topicId", seed.Topic.Id.Value, SubjectUrl(seed));
        AssertDescribedBy(factory, document, section, DeletionConfirmationModel.TopicSectionDescriptionId, seed.Topic.Title);
        Assert.NotNull(document.QuerySelector("#topic-tree-container"));
        Assert.NotNull(document.QuerySelector("#topic-notes-container form"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeleteTopicConfirmation_WhenSubmitted_DeletesTheTopicWithItsSubtopicsAndNotes(bool loadWithHtmx)
    {
        var (store, user, seed) = await SeedAsync();
        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, user);
        using var client = CreateClient(factory);
        var impactUrl = $"{SubjectUrl(seed)}?handler=DeleteTopicImpact&topicId={seed.Topic.Id.Value}";
        var (_, _, csrfHeader, csrfToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, SubjectUrl(seed));
        var form = loadWithHtmx
            ? (await GetPartialAsync(client, impactUrl)).Document.QuerySelector("form")!
            : (await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, impactUrl)).Document.QuerySelector("main section form")!;

        using var response = await SubmitAsync(client, form, csrfHeader, csrfToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(SubjectUrl(seed), response.Headers.Location?.OriginalString);
        var subject = await store.GetSubjectByIdAsync(seed.Subject.Id, TestContext.Current.CancellationToken);
        Assert.Equal([seed.Sibling.Id], subject!.Topics.Select(topic => topic.Id));
        Assert.Null(await store.GetTopicNoteByTopicIdAsync(seed.Subtopic.Id, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeleteImpact_ForUnknownTargets_ReturnsNotFound(bool htmx)
    {
        var (store, user, seed) = await SeedAsync();
        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, user);
        using var client = CreateClient(factory);

        using var subjectResponse = await SendAsync(client, $"{PlanUrl(seed)}?handler=DeleteSubjectImpact&subjectId={Guid.NewGuid()}", htmx);
        using var topicResponse = await SendAsync(client, $"{SubjectUrl(seed)}?handler=DeleteTopicImpact&topicId={Guid.NewGuid()}", htmx);

        Assert.Equal(HttpStatusCode.NotFound, subjectResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, topicResponse.StatusCode);
    }

    [Fact]
    public async Task DeletionPreview_WithAContributor_ShowsItsItemTranslatedThroughItsPrefixInSingularAndPlural()
    {
        var (store, user, seed) = await SeedAsync();
        var emptySubject = Subject.Create(user.Id, seed.Plan.Id, "Empty subject").Data;
        seed.Plan.PlaceSubject(emptySubject.Id, null);
        await store.AddSubjectAsync(emptySubject, TestContext.Current.CancellationToken);
        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, user).WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IDeletionImpactContributor, SampleTopicEntriesContributor>();
                services.Replace(ServiceDescriptor.Singleton<IStringLocalizerFactory>(provider =>
                    new SampleCountLocalizerFactory(ActivatorUtilities.CreateInstance<ResourceManagerStringLocalizerFactory>(provider))));
            }));
        using var client = CreateClient(factory);

        var (_, planDocument, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, PlanUrl(seed));
        var (_, singleTopic) = await GetPartialAsync(client, $"{SubjectUrl(seed)}?handler=DeleteTopicImpact&topicId={seed.Sibling.Id.Value}");
        var (_, noTopics) = await GetPartialAsync(client, $"{PlanUrl(seed)}?handler=DeleteSubjectImpact&subjectId={emptySubject.Id.Value}");

        Assert.Equal(
            "3 sample entries",
            ImpactTexts(planDocument.QuerySelector("dialog#delete-plan-dialog")!)[^1]);
        Assert.Equal(
            [Counted(factory, "planning.deletion.topics.one", 1), Counted(factory, "planning.deletion.notes.other", 0), "1 sample entry"],
            ImpactTexts(singleTopic.Body!));
        Assert.Equal("0 sample entries", ImpactTexts(noTopics.Body!)[^1]);
        Assert.Equal(SampleCode, singleTopic.QuerySelectorAll("[data-deletion-impact] li")[^1].GetAttribute("data-code"));
    }

    private static void AssertDescribedBy(WebApplicationFactory<Program> factory, IParentNode root, IElement described, string descriptionId, string targetName)
    {
        Assert.Equal(descriptionId, described.GetAttribute("aria-describedby"));
        AssertDescription(factory, root, descriptionId, targetName);
    }

    private static void AssertDescription(WebApplicationFactory<Program> factory, IParentNode root, string descriptionId, string targetName)
    {
        var description = Assert.Single(root.QuerySelectorAll($"[id='{descriptionId}']"));
        var paragraphs = description.QuerySelectorAll("p").Select(paragraph => paragraph.TextContent.Trim()).ToList();

        Assert.Equal([targetName, Text(factory, "DeletionImpactWarning")], paragraphs);
        Assert.NotEmpty(ImpactTexts(description));
        Assert.Empty(description.QuerySelectorAll("form, button, a"));
    }

    private static void AssertHandlerUrl(string? url, string path, string handler, string idName, Guid id)
    {
        Assert.NotNull(url);
        var uri = new Uri(new Uri("http://localhost"), url);
        var query = QueryHelpers.ParseQuery(uri.Query);

        Assert.Equal(path, uri.AbsolutePath);
        Assert.Equal(2, query.Count);
        Assert.Equal(handler, query["handler"].ToString());
        Assert.Equal(id.ToString(), query[idName].ToString());
    }

    private static void AssertConfirmationForm(IElement container, string action, string field, Guid targetId, string cancelUrl)
    {
        var form = Assert.Single(container.QuerySelectorAll("form"));
        Assert.Equal("post", form.GetAttribute("method"), ignoreCase: true);
        Assert.Equal(action, form.GetAttribute("action"));
        Assert.Equal(targetId.ToString(), form.QuerySelector($"input[type='hidden'][name='{field}']")?.GetAttribute("value"));
        Assert.NotNull(form.QuerySelector("input[name='__RequestVerificationToken']"));
        Assert.NotNull(form.QuerySelector("button[type='submit']"));
        var cancel = Assert.Single(form.QuerySelectorAll("a[data-dialog-close]"));
        Assert.Equal(cancelUrl, cancel.GetAttribute("href"));
        Assert.True(cancel.HasAttribute("autofocus"));
    }

    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, IElement form, string? csrfHeader, string? csrfToken)
    {
        var fields = form.QuerySelectorAll("input[name]")
            .ToDictionary(input => input.GetAttribute("name")!, input => input.GetAttribute("value") ?? string.Empty);

        return await PlanningTestHelper.PostFormWithAntiforgeryAsync(client, form.GetAttribute("action")!, fields, csrfHeader, csrfToken);
    }

    private static async Task<(HttpResponseMessage Response, IHtmlDocument Document)> GetPartialAsync(HttpClient client, string url)
    {
        var response = await SendAsync(client, url, htmx: true);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        return (response, new HtmlParser().ParseDocument(html));
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string url, bool htmx)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (htmx)
        {
            request.Headers.Add(HtmxIntegration.RequestTypeHeaderName, HtmxIntegration.PartialRequestType);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static List<string> ImpactTexts(IElement container) =>
        [.. container.QuerySelectorAll("[data-deletion-impact] li").Select(item => item.TextContent.Trim())];

    private static string Counted(WebApplicationFactory<Program> factory, string key, int count) =>
        string.Format(CultureInfo.GetCultureInfo("pt-BR"), Text(factory, key), count);

    private static string Text(WebApplicationFactory<Program> factory, string key, params object[] arguments)
    {
        var localizer = factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>();
        var text = localizer[key];
        Assert.False(text.ResourceNotFound, $"The resource {key} is missing.");

        return arguments.Length == 0 ? text.Value : string.Format(CultureInfo.GetCultureInfo("pt-BR"), text.Value, arguments);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static string PlanUrl(Seed seed) => $"/planos/{seed.Plan.Id.Value}";

    private static string SubjectUrl(Seed seed) => $"/planos/{seed.Plan.Id.Value}/disciplinas/{seed.Subject.Id.Value}";

    private static async Task<(TestPlanningStore Store, TestCurrentUser User, Seed Seed)> SeedAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new TestPlanningStore();
        var user = new TestCurrentUser();

        var plan = StudyPlan.Create(user.Id, "Deletion plan").Data;
        var section = plan.AddSection("Section A").Data;
        plan.AddSection("Section B");
        var subject = Subject.Create(user.Id, plan.Id, "Tax law").Data;
        plan.PlaceSubject(subject.Id, section.Id);
        var topic = subject.AddTopic("Competence", TopicCode.Create("1").Data, null, TopicStatus.ToStudy).Data;
        var subtopic = subject.AddTopic("Limits", TopicCode.Create("1.1").Data, topic.Id, TopicStatus.ToStudy).Data;
        var sibling = subject.AddTopic("Immunities", TopicCode.Create("2").Data, null, TopicStatus.ToStudy).Data;
        var note = TopicNote.Create(user.Id, subtopic.Id, "Note").Data;

        await store.AddStudyPlanAsync(plan, ct);
        await store.AddSubjectAsync(subject, ct);
        await store.AddTopicNoteAsync(note, ct);

        return (store, user, new Seed(plan, subject, topic, subtopic, sibling));
    }

    private sealed record Seed(StudyPlan Plan, Subject Subject, Topic Topic, Topic Subtopic, Topic Sibling);

    private sealed class SampleTopicEntriesContributor : IDeletionImpactContributor
    {
        public Task<IReadOnlyList<DeletionImpactItem>> GetImpactAsync(DeletionScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeletionImpactItem>>([DeletionImpactItem.Create(SampleCode, scope.TopicIds.Count)]);
    }

    private sealed class SampleCountLocalizerFactory(IStringLocalizerFactory inner) : IStringLocalizerFactory
    {
        public IStringLocalizer Create(Type resourceSource) =>
            resourceSource == typeof(PracticeResource) ? new SampleCountLocalizer(inner.Create(resourceSource)) : inner.Create(resourceSource);

        public IStringLocalizer Create(string baseName, string location) => inner.Create(baseName, location);
    }

    private sealed class SampleCountLocalizer(IStringLocalizer inner) : IStringLocalizer
    {
        private static readonly Dictionary<string, string> Texts = new(StringComparer.Ordinal)
        {
            [SampleCode + ".one"] = "{0} sample entry",
            [SampleCode + ".other"] = "{0} sample entries",
        };

        public LocalizedString this[string name] =>
            Texts.TryGetValue(name, out var text) ? new LocalizedString(name, text) : inner[name];

        public LocalizedString this[string name, params object[] arguments] =>
            Texts.TryGetValue(name, out var text)
                ? new LocalizedString(name, string.Format(CultureInfo.CurrentCulture, text, arguments))
                : inner[name, arguments];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => inner.GetAllStrings(includeParentCultures);
    }
}
