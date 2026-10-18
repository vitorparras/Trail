using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Trail.E2E.Tests;

[Collection(E2ETestCollectionDefinition.Name)]
public sealed partial class FormErrorsE2ETests(DockerAppFixture fixture)
{
    private const string OverlappingSwapsScript = """
        () => {
            const host = document.createElement('div');
            host.innerHTML = '<input id="rejected-field" aria-invalid="true"><input id="accepted-field">';
            document.querySelector('main').append(host);
            const fire = (name, detail) => document.dispatchEvent(new CustomEvent(name, { detail }));
            const rejected = { response: { status: 422 } };
            const accepted = { response: { status: 200 } };
            const rejectedTask = {};
            const acceptedTask = {};
            fire('htmx:before:swap', { ctx: rejected, tasks: [rejectedTask] });
            fire('htmx:before:swap', { ctx: accepted, tasks: [acceptedTask] });
            fire('htmx:after:settle', { task: rejectedTask, newContent: [host.querySelector('#rejected-field')] });
            fire('htmx:after:settle', { task: acceptedTask, newContent: [host.querySelector('#accepted-field')] });
            fire('htmx:after:swap', { ctx: accepted });
            fire('htmx:after:swap', { ctx: rejected });
        }
        """;

    [Fact]
    public async Task PlanningForm_SubmittedWithAServerError_ShowsTheAlertListAndFocusesTheInvalidField()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var subjectName = $"Matemática {suffix}";
        var page = await fixture.CreateAdminPageAsync(1280, 800);
        var main = page.GetByRole(AriaRole.Main);

        await page.GotoAsync(fixture.BaseUrl + "/planos/novo");
        await main.GetByLabel(UiText.Get("PlanName"), new() { Exact = true }).FillAsync($"Plano {suffix}");
        await main.GetByRole(AriaRole.Button, new() { Name = UiText.Get("Save"), Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(PlanDetailsPathPattern());

        var addSubjectForm = main.GetByRole(AriaRole.Form, new() { Name = UiText.Get("AddSubject"), Exact = true });
        await addSubjectForm.GetByLabel(UiText.Get("SubjectName"), new() { Exact = true }).FillAsync(subjectName);
        await addSubjectForm.GetByRole(AriaRole.Button, new() { Name = UiText.Get("AddSubject"), Exact = true }).ClickAsync();
        await main.GetByRole(AriaRole.Link, new() { Name = subjectName, Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(SubjectPathPattern());

        var addTopicForm = main.GetByRole(AriaRole.Form, new() { Name = UiText.Get("AddTopic"), Exact = true });
        var codeField = addTopicForm.GetByLabel(UiText.Get("TopicCode"), new() { Exact = true });
        await codeField.FillAsync("nine");
        await addTopicForm.GetByLabel(UiText.Get("TopicTitle"), new() { Exact = true }).FillAsync("Funções");

        var response = await page.RunAndWaitForResponseAsync(
            () => addTopicForm.GetByRole(AriaRole.Button, new() { Name = UiText.Get("AddTopic"), Exact = true }).ClickAsync(),
            candidate => candidate.Request.Method == "POST" && candidate.Url.Contains("handler=AddTopic", StringComparison.Ordinal));
        Assert.Equal(422, response.Status);

        var alert = main.GetByRole(AriaRole.Alert).Filter(new() { HasText = UiText.Get("planning.topic.invalid_code") });
        await Expect(alert).ToHaveCountAsync(1);
        await Expect(alert.GetByRole(AriaRole.Listitem)).ToHaveTextAsync(UiText.Get("planning.topic.invalid_code"));

        var alertId = await alert.GetAttributeAsync("id");
        Assert.False(string.IsNullOrEmpty(alertId));
        await Expect(codeField).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(codeField).ToHaveAttributeAsync("aria-describedby", alertId);
        await Expect(codeField).ToBeFocusedAsync();

        await AxeTestHelper.AssertAxeZeroViolationsAcrossThemesAndViewportsAsync(page);
    }

    [Fact]
    public async Task HtmxForm_RespondedWith422_FocusesTheInvalidFieldAndLeavesSuccessfulResponsesAlone()
    {
        var page = await fixture.CreateAdminPageAsync(1280, 800);
        var main = page.GetByRole(AriaRole.Main);

        await page.GotoAsync(fixture.BaseUrl + "/Account/Settings");
        var passwordForm = page.Locator("#password-form-container");
        var currentPassword = passwordForm.GetByLabel(UiText.Get("CurrentPassword"), new() { Exact = true });
        await passwordForm.GetByLabel(UiText.Get("NewPassword"), new() { Exact = true }).FillAsync("AnotherPass456!");
        await passwordForm.GetByLabel(UiText.Get("ConfirmNewPassword"), new() { Exact = true }).FillAsync("AnotherPass456!");
        await currentPassword.FillAsync("NotTheCurrentPass1!");

        var rejected = await page.RunAndWaitForResponseAsync(
            () => passwordForm.GetByRole(AriaRole.Button, new() { Name = UiText.Get("ChangePassword"), Exact = true }).ClickAsync(),
            candidate => candidate.Request.Method == "POST" && candidate.Url.Contains("handler=Password", StringComparison.Ordinal));
        Assert.Equal(422, rejected.Status);

        var invalidField = passwordForm.GetByLabel(UiText.Get("CurrentPassword"), new() { Exact = true });
        await Expect(invalidField).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(invalidField).ToBeFocusedAsync();
        await Expect(passwordForm.Locator("#PasswordInput_CurrentPassword-error")).ToHaveTextAsync(UiText.Get("accounts.identity.password_mismatch"));
        await Expect(invalidField).ToHaveAttributeAsync("aria-describedby", "PasswordInput_CurrentPassword-error");

        var accepted = await page.RunAndWaitForResponseAsync(
            () => page.Locator("#settings-form-container button[type='submit']").ClickAsync(),
            candidate => candidate.Request.Method == "POST" && candidate.Url.Contains("handler=Settings", StringComparison.Ordinal));
        Assert.Equal(200, accepted.Status);

        await Expect(main.Locator("[aria-invalid='true']")).ToHaveCountAsync(1);
        await Expect(invalidField).Not.ToBeFocusedAsync();
    }

    [Fact]
    public async Task OverlappingSwaps_WhenAnotherSwapSettlesBetweenThem_KeepTheFocusOfTheRejectedOne()
    {
        var page = await fixture.CreateAdminPageAsync(1280, 800);
        await page.GotoAsync(fixture.BaseUrl + "/Account/Settings");

        await page.EvaluateAsync(OverlappingSwapsScript);

        await Expect(page.Locator("#rejected-field")).ToBeFocusedAsync();
    }

    [GeneratedRegex("/planos/[0-9a-f-]{36}$")]
    private static partial Regex PlanDetailsPathPattern();

    [GeneratedRegex("/planos/[0-9a-f-]{36}/disciplinas/[0-9a-f-]{36}$")]
    private static partial Regex SubjectPathPattern();
}
