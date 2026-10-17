using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Trail.Domain.Planning;
using Trail.Web.Tests.Accounts;

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

    [Fact]
    public async Task UnauthenticatedAccess_ToPlansPages_ReturnsForbidden()
    {
        var store = new TestPlanningStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = false };

        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        using var indexResponse = await client.GetAsync("/planos", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, indexResponse.StatusCode);

        using var createResponse = await client.GetAsync("/planos/novo", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);

        using var detailsResponse = await client.GetAsync($"/planos/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, detailsResponse.StatusCode);
    }
}
