using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Trail.E2E.Tests;

[Collection(E2ETestCollectionDefinition.Name)]
public sealed partial class PlansE2ETests(DockerAppFixture fixture)
{
    private const string SectionName = "Conhecimentos Específicos";
    private const string SubjectName = "Direito Tributário";
    private const string TopicTitle = "Competência Tributária";
    private const string SubtopicTitle = "Limitações ao Poder de Tributar";
    private const string NoteHeading = "Princípios constitucionais";
    private const string NoteParagraph = "A anterioridade protege o contribuinte.";

    [Theory]
    [InlineData(390, 844)]
    [InlineData(1280, 800)]
    public async Task StudyPlan_BuildTreeChangeStatusWriteNoteAndSearch_WorksWithoutAccessibilityViolations(int width, int height)
    {
        var planName = $"Auditor Fiscal {width}x{height}";
        var page = await fixture.CreateAdminPageAsync(width, height);
        var main = page.GetByRole(AriaRole.Main);

        await page.GotoAsync(fixture.BaseUrl + "/planos");
        await Expect(main.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(UiText.Get("StudyPlans"));
        await AxeTestHelper.AssertAxeZeroViolationsAcrossThemesAndViewportsAsync(page);

        await page.GotoAsync(fixture.BaseUrl + "/planos/novo");
        await AxeTestHelper.AssertAxeZeroViolationsAcrossThemesAndViewportsAsync(page);

        await main.GetByLabel(UiText.Get("PlanName"), new() { Exact = true }).FillAsync(planName);
        await main.GetByLabel(UiText.Get("PlanDescription"), new() { Exact = true }).FillAsync("Plano montado a partir da planilha.");
        await main.GetByRole(AriaRole.Button, new() { Name = UiText.Get("Save"), Exact = true }).ClickAsync();

        await Expect(page).ToHaveURLAsync(PlanDetailsPathPattern());
        await Expect(main.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(planName);
        var planUrl = page.Url;

        var addSectionForm = main.GetByRole(AriaRole.Form, new() { Name = UiText.Get("AddSection"), Exact = true });
        await addSectionForm.GetByLabel(UiText.Get("SectionName"), new() { Exact = true }).FillAsync(SectionName);
        await addSectionForm.GetByRole(AriaRole.Button, new() { Name = UiText.Get("AddSection"), Exact = true }).ClickAsync();

        var addSubjectForm = main.GetByRole(AriaRole.Form, new() { Name = UiText.Get("AddSubject"), Exact = true });
        var subjectSectionSelect = addSubjectForm.GetByLabel(UiText.Get("SubjectSection"), new() { Exact = true });
        await Expect(subjectSectionSelect.Locator("option", new() { HasText = SectionName })).ToHaveCountAsync(1);

        await addSubjectForm.GetByLabel(UiText.Get("SubjectName"), new() { Exact = true }).FillAsync(SubjectName);
        await subjectSectionSelect.SelectOptionAsync(new SelectOptionValue { Label = SectionName });
        await addSubjectForm.GetByRole(AriaRole.Button, new() { Name = UiText.Get("AddSubject"), Exact = true }).ClickAsync();

        var subjectLink = main
            .GetByRole(AriaRole.Table, new() { Name = SectionName, Exact = true })
            .GetByRole(AriaRole.Link, new() { Name = SubjectName, Exact = true });
        await Expect(subjectLink).ToBeVisibleAsync();
        await AxeTestHelper.AssertAxeZeroViolationsAcrossThemesAndViewportsAsync(page);

        await subjectLink.ClickAsync();
        await Expect(page).ToHaveURLAsync(SubjectPathPattern());
        await Expect(main.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(SubjectName);

        var addTopicForm = main.GetByRole(AriaRole.Form, new() { Name = UiText.Get("AddTopic"), Exact = true });
        await addTopicForm.GetByLabel(UiText.Get("TopicCode"), new() { Exact = true }).FillAsync("1");
        await addTopicForm.GetByLabel(UiText.Get("TopicTitle"), new() { Exact = true }).FillAsync(TopicTitle);
        await addTopicForm.GetByRole(AriaRole.Button, new() { Name = UiText.Get("AddTopic"), Exact = true }).ClickAsync();

        var topicList = main.GetByRole(AriaRole.List, new() { Name = UiText.Get("Topics"), Exact = true });
        var topicItem = topicList.GetByRole(AriaRole.Listitem).Filter(new() { HasText = TopicTitle });
        await Expect(topicItem).ToHaveCountAsync(1);

        await addTopicForm.GetByLabel(UiText.Get("TopicCode"), new() { Exact = true }).FillAsync("1.1");
        await addTopicForm.GetByLabel(UiText.Get("TopicTitle"), new() { Exact = true }).FillAsync(SubtopicTitle);
        await addTopicForm.GetByLabel(UiText.Get("ParentTopic"), new() { Exact = true }).SelectOptionAsync(new SelectOptionValue { Label = $"1 - {TopicTitle}" });
        await addTopicForm.GetByRole(AriaRole.Button, new() { Name = UiText.Get("AddTopic"), Exact = true }).ClickAsync();

        var subtopicItem = topicList.GetByRole(AriaRole.Listitem).Filter(new() { HasText = SubtopicTitle });
        await Expect(subtopicItem).ToHaveCountAsync(1);
        await Expect(subtopicItem).ToHaveClassAsync(SubtopicIndentPattern());
        await Expect(subtopicItem).ToContainTextAsync("1.1");
        await Expect(topicItem).ToHaveClassAsync(RootIndentPattern());

        var statusBadge = topicItem.Locator("[class*='badge-status-']");
        await Expect(statusBadge).ToHaveTextAsync(UiText.Get("TopicStatus_ToStudy"));

        var statusSelect = topicItem.GetByLabel(UiText.Format("TopicStatusFor", TopicTitle), new() { Exact = true });
        await statusSelect.SelectOptionAsync("Studying");
        var statusResponse = await page.RunAndWaitForResponseAsync(
            () => topicItem.GetByRole(AriaRole.Button, new() { Name = UiText.Get("Apply"), Exact = true }).ClickAsync(),
            response => response.Request.Method == "POST" && response.Url.Contains("handler=ChangeStatus", StringComparison.Ordinal));
        Assert.Equal(200, statusResponse.Status);

        await Expect(statusBadge).ToHaveTextAsync(UiText.Get("TopicStatus_Studying"));
        await Expect(statusBadge).ToHaveClassAsync(StudyingBadgePattern());
        await Expect(statusSelect.Locator("option[selected]")).ToHaveAttributeAsync("value", "Studying");
        await Expect(statusSelect).ToHaveValueAsync("Studying");
        await Expect(page.Locator("#topic-tree-container")).ToHaveCountAsync(1);
        await Expect(subtopicItem.Locator("[class*='badge-status-']")).ToHaveTextAsync(UiText.Get("TopicStatus_ToStudy"));

        await page.ReloadAsync();
        await Expect(statusBadge).ToHaveTextAsync(UiText.Get("TopicStatus_Studying"));
        await Expect(statusSelect).ToHaveValueAsync("Studying");

        await subtopicItem.GetByRole(AriaRole.Link, new() { Name = UiText.Get("TopicNotes"), Exact = true }).ClickAsync();
        var noteForm = main.GetByRole(AriaRole.Form, new() { Name = UiText.Get("TopicNotes") });
        await Expect(main.Locator("#topic-notes-heading")).ToContainTextAsync(SubtopicTitle);
        await Expect(main.Locator("#topic-notes-container .prose h3")).ToHaveCountAsync(0);

        await noteForm.GetByLabel(UiText.Get("MarkdownSource"), new() { Exact = true }).FillAsync($"### {NoteHeading}\n\n{NoteParagraph}");
        await noteForm.GetByRole(AriaRole.Button, new() { Name = UiText.Get("SaveNotes"), Exact = true }).ClickAsync();

        await Expect(main.Locator("#topic-notes-container .prose h3")).ToHaveTextAsync(NoteHeading);
        await Expect(main.Locator("#topic-notes-container .prose p")).ToHaveTextAsync(NoteParagraph);
        await Expect(main.Locator("#topic-notes-heading")).ToContainTextAsync(SubtopicTitle);
        await AxeTestHelper.AssertAxeZeroViolationsAcrossThemesAndViewportsAsync(page);

        await page.GotoAsync(planUrl);
        var searchForm = main.GetByRole(AriaRole.Form, new() { Name = UiText.Get("Search"), Exact = true });
        var results = main.Locator("#search-results-container");

        await searchForm.GetByLabel(UiText.Get("SearchTopicsLabel"), new() { Exact = true }).FillAsync("competencia");
        await SubmitSearchAsync(page, searchForm);
        await Expect(results.GetByRole(AriaRole.Link, new() { Name = TopicTitle, Exact = true })).ToBeVisibleAsync();
        await Expect(results.GetByText(SubtopicTitle)).ToHaveCountAsync(0);

        await searchForm.GetByLabel(UiText.Get("SearchTopicsLabel"), new() { Exact = true }).FillAsync(string.Empty);
        await searchForm.GetByLabel(UiText.Get("FilterByStatus"), new() { Exact = true }).SelectOptionAsync("ToStudy");
        await SubmitSearchAsync(page, searchForm);
        await Expect(results.GetByRole(AriaRole.Link, new() { Name = SubtopicTitle, Exact = true })).ToBeVisibleAsync();
        await Expect(results.GetByText(TopicTitle)).ToHaveCountAsync(0);

        await AxeTestHelper.AssertAxeZeroViolationsAcrossThemesAndViewportsAsync(page);

        await page.GotoAsync(planUrl);
        var deleteSubjectLink = main.GetByRole(AriaRole.Link, new() { Name = UiText.Format("DeleteNamed", SubjectName), Exact = true });
        var subjectDialog = page.GetByRole(AriaRole.Dialog, new() { Name = UiText.Get("DeleteSubject"), Exact = true });

        await deleteSubjectLink.FocusAsync();
        await OpenDeletionDialogAsync(page, () => deleteSubjectLink.PressAsync("Enter"), "handler=DeleteSubjectImpact");
        string[] subjectItems =
        [
            UiText.Format("planning.deletion.subjects.one", 1),
            UiText.Format("planning.deletion.topics.other", 2),
            UiText.Format("planning.deletion.notes.one", 1),
        ];
        await Expect(subjectDialog).ToBeVisibleAsync();
        await Expect(subjectDialog.GetByRole(AriaRole.Listitem)).ToHaveTextAsync(subjectItems);
        await Expect(subjectDialog).ToHaveAccessibleDescriptionAsync(DeletionDescription(SubjectName, subjectItems));
        await Expect(subjectDialog.GetByRole(AriaRole.Link, new() { Name = UiText.Get("Cancel"), Exact = true })).ToBeFocusedAsync();
        await AxeTestHelper.AssertAxeZeroViolationsInBothThemesAsync(page);

        await page.Keyboard.PressAsync("Escape");
        await Expect(subjectDialog).ToBeHiddenAsync();
        await Expect(deleteSubjectLink).ToBeFocusedAsync();
        await Expect(subjectLink).ToBeVisibleAsync();

        await subjectLink.ClickAsync();
        await Expect(page).ToHaveURLAsync(SubjectPathPattern());
        var topicDialog = page.GetByRole(AriaRole.Dialog, new() { Name = UiText.Get("DeleteTopic"), Exact = true });

        await OpenDeletionDialogAsync(
            page,
            () => subtopicItem.GetByRole(AriaRole.Link, new() { Name = UiText.Format("DeleteNamed", SubtopicTitle), Exact = true }).ClickAsync(),
            "handler=DeleteTopicImpact");
        string[] topicItems =
        [
            UiText.Format("planning.deletion.topics.one", 1),
            UiText.Format("planning.deletion.notes.one", 1),
        ];
        await Expect(topicDialog).ToBeVisibleAsync();
        await Expect(topicDialog.GetByRole(AriaRole.Listitem)).ToHaveTextAsync(topicItems);
        await Expect(topicDialog).ToHaveAccessibleDescriptionAsync(DeletionDescription(SubtopicTitle, topicItems));
        await Expect(topicDialog.GetByRole(AriaRole.Link, new() { Name = UiText.Get("Cancel"), Exact = true })).ToBeFocusedAsync();
        await AxeTestHelper.AssertAxeZeroViolationsInBothThemesAsync(page);

        await topicDialog.GetByRole(AriaRole.Button, new() { Name = UiText.Get("ConfirmDelete"), Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(SubjectPathPattern());
        await Expect(subtopicItem).ToHaveCountAsync(0);
        await Expect(topicItem).ToHaveCountAsync(1);

        await page.GotoAsync(planUrl);
        await OpenDeletionDialogAsync(page, () => deleteSubjectLink.ClickAsync(), "handler=DeleteSubjectImpact");
        await Expect(subjectDialog.GetByRole(AriaRole.Listitem)).ToHaveTextAsync(
        [
            UiText.Format("planning.deletion.subjects.one", 1),
            UiText.Format("planning.deletion.topics.one", 1),
            UiText.Format("planning.deletion.notes.other", 0),
        ]);

        await subjectDialog.GetByRole(AriaRole.Button, new() { Name = UiText.Get("ConfirmDelete"), Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(PlanDetailsPathPattern());
        await Expect(subjectLink).ToHaveCountAsync(0);

        var planDialog = page.GetByRole(AriaRole.Dialog, new() { Name = UiText.Get("DeleteStudyPlan"), Exact = true });
        string[] planItems =
        [
            UiText.Format("planning.deletion.sections.one", 1),
            UiText.Format("planning.deletion.subjects.other", 0),
            UiText.Format("planning.deletion.topics.other", 0),
            UiText.Format("planning.deletion.notes.other", 0),
        ];
        await main.Locator("[data-dialog-open='delete-plan-dialog']").ClickAsync();
        await Expect(planDialog.GetByRole(AriaRole.Listitem)).ToHaveTextAsync(planItems);
        await Expect(planDialog).ToHaveAccessibleDescriptionAsync(DeletionDescription(planName, planItems));
        await Expect(planDialog.GetByRole(AriaRole.Button, new() { Name = UiText.Get("Cancel"), Exact = true })).ToBeFocusedAsync();
        await AxeTestHelper.AssertAxeZeroViolationsInBothThemesAsync(page);

        await planDialog.GetByRole(AriaRole.Button, new() { Name = UiText.Get("ConfirmDelete"), Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(PlanListPathPattern());
        await Expect(main.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(UiText.Get("StudyPlans"));
        await Expect(main).Not.ToContainTextAsync(planName);
    }

    private static Regex DeletionDescription(string target, IEnumerable<string> items) =>
        new("^" + string.Join(@"\s*", new[] { target, UiText.Get("DeletionImpactWarning") }.Concat(items).Select(Regex.Escape)) + "$");

    private static async Task OpenDeletionDialogAsync(IPage page, Func<Task> open, string handler)
    {
        var response = await page.RunAndWaitForResponseAsync(
            open,
            candidate => candidate.Request.Method == "GET" && candidate.Url.Contains(handler, StringComparison.Ordinal));

        Assert.Equal(200, response.Status);
        Assert.Equal("partial", await response.Request.HeaderValueAsync("hx-request-type"));
    }

    private static async Task SubmitSearchAsync(IPage page, ILocator searchForm)
    {
        var response = await page.RunAndWaitForResponseAsync(
            () => searchForm.GetByRole(AriaRole.Button, new() { Name = UiText.Get("Filter"), Exact = true }).ClickAsync(),
            candidate => candidate.Request.Method == "GET" && candidate.Url.Contains("handler=SearchTopics", StringComparison.Ordinal));

        Assert.Equal(200, response.Status);
    }

    [GeneratedRegex("/planos/[0-9a-f-]{36}$")]
    private static partial Regex PlanDetailsPathPattern();

    [GeneratedRegex("/planos$")]
    private static partial Regex PlanListPathPattern();

    [GeneratedRegex("/planos/[0-9a-f-]{36}/disciplinas/[0-9a-f-]{36}$")]
    private static partial Regex SubjectPathPattern();

    [GeneratedRegex("(^|\\s)pl-6(\\s|$)")]
    private static partial Regex SubtopicIndentPattern();

    [GeneratedRegex("(^|\\s)pl-0(\\s|$)")]
    private static partial Regex RootIndentPattern();

    [GeneratedRegex("(^|\\s)badge-status-studying(\\s|$)")]
    private static partial Regex StudyingBadgePattern();
}
