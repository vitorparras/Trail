using System.Globalization;
using System.Net;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;
using Trail.Domain.Planning;
using Trail.Web.Tests.Accounts;

namespace Trail.Web.Tests.Planning;

public sealed class SubjectNotePanelTests(TrailWebApplicationFactory baseFactory) : IClassFixture<TrailWebApplicationFactory>
{
    [Fact]
    public async Task InvalidPost_WithoutHtmx_KeepsTheNotePanelOfTheFirstTopic()
    {
        var seed = await SeedAsync();
        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, seed.Store, seed.User);
        using var client = CreateClient(factory);
        var (_, _, csrfHeader, csrfToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, seed.Url);

        using var response = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{seed.Url}?handler=AddTopic",
            new() { ["title"] = string.Empty, ["initialStatus"] = "ToStudy", ["expectedSubjectVersion"] = Version(seed.Subject) },
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        AssertNotePanel(await ParseAsync(response), seed.First.Title, seed.FirstNote);
    }

    [Fact]
    public async Task PostWithBindingErrors_WithoutHtmx_KeepsTheNotePanelOfThePostedTopic()
    {
        var seed = await SeedAsync();
        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, seed.Store, seed.User);
        using var client = CreateClient(factory);
        var (_, _, csrfHeader, csrfToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, seed.Url);

        using var response = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{seed.Url}?handler=SaveNote",
            new() { ["topicId"] = seed.Second.Id.Value.ToString(), ["markdown"] = "Changed", ["expectedVersion"] = "not-a-number" },
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        AssertNotePanel(await ParseAsync(response), seed.Second.Title, seed.SecondNote);
        Assert.Equal(seed.SecondNote, (await seed.Store.GetTopicNoteByTopicIdAsync(seed.Second.Id, TestContext.Current.CancellationToken))!.Markdown);
    }

    [Fact]
    public async Task ConflictingPost_WithoutHtmx_KeepsTheNotePanel()
    {
        var seed = await SeedAsync();
        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, seed.Store, seed.User);
        using var client = CreateClient(factory);
        var (_, _, csrfHeader, csrfToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, seed.Url);

        using var response = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{seed.Url}?handler=UpdateSubject",
            new() { ["name"] = "Renamed", ["expectedVersion"] = "999" },
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        AssertNotePanel(await ParseAsync(response), seed.First.Title, seed.FirstNote);
    }

    private static void AssertNotePanel(IHtmlDocument document, string topicTitle, string markdown)
    {
        var panel = Assert.Single(document.QuerySelectorAll("main #topic-notes-container"));
        Assert.Contains(topicTitle, panel.QuerySelector("#topic-notes-heading")!.TextContent, StringComparison.Ordinal);
        Assert.NotNull(panel.QuerySelector("form"));
        Assert.Contains(markdown, panel.TextContent, StringComparison.Ordinal);
    }

    private static async Task<IHtmlDocument> ParseAsync(HttpResponseMessage response) =>
        new HtmlParser().ParseDocument(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static string Version(Subject subject) => subject.Version.ToString(CultureInfo.InvariantCulture);

    private static async Task<Seed> SeedAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new TestPlanningStore();
        var user = new TestCurrentUser();
        var plan = StudyPlan.Create(user.Id, "Note panel plan").Data;
        var subject = Subject.Create(user.Id, plan.Id, "Note panel subject").Data;
        plan.PlaceSubject(subject.Id, null);
        var first = subject.AddTopic("First topic", TopicCode.Create("1").Data, null, TopicStatus.ToStudy).Data;
        var second = subject.AddTopic("Second topic", TopicCode.Create("2").Data, null, TopicStatus.ToStudy).Data;
        const string firstNote = "First topic markdown";
        const string secondNote = "Second topic markdown";

        await store.AddStudyPlanAsync(plan, ct);
        await store.AddSubjectAsync(subject, ct);
        await store.AddTopicNoteAsync(TopicNote.Create(user.Id, first.Id, firstNote).Data, ct);
        await store.AddTopicNoteAsync(TopicNote.Create(user.Id, second.Id, secondNote).Data, ct);

        return new Seed(store, user, subject, first, second, firstNote, secondNote, $"/planos/{plan.Id.Value}/disciplinas/{subject.Id.Value}");
    }

    private sealed record Seed(
        TestPlanningStore Store,
        TestCurrentUser User,
        Subject Subject,
        Topic First,
        Topic Second,
        string FirstNote,
        string SecondNote,
        string Url);
}
