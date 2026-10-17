using System.Globalization;
using System.Net;
using System.Reflection;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Localization;
using OpenTelemetry.Trace;
using Trail.Application.Planning;
using Trail.Application.Telemetry;
using Trail.Domain.Planning;
using Trail.Infrastructure.Persistence;
using Trail.Web.Htmx;
using Trail.Web.Pages.Plans;
using Trail.Web.Tests.Accounts;
using Trail.Web.Tests.Observability;
using Trail.Web.Tests.Persistence;

namespace Trail.Web.Tests.Planning;

public sealed class PlanPagesTests(TrailWebApplicationFactory baseFactory) : IClassFixture<TrailWebApplicationFactory>
{
    [Fact]
    public async Task CreateStudyPlan_WhenValid_RedirectsToDetails_AndCanBeListed()
    {
        var store = new TestPlanningStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true };

        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var (_, _, csrfHeader, csrfToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, "/planos/novo");

        var formData = new Dictionary<string, string>
        {
            ["Input.Name"] = "Analista Judiciario TRT",
            ["Input.Description"] = "Plano focado no concurso do TRT",
            ["Input.ScoringRule"] = "Cebraspe",
            ["Input.PeriodKind"] = "All",
            ["Input.LowBandThreshold"] = "50",
            ["Input.HighBandThreshold"] = "75",
            ["Input.ExamDate"] = "2026-12-01",
            ["Input.DefaultMockExamQuestions"] = "120",
            ["Input.FitnessTestEnabled"] = "true"
        };

        using var postResponse = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/planos/novo",
            formData,
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        var location = postResponse.Headers.Location?.OriginalString;
        Assert.NotNull(location);
        Assert.StartsWith("/planos/", location, StringComparison.Ordinal);

        var (indexResponse, indexDoc, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, "/planos");
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("Analista Judiciario TRT", indexDoc.Body!.TextContent, StringComparison.Ordinal);
        Assert.Contains("Cebraspe (certa − errada)", indexDoc.Body.TextContent, StringComparison.Ordinal);

        var (detailsResponse, detailsDoc, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, location);
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Analista Judiciario TRT", detailsDoc.Body!.TextContent, StringComparison.Ordinal);
        Assert.Contains("Plano focado no concurso do TRT", detailsDoc.Body.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SectionAndSubjectLifecycle_CanAddReorderMoveAndDeleteSection()
    {
        var store = new TestPlanningStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true };

        var plan = StudyPlan.Create(
            currentUser.Id,
            "Policia Federal - Agente",
            "Concurso PF",
            ScoringRule.Cebraspe,
            PerformancePeriod.All(),
            PerformanceBands.CreateDefault(),
            new DateOnly(2026, 11, 15),
            120,
            true).Data;

        await store.AddStudyPlanAsync(plan, TestContext.Current.CancellationToken);

        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var planUrl = $"/planos/{plan.Id.Value}";
        var (_, _, csrfHeader, csrfToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, planUrl);

        using var addSection1Response = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{planUrl}?handler=AddSection",
            new() { ["name"] = "Bloco I - Conhecimentos Basicos", ["expectedPlanVersion"] = plan.Version.ToString(CultureInfo.InvariantCulture) },
            csrfHeader,
            csrfToken);
        Assert.Equal(HttpStatusCode.Redirect, addSection1Response.StatusCode);

        var currentPlan = (await store.GetStudyPlanByIdAsync(plan.Id, TestContext.Current.CancellationToken))!;
        var section1Id = currentPlan.Sections[0].Id;

        using var addSection2Response = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{planUrl}?handler=AddSection",
            new() { ["name"] = "Bloco II - Especificos", ["expectedPlanVersion"] = currentPlan.Version.ToString(CultureInfo.InvariantCulture) },
            csrfHeader,
            csrfToken);
        Assert.Equal(HttpStatusCode.Redirect, addSection2Response.StatusCode);

        currentPlan = (await store.GetStudyPlanByIdAsync(plan.Id, TestContext.Current.CancellationToken))!;
        Assert.Equal(2, currentPlan.Sections.Count);
        var section2Id = currentPlan.Sections[1].Id;

        using var addSubject1Response = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{planUrl}?handler=AddSubject",
            new() { ["name"] = "Lingua Portuguesa", ["sectionId"] = section1Id.Value.ToString(), ["expectedPlanVersion"] = currentPlan.Version.ToString(CultureInfo.InvariantCulture) },
            csrfHeader,
            csrfToken);
        Assert.Equal(HttpStatusCode.Redirect, addSubject1Response.StatusCode);

        currentPlan = (await store.GetStudyPlanByIdAsync(plan.Id, TestContext.Current.CancellationToken))!;
        using var addSubject2Response = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{planUrl}?handler=AddSubject",
            new() { ["name"] = "Informatica", ["sectionId"] = section1Id.Value.ToString(), ["expectedPlanVersion"] = currentPlan.Version.ToString(CultureInfo.InvariantCulture) },
            csrfHeader,
            csrfToken);
        Assert.Equal(HttpStatusCode.Redirect, addSubject2Response.StatusCode);

        var subjects = await store.ListSubjectsByPlanIdAsync(plan.Id, TestContext.Current.CancellationToken);
        Assert.Equal(2, subjects.Count);
        var subject1 = subjects.First(s => s.Name == "Lingua Portuguesa");
        var subject2 = subjects.First(s => s.Name == "Informatica");

        currentPlan = (await store.GetStudyPlanByIdAsync(plan.Id, TestContext.Current.CancellationToken))!;
        using var reorderSubjectsResponse = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{planUrl}?handler=ReorderSubjects",
            new()
            {
                ["sectionId"] = section1Id.Value.ToString(),
                ["subjectId"] = subject1.Id.Value.ToString(),
                ["direction"] = "down",
                ["expectedPlanVersion"] = currentPlan.Version.ToString(CultureInfo.InvariantCulture)
            },
            csrfHeader,
            csrfToken);
        Assert.Equal(HttpStatusCode.Redirect, reorderSubjectsResponse.StatusCode);

        currentPlan = (await store.GetStudyPlanByIdAsync(plan.Id, TestContext.Current.CancellationToken))!;
        using var moveSubjectResponse = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{planUrl}?handler=MoveSubject",
            new()
            {
                ["subjectId"] = subject2.Id.Value.ToString(),
                ["newSectionId"] = section2Id.Value.ToString(),
                ["expectedPlanVersion"] = currentPlan.Version.ToString(CultureInfo.InvariantCulture)
            },
            csrfHeader,
            csrfToken);
        Assert.Equal(HttpStatusCode.Redirect, moveSubjectResponse.StatusCode);

        currentPlan = (await store.GetStudyPlanByIdAsync(plan.Id, TestContext.Current.CancellationToken))!;
        using var deleteSectionResponse = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{planUrl}?handler=DeleteSection",
            new()
            {
                ["sectionId"] = section2Id.Value.ToString(),
                ["expectedPlanVersion"] = currentPlan.Version.ToString(CultureInfo.InvariantCulture)
            },
            csrfHeader,
            csrfToken);
        Assert.Equal(HttpStatusCode.Redirect, deleteSectionResponse.StatusCode);

        currentPlan = (await store.GetStudyPlanByIdAsync(plan.Id, TestContext.Current.CancellationToken))!;
        Assert.Single(currentPlan.Sections);

        var unassignedPlacement = currentPlan.Outline.First(p => p.SubjectId == subject2.Id);
        Assert.Null(unassignedPlacement.SectionId);

        var (_, detailsDoc, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, planUrl);
        Assert.Contains("Disciplinas sem bloco", detailsDoc.Body!.TextContent, StringComparison.Ordinal);
        Assert.Contains("Informatica", detailsDoc.Body.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TopicTreeAndStatus_CanAddTopicsChangeStatusStandardAndHtmxAndReorder()
    {
        var store = new TestPlanningStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true };

        var plan = StudyPlan.Create(
            currentUser.Id,
            "Receita Federal - Auditor",
            "Plano RFB",
            ScoringRule.Standard,
            PerformancePeriod.All(),
            PerformanceBands.CreateDefault(),
            null,
            120,
            false).Data;

        await store.AddStudyPlanAsync(plan, TestContext.Current.CancellationToken);

        var subject = Subject.Create(currentUser.Id, plan.Id, "Direito Tributario").Data;
        plan.PlaceSubject(subject.Id, null);
        await store.AddSubjectAsync(subject, TestContext.Current.CancellationToken);

        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var subjectUrl = $"/planos/{plan.Id.Value}/disciplinas/{subject.Id.Value}";
        var (_, _, csrfHeader, csrfToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, subjectUrl);

        using var addRootTopicResponse = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{subjectUrl}?handler=AddTopic",
            new()
            {
                ["title"] = "Sistema Tributario Nacional",
                ["code"] = "1",
                ["parentId"] = string.Empty,
                ["initialStatus"] = "ToStudy",
                ["expectedSubjectVersion"] = subject.Version.ToString(CultureInfo.InvariantCulture)
            },
            csrfHeader,
            csrfToken);
        Assert.Equal(HttpStatusCode.Redirect, addRootTopicResponse.StatusCode);

        var updatedSubject = (await store.GetSubjectByIdAsync(subject.Id, TestContext.Current.CancellationToken))!;
        var rootTopic = updatedSubject.Topics.First(t => t.Title == "Sistema Tributario Nacional");

        using var addSubtopicResponse = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{subjectUrl}?handler=AddTopic",
            new()
            {
                ["title"] = "Principios Gerais",
                ["code"] = "1.1",
                ["parentId"] = rootTopic.Id.Value.ToString(),
                ["initialStatus"] = "ToStudy",
                ["expectedSubjectVersion"] = updatedSubject.Version.ToString(CultureInfo.InvariantCulture)
            },
            csrfHeader,
            csrfToken);
        Assert.Equal(HttpStatusCode.Redirect, addSubtopicResponse.StatusCode);

        updatedSubject = (await store.GetSubjectByIdAsync(subject.Id, TestContext.Current.CancellationToken))!;
        var subtopic = updatedSubject.Topics.First(t => t.Title == "Principios Gerais");

        using var changeStatusStdResponse = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{subjectUrl}?handler=ChangeStatus",
            new()
            {
                ["topicId"] = subtopic.Id.Value.ToString(),
                ["newStatus"] = "Studying"
            },
            csrfHeader,
            csrfToken);
        Assert.Equal(HttpStatusCode.Redirect, changeStatusStdResponse.StatusCode);

        updatedSubject = (await store.GetSubjectByIdAsync(subject.Id, TestContext.Current.CancellationToken))!;
        Assert.Equal(TopicStatus.Studying, updatedSubject.Topics.First(t => t.Id == subtopic.Id).Status);

        using var changeStatusHtmxResponse = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{subjectUrl}?handler=ChangeStatus",
            new()
            {
                ["topicId"] = subtopic.Id.Value.ToString(),
                ["newStatus"] = "Reviewing"
            },
            csrfHeader,
            csrfToken,
            customHeaders: new() { ["HX-Request-Type"] = "partial" });

        Assert.Equal(HttpStatusCode.OK, changeStatusHtmxResponse.StatusCode);
        var htmxHtml = await changeStatusHtmxResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Revisando", htmxHtml, StringComparison.Ordinal);
        Assert.Contains("badge-status-reviewing", htmxHtml, StringComparison.Ordinal);

        using var deleteTopicResponse = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{subjectUrl}?handler=DeleteTopic",
            new()
            {
                ["topicId"] = rootTopic.Id.Value.ToString()
            },
            csrfHeader,
            csrfToken);
        Assert.Equal(HttpStatusCode.Redirect, deleteTopicResponse.StatusCode);

        updatedSubject = (await store.GetSubjectByIdAsync(subject.Id, TestContext.Current.CancellationToken))!;
        Assert.Empty(updatedSubject.Topics);
    }

    [Fact]
    public async Task OptimisticConcurrencyConflict_PreservesUserInputAndReturns409()
    {
        var store = new TestPlanningStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true };

        var plan = StudyPlan.Create(
            currentUser.Id,
            "Original Plan Title",
            "Original Description",
            ScoringRule.Cebraspe,
            PerformancePeriod.All(),
            PerformanceBands.CreateDefault(),
            null,
            120,
            false).Data;

        await store.AddStudyPlanAsync(plan, TestContext.Current.CancellationToken);

        var subject = Subject.Create(currentUser.Id, plan.Id, "Original Subject").Data;
        plan.PlaceSubject(subject.Id, null);
        await store.AddSubjectAsync(subject, TestContext.Current.CancellationToken);

        var addTopicResult = subject.AddTopic("Original Topic", null, null, TopicStatus.ToStudy);
        var topic = addTopicResult.Data;
        var note = TopicNote.Create(currentUser.Id, topic.Id, "Original Note Content").Data;
        await store.AddTopicNoteAsync(note, TestContext.Current.CancellationToken);

        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var editPlanUrl = $"/planos/{plan.Id.Value}/editar";
        var (_, _, csrfHeader, csrfToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, editPlanUrl);

        var planFormData = new Dictionary<string, string>
        {
            ["Input.ExpectedVersion"] = "999",
            ["Input.Name"] = "Edited Title by User A",
            ["Input.Description"] = "Edited Description by User A",
            ["Input.ScoringRule"] = "Cebraspe",
            ["Input.PeriodKind"] = "All",
            ["Input.LowBandThreshold"] = "50",
            ["Input.HighBandThreshold"] = "70",
            ["Input.DefaultMockExamQuestions"] = "120"
        };

        using var planResponse = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            editPlanUrl,
            planFormData,
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.Conflict, planResponse.StatusCode);
        var planHtml = await planResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Este item foi atualizado em outro aparelho ou aba", planHtml, StringComparison.Ordinal);
        Assert.Contains("Edited Title by User A", planHtml, StringComparison.Ordinal);
        Assert.Contains("Edited Description by User A", planHtml, StringComparison.Ordinal);

        var subjectUrl = $"/planos/{plan.Id.Value}/disciplinas/{subject.Id.Value}";
        var (_, _, subjectCsrfHeader, subjectCsrfToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, subjectUrl);

        var subjectFormData = new Dictionary<string, string>
        {
            ["name"] = "My Unsaved Subject Name",
            ["expectedVersion"] = "999"
        };

        using var subjectResponse = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{subjectUrl}?handler=UpdateSubject",
            subjectFormData,
            subjectCsrfHeader,
            subjectCsrfToken);

        Assert.Equal(HttpStatusCode.Conflict, subjectResponse.StatusCode);
        var subjectHtml = await subjectResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Este item foi atualizado em outro aparelho ou aba", subjectHtml, StringComparison.Ordinal);
        Assert.Contains("My Unsaved Subject Name", subjectHtml, StringComparison.Ordinal);

        var noteFormData = new Dictionary<string, string>
        {
            ["topicId"] = topic.Id.Value.ToString(),
            ["expectedVersion"] = "999",
            ["markdown"] = "My Unsaved Typed Notes that should never be lost"
        };

        using var noteResponse = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{subjectUrl}?handler=SaveNote",
            noteFormData,
            subjectCsrfHeader,
            subjectCsrfToken);

        Assert.Equal(HttpStatusCode.Conflict, noteResponse.StatusCode);
        var noteHtml = await noteResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Este item foi atualizado em outro aparelho ou aba", noteHtml, StringComparison.Ordinal);
        Assert.Contains("My Unsaved Typed Notes that should never be lost", noteHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SafeMarkdown_EscapesHtmlAndRendersMarkdownFormatting()
    {
        var store = new TestPlanningStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true };

        var plan = StudyPlan.Create(
            currentUser.Id,
            "Plano Markdown Test",
            null,
            ScoringRule.Cebraspe,
            PerformancePeriod.All(),
            PerformanceBands.CreateDefault(),
            null,
            120,
            false).Data;

        await store.AddStudyPlanAsync(plan, TestContext.Current.CancellationToken);

        var subject = Subject.Create(currentUser.Id, plan.Id, "Informatica").Data;
        plan.PlaceSubject(subject.Id, null);
        await store.AddSubjectAsync(subject, TestContext.Current.CancellationToken);

        var topic = subject.AddTopic("Seguranca da Informacao", null, null, TopicStatus.ToStudy).Data;
        var markdownText = "# Titulo Principal\n\n**texto em negrito** e *italico*.\n\n<script>alert('xss-attack')</script>";
        var note = TopicNote.Create(currentUser.Id, topic.Id, markdownText).Data;
        await store.AddTopicNoteAsync(note, TestContext.Current.CancellationToken);

        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var subjectUrl = $"/planos/{plan.Id.Value}/disciplinas/{subject.Id.Value}?noteTopicId={topic.Id.Value}";
        var (response, doc, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, subjectUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("<strong>texto em negrito</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<em>italico</em>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert('xss-attack')</script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert('xss-attack')&lt;/script&gt;", html, StringComparison.Ordinal);

        Assert.Null(doc.QuerySelector("div.prose script"));
    }

    [Fact]
    public async Task SearchAndFilterTopics_FindsMatchesAccentsInsensitive()
    {
        var store = new TestPlanningStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true };

        var plan = StudyPlan.Create(
            currentUser.Id,
            "Plano Busca e Filtros",
            null,
            ScoringRule.Cebraspe,
            PerformancePeriod.All(),
            PerformanceBands.CreateDefault(),
            null,
            120,
            false).Data;

        await store.AddStudyPlanAsync(plan, TestContext.Current.CancellationToken);

        var subject1 = Subject.Create(currentUser.Id, plan.Id, "Direito Constitucional").Data;
        plan.PlaceSubject(subject1.Id, null);
        await store.AddSubjectAsync(subject1, TestContext.Current.CancellationToken);

        var subject2 = Subject.Create(currentUser.Id, plan.Id, "Lingua Portuguesa").Data;
        plan.PlaceSubject(subject2.Id, null);
        await store.AddSubjectAsync(subject2, TestContext.Current.CancellationToken);

        var code1 = TopicCode.Create("1").Data;
        var code2 = TopicCode.Create("2").Data;
        subject1.AddTopic("Organização Político-Administrativa do Estado", code1, null, TopicStatus.Studying);
        subject1.AddTopic("Direitos Fundamentais", code2, null, TopicStatus.ToStudy);
        subject2.AddTopic("Acentuação Gráfica e Ortografia", code1, null, TopicStatus.Studied);

        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var planUrl = $"/planos/{plan.Id.Value}";

        var (searchResp, searchDoc, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(
            client,
            $"{planUrl}?q=organizacao");

        Assert.Equal(HttpStatusCode.OK, searchResp.StatusCode);
        Assert.Contains("Organização Político-Administrativa do Estado", searchDoc.Body!.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Acentuação Gráfica", searchDoc.Body.TextContent, StringComparison.Ordinal);

        var (filterStatusResp, filterStatusDoc, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(
            client,
            $"{planUrl}?filterStatus=Studying");

        Assert.Equal(HttpStatusCode.OK, filterStatusResp.StatusCode);
        Assert.Contains("Organização Político-Administrativa do Estado", filterStatusDoc.Body!.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Direitos Fundamentais", filterStatusDoc.Body.TextContent, StringComparison.Ordinal);

        var (filterSubjectResp, filterSubjectDoc, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(
            client,
            $"{planUrl}?filterSubjectId={subject2.Id.Value}");

        Assert.Equal(HttpStatusCode.OK, filterSubjectResp.StatusCode);
        Assert.Contains("Acentuação Gráfica e Ortografia", filterSubjectDoc.Body!.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Organização Político-Administrativa do Estado", filterSubjectDoc.Body.TextContent, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task PlanningFlow_OnRealDatabase_BuildsSubjectTreeChangesStatusAndRendersNote(DatabaseProvider provider)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var factory = TrailWebApplicationFactory.Create(provider, new Dictionary<string, string?>
        {
            ["Email:Smtp:Host"] = "",
            ["Registration:Mode"] = "Open"
        });

        var localizer = factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        using var registerResponse = await AccountTestHelper.RegisterAsync(client, "candidate@trail.local", "CandidatePass123!");
        Assert.Equal(HttpStatusCode.Redirect, registerResponse.StatusCode);

        using var signInResponse = await AccountTestHelper.SignInAsync(client, "candidate@trail.local", "CandidatePass123!");
        Assert.Equal(HttpStatusCode.Redirect, signInResponse.StatusCode);

        var (_, _, createHeader, createToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, "/planos/novo");
        using var createPlanResponse = await PlanningTestHelper.PostFormWithAntiforgeryAsync(client, "/planos/novo", new()
        {
            ["Input.Name"] = "Tax Auditor",
            ["Input.ScoringRule"] = "Cebraspe",
            ["Input.PeriodKind"] = "All",
            ["Input.LowBandThreshold"] = "50",
            ["Input.HighBandThreshold"] = "75",
            ["Input.DefaultMockExamQuestions"] = "120"
        }, createHeader, createToken);
        Assert.Equal(HttpStatusCode.Redirect, createPlanResponse.StatusCode);
        var planUrl = createPlanResponse.Headers.Location?.OriginalString;
        Assert.NotNull(planUrl);

        var (_, planPage, planHeader, planToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, planUrl);
        using var addSectionResponse = await PlanningTestHelper.PostFormWithAntiforgeryAsync(client, $"{planUrl}?handler=AddSection", new()
        {
            ["name"] = "Specific Knowledge",
            ["expectedPlanVersion"] = FormValue(planPage, "AddSection", "expectedPlanVersion")
        }, planHeader, planToken);
        Assert.Equal(HttpStatusCode.Redirect, addSectionResponse.StatusCode);

        (_, planPage, planHeader, planToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, planUrl);
        var sectionOption = planPage.QuerySelectorAll("form[action*='handler=AddSubject'] select[name='sectionId'] option")
            .Single(option => option.TextContent.Trim() == "Specific Knowledge");
        using var addSubjectResponse = await PlanningTestHelper.PostFormWithAntiforgeryAsync(client, $"{planUrl}?handler=AddSubject", new()
        {
            ["name"] = "Tax Law",
            ["sectionId"] = sectionOption.GetAttribute("value")!,
            ["expectedPlanVersion"] = FormValue(planPage, "AddSubject", "expectedPlanVersion")
        }, planHeader, planToken);
        Assert.Equal(HttpStatusCode.Redirect, addSubjectResponse.StatusCode);

        (_, planPage, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, planUrl);
        var subjectUrl = planPage.QuerySelectorAll("a[href*='/disciplinas/']")
            .Single(link => link.TextContent.Trim() == "Tax Law")
            .GetAttribute("href");
        Assert.NotNull(subjectUrl);

        var (_, subjectPage, subjectHeader, subjectToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, subjectUrl);
        using var addTopicResponse = await PlanningTestHelper.PostFormWithAntiforgeryAsync(client, $"{subjectUrl}?handler=AddTopic", new()
        {
            ["code"] = "1",
            ["title"] = "Competência Tributária",
            ["initialStatus"] = "ToStudy",
            ["expectedSubjectVersion"] = FormValue(subjectPage, "AddTopic", "expectedSubjectVersion")
        }, subjectHeader, subjectToken);
        Assert.Equal(HttpStatusCode.Redirect, addTopicResponse.StatusCode);

        (_, subjectPage, subjectHeader, subjectToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, subjectUrl);
        var parentOption = subjectPage.QuerySelectorAll("#add-topic-form select[name='parentId'] option")
            .Single(option => option.TextContent.Contains("Competência Tributária", StringComparison.Ordinal));
        var rootTopicId = parentOption.GetAttribute("value")!;
        using var addSubtopicResponse = await PlanningTestHelper.PostFormWithAntiforgeryAsync(client, $"{subjectUrl}?handler=AddTopic", new()
        {
            ["code"] = "1.1",
            ["title"] = "Limits on the Power to Tax",
            ["parentId"] = rootTopicId,
            ["initialStatus"] = "ToStudy",
            ["expectedSubjectVersion"] = FormValue(subjectPage, "AddTopic", "expectedSubjectVersion")
        }, subjectHeader, subjectToken);
        Assert.Equal(HttpStatusCode.Redirect, addSubtopicResponse.StatusCode);

        (_, subjectPage, subjectHeader, subjectToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, subjectUrl);
        var treeItems = subjectPage.QuerySelectorAll("#topic-tree-container li");
        Assert.Equal(2, treeItems.Length);
        Assert.Contains("pl-6", treeItems.Single(item => item.TextContent.Contains("Limits on the Power to Tax", StringComparison.Ordinal)).ClassName, StringComparison.Ordinal);

        using var changeStatusResponse = await PlanningTestHelper.PostFormWithAntiforgeryAsync(client, $"{subjectUrl}?handler=ChangeStatus", new()
        {
            ["topicId"] = rootTopicId,
            ["newStatus"] = "Studying"
        }, subjectHeader, subjectToken);
        Assert.Equal(HttpStatusCode.Redirect, changeStatusResponse.StatusCode);

        var (_, notePage, noteHeader, noteToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, $"{subjectUrl}?noteTopicId={rootTopicId}");
        var rootItem = notePage.QuerySelectorAll("#topic-tree-container li")
            .Single(item => item.TextContent.Contains("Competência Tributária", StringComparison.Ordinal));
        var statusBadge = Assert.Single(rootItem.QuerySelectorAll("[class*='badge-status-']"));
        Assert.Contains("badge-status-studying", statusBadge.ClassName, StringComparison.Ordinal);
        Assert.Equal(localizer["TopicStatus_Studying"].Value, statusBadge.TextContent.Trim());

        using var saveNoteResponse = await PlanningTestHelper.PostFormWithAntiforgeryAsync(client, $"{subjectUrl}?handler=SaveNote", new()
        {
            ["topicId"] = rootTopicId,
            ["expectedVersion"] = notePage.QuerySelector("#save-note-form input[name='expectedVersion']")?.GetAttribute("value") ?? throw new InvalidOperationException("The note form has no version."),
            ["markdown"] = "### Constitutional limits\n\nThe Union cannot tax the income of the States."
        }, noteHeader, noteToken);
        Assert.Equal(HttpStatusCode.Redirect, saveNoteResponse.StatusCode);

        var (_, savedNotePage, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, saveNoteResponse.Headers.Location!.OriginalString);
        Assert.Equal("Constitutional limits", savedNotePage.QuerySelector("#topic-notes-container .prose h3")?.TextContent.Trim());
        Assert.Equal("The Union cannot tax the income of the States.", savedNotePage.QuerySelector("#topic-notes-container .prose p")?.TextContent.Trim());

        var (searchResponse, searchPage, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, $"{planUrl}?q=competencia");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        var resultTitles = searchPage.QuerySelectorAll("#search-results-container tbody tr td:nth-child(3)")
            .Select(cell => cell.TextContent.Trim())
            .ToList();
        Assert.Equal("Competência Tributária", Assert.Single(resultTitles));
    }

    [Fact]
    public async Task ChangeStatus_WithHtmxPartialRequest_SwapsTreeContentKeepingASingleContainer()
    {
        var store = new TestPlanningStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true };
        var (plan, subject, seeded) = await SeedPlanWithTreeAsync(store, currentUser, "Alpha");

        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var localizer = factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>();

        var subjectUrl = $"/planos/{plan.Id.Value}/disciplinas/{subject.Id.Value}";
        var (pageResponse, page, csrfHeader, csrfToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, subjectUrl);
        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);

        var container = Assert.Single(page.QuerySelectorAll("#topic-tree-container"));
        var statusForm = container.QuerySelectorAll("form[hx-post*='handler=ChangeStatus']")
            .Single(form => form.QuerySelector($"input[name='topicId'][value='{seeded.TopicId}']") is not null);
        Assert.Equal("#topic-tree-container", statusForm.GetAttribute("hx-target"));
        Assert.Null(statusForm.GetAttribute("hx-swap"));

        using var response = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{subjectUrl}?handler=ChangeStatus",
            new()
            {
                ["topicId"] = seeded.TopicId.ToString(),
                ["newStatus"] = "Studying"
            },
            csrfHeader,
            csrfToken,
            customHeaders: new() { [HtmxIntegration.RequestTypeHeaderName] = HtmxIntegration.PartialRequestType });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        container.InnerHtml = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Single(page.QuerySelectorAll("#topic-tree-container"));
        var topicItem = container.QuerySelectorAll("li")
            .Single(item => item.QuerySelector($"input[name='topicId'][value='{seeded.TopicId}']") is not null);
        var statusBadge = Assert.Single(topicItem.QuerySelectorAll("[class*='badge-status-']"));
        Assert.Contains("badge-status-studying", statusBadge.ClassName, StringComparison.Ordinal);
        Assert.Equal(localizer["TopicStatus_Studying"].Value, statusBadge.TextContent.Trim());
        Assert.Equal("Studying", topicItem.QuerySelector("select[name='newStatus'] option[selected]")?.GetAttribute("value"));
    }

    [Fact]
    public async Task TopicNote_WithoutHtmxHeader_RendersTheFullSubjectPage()
    {
        var store = new TestPlanningStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true };
        var (plan, subject, seeded) = await SeedPlanWithTreeAsync(store, currentUser, "Alpha");

        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var (response, document, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(
            client,
            $"/planos/{plan.Id.Value}/disciplinas/{subject.Id.Value}?handler=TopicNote&topicId={seeded.TopicId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(document.QuerySelector("main#main-content"));
        Assert.Equal(seeded.SubjectName, document.QuerySelector("main#main-content h1")?.TextContent.Trim());
        Assert.Single(document.QuerySelectorAll("#topic-tree-container"));
        var notes = Assert.Single(document.QuerySelectorAll("#topic-notes-container"));
        Assert.Equal(seeded.NoteMarkdown, notes.QuerySelector(".prose p")?.TextContent.Trim());
    }

    [Fact]
    public async Task TopicNote_WithHtmxPartialRequest_RendersOnlyTheNotePartial()
    {
        var store = new TestPlanningStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true };
        var (plan, subject, seeded) = await SeedPlanWithTreeAsync(store, currentUser, "Alpha");

        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/planos/{plan.Id.Value}/disciplinas/{subject.Id.Value}?handler=TopicNote&topicId={seeded.TopicId}");
        request.Headers.Add(HtmxIntegration.RequestTypeHeaderName, HtmxIntegration.PartialRequestType);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("<html", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("main-content", html, StringComparison.Ordinal);

        var fragment = new HtmlParser().ParseDocument($"<body>{html}</body>");
        var notes = Assert.Single(fragment.QuerySelectorAll("#topic-notes-container"));
        Assert.Equal(seeded.NoteMarkdown, notes.QuerySelector(".prose p")?.TextContent.Trim());
    }

    [Theory]
    [InlineData("plan", "DoesNotExist")]
    [InlineData("subject", "DoesNotExist")]
    [InlineData("plan", null)]
    [InlineData("subject", null)]
    [InlineData("plans", null)]
    public async Task Post_WithoutAMatchingHandler_ReturnsNotFound(string page, string? handler)
    {
        var store = new TestPlanningStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true };
        var (plan, subject, _) = await SeedPlanWithTreeAsync(store, currentUser, "Alpha");

        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var (_, _, csrfHeader, csrfToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, "/planos");

        var pageUrl = page switch
        {
            "plan" => $"/planos/{plan.Id.Value}",
            "subject" => $"/planos/{plan.Id.Value}/disciplinas/{subject.Id.Value}",
            _ => "/planos",
        };
        var url = handler is null ? pageUrl : $"{pageUrl}?handler={handler}";

        using var response = await PlanningTestHelper.PostFormWithAntiforgeryAsync(client, url, [], csrfHeader, csrfToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(false, "?handler=DoesNotExist")]
    [InlineData(true, "?handler=DoesNotExist")]
    [InlineData(false, "?handler=AddSection&handler=DeletePlan")]
    [InlineData(true, "?handler=AddTopic&handler=DeleteTopic")]
    public async Task Get_WithAHandlerNameWithoutGetHandler_FallsBackToThePage(bool onSubjectPage, string query)
    {
        var store = new TestPlanningStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true };
        var (plan, subject, seeded) = await SeedPlanWithTreeAsync(store, currentUser, "Alpha");

        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var pageUrl = onSubjectPage
            ? $"/planos/{plan.Id.Value}/disciplinas/{subject.Id.Value}"
            : $"/planos/{plan.Id.Value}";
        var (response, document, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, pageUrl + query);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            onSubjectPage ? seeded.SubjectName : seeded.PlanName,
            document.QuerySelector("main#main-content h1")?.TextContent.Trim());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ConflictReloadLink_TargetsThePageAndAnswersOk(bool onSubjectPage, bool htmx)
    {
        var store = new TestPlanningStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true };
        var (plan, subject, seeded) = await SeedPlanWithTreeAsync(store, currentUser, "Alpha");

        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var localizer = factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>();

        var pageUrl = onSubjectPage
            ? $"/planos/{plan.Id.Value}/disciplinas/{subject.Id.Value}"
            : $"/planos/{plan.Id.Value}";
        var (_, _, csrfHeader, csrfToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, pageUrl);

        var staleForm = onSubjectPage
            ? new Dictionary<string, string>
            {
                ["title"] = "Stale topic",
                ["initialStatus"] = "ToStudy",
                ["expectedSubjectVersion"] = (seeded.SubjectVersion + 1).ToString(CultureInfo.InvariantCulture)
            }
            : new Dictionary<string, string>
            {
                ["name"] = "Stale section",
                ["expectedPlanVersion"] = (seeded.PlanVersion + 1).ToString(CultureInfo.InvariantCulture)
            };

        using var conflictResponse = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{pageUrl}?handler={(onSubjectPage ? "AddTopic" : "AddSection")}",
            staleForm,
            csrfHeader,
            csrfToken,
            htmx ? new() { [HtmxIntegration.RequestTypeHeaderName] = HtmxIntegration.PartialRequestType } : null);

        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        var conflictDocument = new HtmlParser().ParseDocument(await conflictResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var reloadLink = conflictDocument.QuerySelectorAll("a")
            .Single(link => link.TextContent.Trim() == localizer["Reload"].Value);
        Assert.Equal(pageUrl, reloadLink.GetAttribute("href"));

        var (reloadResponse, reloadedPage, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, reloadLink.GetAttribute("href")!);
        Assert.Equal(HttpStatusCode.OK, reloadResponse.StatusCode);
        Assert.Equal(
            onSubjectPage ? seeded.SubjectName : seeded.PlanName,
            reloadedPage.QuerySelector("main#main-content h1")?.TextContent.Trim());
    }

    [Theory]
    [InlineData("GET", "SearchTopics", false)]
    [InlineData("GET", "SearchTopics", true)]
    [InlineData("POST", "AddSection", false)]
    [InlineData("POST", "AddSection", true)]
    public async Task UnexpectedException_OnARequestWithAHandler_RendersTheErrorPage(string method, string handler, bool htmx)
    {
        var currentUser = new TestCurrentUser { IsAuthenticated = true };
        var failingStore = DispatchProxy.Create<IPlanningStore, FailingPlanningStore>();

        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, failingStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var localizer = factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>();
        var (_, _, csrfHeader, csrfToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, "/planos/novo");

        using var request = new HttpRequestMessage(new HttpMethod(method), $"/planos/{Guid.NewGuid()}?handler={handler}");
        if (method == "POST")
        {
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["name"] = "Section", ["expectedPlanVersion"] = "0" });
            request.Headers.Add(csrfHeader!, csrfToken);
        }

        if (htmx)
        {
            request.Headers.Add(HtmxIntegration.RequestTypeHeaderName, HtmxIntegration.PartialRequestType);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var document = new HtmlParser().ParseDocument(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(localizer["ErrorTitle"].Value, document.QuerySelector("h1")?.TextContent.Trim());
        Assert.NotNull(document.QuerySelector("p.font-mono"));
        Assert.Equal(!htmx, document.QuerySelector("main#main-content") is not null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ErrorPage_WithAHandlerInTheQuery_StillRendersTheErrorPage(bool post)
    {
        using var client = baseFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var localizer = baseFactory.Services.GetRequiredService<IStringLocalizer<SharedResource>>();

        using var response = post
            ? await client.PostAsync("/Error?handler=AddSection", new StringContent(string.Empty), TestContext.Current.CancellationToken)
            : await client.GetAsync("/Error?handler=AddSection", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var document = new HtmlParser().ParseDocument(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(localizer["ErrorTitle"].Value, document.QuerySelector("main h1")?.TextContent.Trim());
    }

    [Fact]
    public async Task StatusChangeAndSearch_OnSuccess_AreRecordedThroughTheResultResponder()
    {
        var store = new TestPlanningStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true };
        var (plan, subject, seeded) = await SeedPlanWithTreeAsync(store, currentUser, "Alpha");

        var serverSpans = new ServerSpanRecorder();
        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, currentUser).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddOpenTelemetry().WithTracing(tracing => tracing.AddProcessor(serverSpans))));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var useCaseResults = new UseCaseResultRecorder(factory.Services.GetRequiredService<ApplicationMetrics>());

        var planUrl = $"/planos/{plan.Id.Value}";
        var subjectUrl = $"{planUrl}/disciplinas/{subject.Id.Value}";
        var (_, _, csrfHeader, csrfToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, subjectUrl);

        using var statusRequest = new HttpRequestMessage(HttpMethod.Post, $"{subjectUrl}?handler=ChangeStatus")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["topicId"] = seeded.TopicId.ToString(), ["newStatus"] = "Studying" }),
        };
        statusRequest.Headers.Add(csrfHeader!, csrfToken);
        statusRequest.Headers.Add(HtmxIntegration.RequestTypeHeaderName, HtmxIntegration.PartialRequestType);
        var statusTrace = serverSpans.Trace(statusRequest);
        using var statusResponse = await client.SendAsync(statusRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        using var htmxSearchRequest = new HttpRequestMessage(HttpMethod.Get, $"{planUrl}?handler=SearchTopics&q=alpha+topic");
        htmxSearchRequest.Headers.Add(HtmxIntegration.RequestTypeHeaderName, HtmxIntegration.PartialRequestType);
        var htmxSearchTrace = serverSpans.Trace(htmxSearchRequest);
        using var htmxSearchResponse = await client.SendAsync(htmxSearchRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, htmxSearchResponse.StatusCode);
        var searchResults = new HtmlParser().ParseDocument(await htmxSearchResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var topicLink = searchResults.QuerySelectorAll("a").First(link => link.TextContent.Trim() == seeded.TopicTitle);
        Assert.Equal(subjectUrl, topicLink.GetAttribute("href"));

        using var searchRequest = new HttpRequestMessage(HttpMethod.Get, $"{planUrl}?handler=SearchTopics&q=alpha+topic");
        var searchTrace = serverSpans.Trace(searchRequest);
        using var searchResponse = await client.SendAsync(searchRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, searchResponse.StatusCode);
        var location = new Uri(new Uri("http://localhost"), searchResponse.Headers.Location!);
        Assert.Equal(planUrl, location.AbsolutePath);
        Assert.Equal("alpha topic", Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(location.Query)["q"].ToString());

        foreach (var (trace, useCase) in new[]
        {
            (statusTrace, "planning.change_topic_status"),
            (htmxSearchTrace, "planning.search_topics"),
            (searchTrace, "planning.search_topics"),
        })
        {
            var serverSpan = await trace.ServerSpanEnded;
            Assert.Equal(useCase, serverSpan.GetTagItem("trail.use_case"));
            Assert.Equal("Ok", serverSpan.GetTagItem("trail.result.status"));
        }

        Assert.Equal(1, useCaseResults.Count("planning.change_topic_status", "Ok"));
        Assert.Equal(2, useCaseResults.Count("planning.search_topics", "Ok"));
    }

    [Fact]
    public async Task SubjectHandlers_WithSubjectFromAnotherPlanOfTheSameUser_ReturnNotFoundWithoutChanges()
    {
        var store = new TestPlanningStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true };
        var (_, subject, seeded) = await SeedPlanWithTreeAsync(store, currentUser, "Alpha");
        var (otherPlan, _, _) = await SeedPlanWithTreeAsync(store, currentUser, "Bravo");

        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var (_, _, csrfHeader, csrfToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, "/planos");

        var routeCases = PlanningIsolationSweep
            .SubjectRouteCases("a subject from another plan", otherPlan.Id.Value, subject.Id.Value, seeded, seeded, includeSubjectLevelHandlers: true)
            .ToList();

        var subjectHandlers = typeof(SubjectModel)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Count(m => m.Name.StartsWith("OnGet", StringComparison.Ordinal) || m.Name.StartsWith("OnPost", StringComparison.Ordinal));
        Assert.Equal(subjectHandlers, routeCases.Select(c => $"{c.Method}:{c.Handler}").Distinct().Count());

        foreach (var routeCase in routeCases)
        {
            using var response = await PlanningIsolationSweep.SendAsync(client, routeCase, csrfHeader, csrfToken);
            Assert.True(
                response.StatusCode == HttpStatusCode.NotFound,
                $"{routeCase.Name} ({routeCase.Method} {routeCase.Path}) answered {(int)response.StatusCode} instead of 404.");
        }

        var unchanged = (await store.GetSubjectByIdAsync(subject.Id, TestContext.Current.CancellationToken))!;
        Assert.Equal(seeded.SubjectName, unchanged.Name);
        Assert.Equal(3, unchanged.Topics.Count);
        Assert.All(unchanged.Topics, topic => Assert.Equal(TopicStatus.ToStudy, topic.Status));
        Assert.Equal(seeded.TopicTitle, unchanged.Topics.Single(t => t.Id == TopicId.From(seeded.TopicId)).Title);
        Assert.Equal(TopicId.From(seeded.TopicId), unchanged.Topics.Single(t => t.Id == TopicId.From(seeded.SubtopicId)).ParentId);
        var note = await store.GetTopicNoteByTopicIdAsync(TopicId.From(seeded.TopicId), TestContext.Current.CancellationToken);
        Assert.Equal(seeded.NoteMarkdown, note?.Markdown);
    }

    private static async Task<(StudyPlan Plan, Subject Subject, SeededPlan Seeded)> SeedPlanWithTreeAsync(
        TestPlanningStore store,
        TestCurrentUser currentUser,
        string label)
    {
        var plan = StudyPlan.Create(
            currentUser.Id,
            $"{label} plan",
            null,
            ScoringRule.Cebraspe,
            PerformancePeriod.All(),
            PerformanceBands.CreateDefault(),
            null,
            120,
            false).Data;

        var section = plan.AddSection($"{label} section").Data;
        var spareSection = plan.AddSection($"{label} spare section").Data;
        var subject = Subject.Create(currentUser.Id, plan.Id, $"{label} subject").Data;
        plan.PlaceSubject(subject.Id, section.Id);

        var topic = subject.AddTopic($"{label} topic", TopicCode.Create("1").Data, null, TopicStatus.ToStudy).Data;
        var subtopic = subject.AddTopic($"{label} subtopic", TopicCode.Create("1.1").Data, topic.Id, TopicStatus.ToStudy).Data;
        var siblingTopic = subject.AddTopic($"{label} sibling topic", TopicCode.Create("2").Data, null, TopicStatus.ToStudy).Data;
        var note = TopicNote.Create(currentUser.Id, topic.Id, $"{label} note").Data;

        await store.AddStudyPlanAsync(plan, TestContext.Current.CancellationToken);
        await store.AddSubjectAsync(subject, TestContext.Current.CancellationToken);
        await store.AddTopicNoteAsync(note, TestContext.Current.CancellationToken);

        var seeded = new SeededPlan(
            plan.Id.Value,
            section.Id.Value,
            spareSection.Id.Value,
            subject.Id.Value,
            topic.Id.Value,
            subtopic.Id.Value,
            siblingTopic.Id.Value,
            plan.Version,
            subject.Version,
            note.Version,
            plan.Name,
            section.Name,
            spareSection.Name,
            subject.Name,
            topic.Title,
            subtopic.Title,
            siblingTopic.Title,
            note.Markdown);

        return (plan, subject, seeded);
    }

    [Theory]
    [InlineData("AddSection", "duplicate", false)]
    [InlineData("AddSection", "duplicate", true)]
    [InlineData("AddSection", "empty", false)]
    [InlineData("AddSection", "empty", true)]
    [InlineData("UpdateSection", "duplicate", false)]
    [InlineData("UpdateSection", "duplicate", true)]
    [InlineData("AddSubject", "too-long", false)]
    [InlineData("AddSubject", "too-long", true)]
    public async Task PlanForms_WithInvalidName_Return422WithTheFieldErrorOnTheSubmittedForm(string handler, string scenario, bool htmx)
    {
        var store = new TestPlanningStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true };
        var (plan, _, seeded) = await SeedPlanWithTreeAsync(store, currentUser, "Alpha");

        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var localizer = factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>();

        var planUrl = $"/planos/{plan.Id.Value}";
        var (_, _, csrfHeader, csrfToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, planUrl);

        var submittedName = scenario switch
        {
            "duplicate" => seeded.SectionName,
            "too-long" => new string('x', Subject.MaxNameLength + 1),
            _ => string.Empty,
        };
        var expectedMessage = (handler, scenario) switch
        {
            ("AddSubject", _) => localizer["planning.subject.name_too_long"].Value,
            (_, "duplicate") => localizer["planning.section.duplicate_name"].Value,
            _ => localizer["planning.section.name_required"].Value,
        };

        var form = new Dictionary<string, string>
        {
            ["name"] = submittedName,
            ["expectedPlanVersion"] = seeded.PlanVersion.ToString(CultureInfo.InvariantCulture)
        };
        if (handler == "UpdateSection")
        {
            form["sectionId"] = seeded.SpareSectionId.ToString();
        }

        if (handler == "AddSubject")
        {
            form["sectionId"] = seeded.SectionId.ToString();
        }

        using var response = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{planUrl}?handler={handler}",
            form,
            csrfHeader,
            csrfToken,
            htmx ? new() { [HtmxIntegration.RequestTypeHeaderName] = HtmxIntegration.PartialRequestType } : null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var document = new HtmlParser().ParseDocument(html);

        Assert.Equal(htmx, !html.Contains("<html", StringComparison.OrdinalIgnoreCase));
        if (!htmx)
        {
            Assert.Equal(seeded.PlanName, document.QuerySelector("main#main-content h1")?.TextContent.Trim());
        }

        var invalidInput = Assert.Single(document.QuerySelectorAll("[aria-invalid='true']"));
        var submittedForm = invalidInput.Closest("form");
        Assert.NotNull(submittedForm);
        Assert.Contains($"handler={handler}", submittedForm.GetAttribute("action"), StringComparison.Ordinal);
        if (handler == "UpdateSection")
        {
            Assert.Equal(seeded.SpareSectionId.ToString(), submittedForm.QuerySelector("input[name='sectionId']")?.GetAttribute("value"));
        }

        if (handler == "AddSubject")
        {
            Assert.Equal(seeded.SectionId.ToString(), submittedForm.QuerySelector("select[name='sectionId'] option[selected]")?.GetAttribute("value"));
        }

        Assert.Equal(submittedName, invalidInput.GetAttribute("value") ?? string.Empty);
        var errors = document.GetElementById(invalidInput.GetAttribute("aria-describedby")!);
        Assert.NotNull(errors);
        Assert.Equal(expectedMessage, Assert.Single(errors.QuerySelectorAll("li")).TextContent.Trim());
        Assert.Single(document.All, element => element.ChildElementCount == 0 && element.TextContent.Trim() == expectedMessage);
        Assert.DoesNotContain("field is required", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("UpdateSubject", "empty", false)]
    [InlineData("UpdateSubject", "empty", true)]
    [InlineData("UpdateSubject", "too-long", false)]
    [InlineData("UpdateSubject", "too-long", true)]
    [InlineData("AddTopic", "empty", false)]
    [InlineData("AddTopic", "empty", true)]
    [InlineData("AddTopic", "too-long", false)]
    [InlineData("AddTopic", "too-long", true)]
    [InlineData("AddTopic", "invalid-code", false)]
    [InlineData("AddTopic", "invalid-code", true)]
    [InlineData("UpdateTopic", "empty", false)]
    [InlineData("UpdateTopic", "empty", true)]
    [InlineData("UpdateTopic", "too-long", false)]
    [InlineData("UpdateTopic", "too-long", true)]
    [InlineData("UpdateTopic", "invalid-code", false)]
    [InlineData("UpdateTopic", "invalid-code", true)]
    [InlineData("MoveTopic", "cycle", false)]
    [InlineData("MoveTopic", "cycle", true)]
    [InlineData("SaveNote", "too-long", false)]
    [InlineData("SaveNote", "too-long", true)]
    public async Task SubjectForms_WithInvalidInput_Return422WithTheFieldErrorOnTheSubmittedForm(string handler, string scenario, bool htmx)
    {
        var store = new TestPlanningStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true };
        var (plan, subject, seeded) = await SeedPlanWithTreeAsync(store, currentUser, "Alpha");

        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var localizer = factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>();

        var subjectUrl = $"/planos/{plan.Id.Value}/disciplinas/{subject.Id.Value}";
        var (_, _, csrfHeader, csrfToken) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, subjectUrl);

        var subjectVersion = seeded.SubjectVersion.ToString(CultureInfo.InvariantCulture);
        var (form, submittedValue, messageCode) = (handler, scenario) switch
        {
            ("UpdateSubject", "empty") => (new Dictionary<string, string> { ["name"] = string.Empty, ["expectedVersion"] = subjectVersion }, string.Empty, "planning.subject.name_required"),
            ("UpdateSubject", _) => (new Dictionary<string, string> { ["name"] = new string('s', Subject.MaxNameLength + 1), ["expectedVersion"] = subjectVersion }, new string('s', Subject.MaxNameLength + 1), "planning.subject.name_too_long"),
            ("AddTopic", "empty") => (new Dictionary<string, string> { ["code"] = "9", ["title"] = string.Empty, ["initialStatus"] = "ToStudy", ["expectedSubjectVersion"] = subjectVersion }, string.Empty, "planning.topic.title_required"),
            ("AddTopic", "too-long") => (new Dictionary<string, string> { ["code"] = "9", ["title"] = new string('t', Subject.MaxTopicTitleLength + 1), ["initialStatus"] = "ToStudy", ["expectedSubjectVersion"] = subjectVersion }, new string('t', Subject.MaxTopicTitleLength + 1), "planning.topic.title_too_long"),
            ("AddTopic", _) => (new Dictionary<string, string> { ["code"] = "nine", ["title"] = "Valid title", ["initialStatus"] = "ToStudy", ["expectedSubjectVersion"] = subjectVersion }, "nine", "planning.topic.invalid_code"),
            ("UpdateTopic", "empty") => (new Dictionary<string, string> { ["topicId"] = seeded.TopicId.ToString(), ["code"] = "1", ["title"] = string.Empty, ["expectedSubjectVersion"] = subjectVersion }, string.Empty, "planning.topic.title_required"),
            ("UpdateTopic", "too-long") => (new Dictionary<string, string> { ["topicId"] = seeded.TopicId.ToString(), ["code"] = "1", ["title"] = new string('t', Subject.MaxTopicTitleLength + 1), ["expectedSubjectVersion"] = subjectVersion }, new string('t', Subject.MaxTopicTitleLength + 1), "planning.topic.title_too_long"),
            ("UpdateTopic", _) => (new Dictionary<string, string> { ["topicId"] = seeded.TopicId.ToString(), ["code"] = "one", ["title"] = seeded.TopicTitle, ["expectedSubjectVersion"] = subjectVersion }, "one", "planning.topic.invalid_code"),
            ("MoveTopic", _) => (new Dictionary<string, string> { ["topicId"] = seeded.TopicId.ToString(), ["newParentId"] = seeded.SubtopicId.ToString(), ["expectedSubjectVersion"] = subjectVersion }, seeded.SubtopicId.ToString(), "planning.topic.cycle_detected"),
            _ => (new Dictionary<string, string> { ["topicId"] = seeded.TopicId.ToString(), ["markdown"] = new string('m', TopicNote.MaxMarkdownLength + 1), ["expectedVersion"] = seeded.NoteVersion.ToString(CultureInfo.InvariantCulture) }, new string('m', TopicNote.MaxMarkdownLength + 1), "planning.note.too_long"),
        };

        using var response = await PlanningTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"{subjectUrl}?handler={handler}",
            form,
            csrfHeader,
            csrfToken,
            htmx ? new() { [HtmxIntegration.RequestTypeHeaderName] = HtmxIntegration.PartialRequestType } : null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var document = new HtmlParser().ParseDocument(html);

        Assert.Equal(htmx, !html.Contains("<html", StringComparison.OrdinalIgnoreCase));
        if (!htmx)
        {
            Assert.Equal(seeded.SubjectName, document.QuerySelector("main#main-content h1")?.TextContent.Trim());
        }

        var invalidControl = Assert.Single(document.QuerySelectorAll("[aria-invalid='true']"));
        var submittedForm = invalidControl.Closest("form");
        Assert.NotNull(submittedForm);
        Assert.Contains($"handler={handler}", submittedForm.GetAttribute("action"), StringComparison.Ordinal);
        Assert.Equal(submittedValue, SubmittedValueOf(invalidControl));

        if (handler is "UpdateTopic" or "MoveTopic")
        {
            Assert.Equal(seeded.TopicId.ToString(), submittedForm.QuerySelector("select[name='topicId'] option[selected]")?.GetAttribute("value"));
        }

        if (handler == "SaveNote")
        {
            Assert.Equal(seeded.TopicId.ToString(), submittedForm.QuerySelector("input[name='topicId']")?.GetAttribute("value"));
        }

        var expectedMessage = localizer[messageCode].Value;
        var errors = document.GetElementById(invalidControl.GetAttribute("aria-describedby")!);
        Assert.NotNull(errors);
        Assert.Equal(expectedMessage, Assert.Single(errors.QuerySelectorAll("li")).TextContent.Trim());
        Assert.Single(document.All, element => element.ChildElementCount == 0 && element.TextContent.Trim() == expectedMessage);
        Assert.DoesNotContain("field is required", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("is not valid", html, StringComparison.OrdinalIgnoreCase);
    }

    private static string SubmittedValueOf(AngleSharp.Dom.IElement control) => control.TagName switch
    {
        "TEXTAREA" => control.TextContent,
        "SELECT" => control.QuerySelector("option[selected]")?.GetAttribute("value") ?? string.Empty,
        _ => control.GetAttribute("value") ?? string.Empty,
    };

    private static string FormValue(IHtmlDocument document, string handler, string inputName) =>
        document.QuerySelector($"form[action*='handler={handler}'] input[name='{inputName}']")?.GetAttribute("value")
            ?? throw new InvalidOperationException($"The {handler} form has no '{inputName}' field.");
}
