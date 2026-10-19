using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trail.Application.Accounts;
using Trail.Domain.Common;

namespace Trail.Web.Tests.Accounts;

public sealed partial class AccountEmailDeliveryTests(OpenRegistrationWebApplicationFactory factory) : IClassFixture<OpenRegistrationWebApplicationFactory>
{
    private const string ValidPassword = "ValidPassword123!";

    private static readonly TimeSpan SlowSmtp = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Post_Register_WithASlowSmtpServer_AnswersForTheExistingAndTheNewEmailAlikeBeforeAnySendCompletes()
    {
        var smtp = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = new TestEmailSender { Gate = smtp.Task, Delay = SlowSmtp };
        using var app = WithEmailSender(sender);
        var existingEmail = await CreateUserAsync(emailConfirmed: true);
        var newEmail = $"new-{Guid.NewGuid():N}@trail.local";

        var existing = await AnsweredAsync(RegisterAsync(app, existingEmail));
        var created = await AnsweredAsync(RegisterAsync(app, newEmail));

        Assert.Empty(sender.SentMessages);
        Assert.Equal(HttpStatusCode.OK, existing.StatusCode);
        Assert.Equal(existing, created);

        smtp.SetResult();
        var sent = await sender.WaitForMessagesAsync(2);
        Assert.Equal(2, sent.Count);
        var notice = Assert.Single(sent, message => message.Kind == EmailKinds.RegistrationAttempt);
        Assert.Equal(existingEmail, notice.ToAddress, ignoreCase: true);
        Assert.Contains("/Account/ForgotPassword", notice.TextBody, StringComparison.Ordinal);
        var confirmation = Assert.Single(sent, message => message.Kind == EmailKinds.Confirmation);
        Assert.Equal(newEmail, confirmation.ToAddress);
    }

    [Fact]
    public async Task Post_Register_RepeatedForTheSameExistingEmail_AnswersTheSameAndSendsOneNotice()
    {
        var sender = new TestEmailSender();
        using var app = WithEmailSender(sender);
        var existingEmail = await CreateUserAsync(emailConfirmed: true);
        var sentinelEmail = $"sentinel-{Guid.NewGuid():N}@trail.local";

        AccountResponse[] repeated =
        [
            await AnsweredAsync(RegisterAsync(app, existingEmail)),
            await AnsweredAsync(RegisterAsync(app, existingEmail)),
            await AnsweredAsync(RegisterAsync(app, existingEmail)),
        ];
        var sentinel = await AnsweredAsync(RegisterAsync(app, sentinelEmail));

        Assert.All(repeated, response => Assert.Equal(sentinel, response));
        var sent = await sender.WaitForMessagesAsync(2);
        Assert.Equal(
            [(EmailKinds.RegistrationAttempt, existingEmail), (EmailKinds.Confirmation, sentinelEmail)],
            sent.Select(message => (message.Kind, message.ToAddress)));
    }

    [Theory]
    [InlineData("/Account/ForgotPassword", true, EmailKinds.PasswordReset)]
    [InlineData("/Account/ResendConfirmation", false, EmailKinds.Confirmation)]
    public async Task Post_EmailForm_WithASlowSmtpServer_AnswersForTheExistingAndTheUnknownEmailAlikeBeforeTheSendCompletes(
        string path,
        bool emailConfirmed,
        string expectedKind)
    {
        var smtp = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = new TestEmailSender { Gate = smtp.Task, Delay = SlowSmtp };
        using var app = WithEmailSender(sender);
        var existingEmail = await CreateUserAsync(emailConfirmed);

        var existing = await AnsweredAsync(PostEmailFormAsync(app, path, existingEmail));
        var unknown = await AnsweredAsync(PostEmailFormAsync(app, path, $"unknown-{Guid.NewGuid():N}@trail.local"));

        Assert.Empty(sender.SentMessages);
        Assert.Equal(HttpStatusCode.OK, existing.StatusCode);
        Assert.Equal(unknown, existing);

        smtp.SetResult();
        var sent = Assert.Single(await sender.WaitForMessagesAsync(1));
        Assert.Equal(expectedKind, sent.Kind);
        Assert.Equal(existingEmail, sent.ToAddress);
    }

    [Theory]
    [InlineData("Invite", "email")]
    [InlineData("TestEmail", "recipientEmail")]
    public async Task Post_AdminEmail_WaitsForTheSmtpServerBeforeAnswering(string handler, string field)
    {
        var smtp = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = new TestEmailSender { Gate = smtp.Task };
        var adminId = UserId.New();
        var gateway = new TestAccountIdentityGateway();
        gateway.Users.Add(new AccountUserDto(adminId, "admin@example.com", true, true, true, DateTime.UtcNow));
        using var app = AccountTestHelper.CreateTestFactory(
            factory,
            gateway,
            sender,
            new TestUserSettingsStore(),
            new TestCurrentUser { IsAuthenticated = true, IsAdmin = true, Id = adminId });
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Admin/Users");

        var request = AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"/Admin/Users?handler={handler}",
            new Dictionary<string, string> { [field] = "recipient@example.com" },
            csrfHeader,
            csrfToken);
        await sender.FirstSendStarted.WaitAsync(AnswerTimeout, TestContext.Current.CancellationToken);

        Assert.False(request.IsCompleted);

        smtp.SetResult();
        using var response = await request.WaitAsync(AnswerTimeout, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("recipient@example.com", Assert.Single(sender.SentMessages).ToAddress);
    }

    private WebApplicationFactory<Program> WithEmailSender(TestEmailSender sender) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddScoped<IEmailSender>(_ => sender);
        }));

    private async Task<string> CreateUserAsync(bool emailConfirmed, bool isActive = true)
    {
        using var scope = factory.Services.CreateScope();
        var gateway = scope.ServiceProvider.GetRequiredService<IAccountIdentityGateway>();
        var email = $"existing-{Guid.NewGuid():N}@trail.local";

        var created = await gateway.CreateUserAsync(email, ValidPassword, isAdmin: false, emailConfirmed, TestContext.Current.CancellationToken);
        Assert.True(created.IsOk);
        if (!isActive)
        {
            Assert.True((await gateway.SetUserActiveStatusAsync(created.Data, isActive: false, TestContext.Current.CancellationToken)).IsOk);
        }

        return email;
    }

    private static async Task<AccountResponse> AnsweredAsync(Task<AccountResponse> response) =>
        await response.WaitAsync(AnswerTimeout, TestContext.Current.CancellationToken);

    private static async Task<AccountResponse> RegisterAsync(WebApplicationFactory<Program> app, string email)
    {
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        return await ReadAsync(await AccountTestHelper.RegisterAsync(client, email, ValidPassword));
    }

    private static async Task<AccountResponse> PostEmailFormAsync(WebApplicationFactory<Program> app, string path, string email)
    {
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var (_, page, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, path);
        var form = new Dictionary<string, string> { ["Email"] = email };
        var formToken = page.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");
        if (!string.IsNullOrEmpty(formToken))
        {
            form["__RequestVerificationToken"] = formToken;
        }

        return await ReadAsync(await AccountTestHelper.PostFormWithAntiforgeryAsync(client, path, form, csrfHeader, csrfToken));
    }

    private static async Task<AccountResponse> ReadAsync(HttpResponseMessage response)
    {
        using (response)
        {
            var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            var main = new HtmlParser().ParseDocument(html).QuerySelector("main")?.TextContent ?? string.Empty;

            return new AccountResponse(response.StatusCode, Whitespace().Replace(main, " ").Trim());
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    private sealed record AccountResponse(HttpStatusCode StatusCode, string MainText);
}
