using System.Net;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Localization;
using Trail.Application.Accounts;
using Trail.Domain.Common;

namespace Trail.Web.Tests.Accounts;

public sealed class AdminInvitationModeTests(TrailWebApplicationFactory baseFactory) : IClassFixture<TrailWebApplicationFactory>
{
    private const string AdminPassword = "AdminPass123!";

    private readonly IStringLocalizer<SharedResource> _localizer = baseFactory.Services.GetRequiredService<IStringLocalizer<SharedResource>>();

    [Fact]
    public async Task Get_AdminUsers_WhenRegistrationIsClosed_HidesInvitationsAndExplainsTheMode()
    {
        var gateway = AdminGateway(RegistrationMode.Closed);
        gateway.Invitations.Add(new AccountInvitationDto(Guid.NewGuid(), "invited@example.com", "token", DateTime.UtcNow, DateTime.UtcNow.AddDays(3), false, null));
        using var factory = CreateAdminFactory(gateway);
        using var client = CreateClient(factory);

        var (response, document, _, _) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Admin/Users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(document.QuerySelector("#invite-heading"));
        Assert.Null(document.QuerySelector("#invite-email"));
        Assert.Null(document.QuerySelector("form[action*='handler=Invite']"));
        Assert.Null(document.QuerySelector("#invitations-table-heading"));
        Assert.DoesNotContain("invited@example.com", MainText(document), StringComparison.Ordinal);

        var explanation = document.QuerySelector("section[aria-labelledby='registration-mode-heading']");
        Assert.NotNull(explanation);
        Assert.Equal(_localizer["RegistrationClosedHeading"].Value, explanation.QuerySelector("h2#registration-mode-heading")?.TextContent.Trim());
        Assert.Contains(_localizer["RegistrationClosedDescription"].Value, explanation.TextContent, StringComparison.Ordinal);
        Assert.Contains(_localizer["RegistrationClosedHowToInvite"].Value, explanation.TextContent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RegistrationMode.InviteOnly)]
    [InlineData(RegistrationMode.Open)]
    public async Task Get_AdminUsers_WhenRegistrationAcceptsInvitations_ShowsTheInviteFormAndInvitations(RegistrationMode mode)
    {
        var gateway = AdminGateway(mode);
        gateway.Invitations.Add(new AccountInvitationDto(Guid.NewGuid(), "invited@example.com", "token", DateTime.UtcNow, DateTime.UtcNow.AddDays(3), false, null));
        using var factory = CreateAdminFactory(gateway);
        using var client = CreateClient(factory);

        var (response, document, _, _) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Admin/Users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(document.QuerySelector("#invite-email"));
        Assert.NotNull(document.QuerySelector("#invitations-table-heading"));
        Assert.Contains("invited@example.com", MainText(document), StringComparison.Ordinal);
        Assert.Null(document.QuerySelector("#registration-mode-heading"));
    }

    [Fact]
    public async Task Post_AdminInvite_WhenRegistrationIsClosed_ReturnsForbiddenWithItsOwnMessageAndCreatesNoInvitation()
    {
        var gateway = AdminGateway(RegistrationMode.Closed);
        var emailSender = new TestEmailSender();
        using var factory = CreateAdminFactory(gateway, emailSender);
        using var client = CreateClient(factory);

        using var response = await PostInviteAsync(client, "invitee@example.com");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(_localizer["accounts.invitation.registration_closed"].Value, await MainTextAsync(response), StringComparison.Ordinal);
        Assert.Empty(gateway.Invitations);
        Assert.Empty(emailSender.SentMessages);
    }

    [Fact]
    public async Task Post_AdminInvite_WithTheDefaultClosedRegistration_IsRefusedAndTheAdminPageHidesInvitations()
    {
        var adminEmail = $"admin-{Guid.NewGuid():N}@trail.local";
        await using (var scope = baseFactory.Services.CreateAsyncScope())
        {
            var gateway = scope.ServiceProvider.GetRequiredService<IAccountIdentityGateway>();
            Assert.Equal(RegistrationMode.Closed, gateway.GetRegistrationMode());
            Assert.True((await gateway.CreateUserAsync(adminEmail, AdminPassword, isAdmin: true, emailConfirmed: true, TestContext.Current.CancellationToken)).IsOk);
        }

        using var client = CreateClient(baseFactory);
        using var signIn = await AccountTestHelper.SignInAsync(client, adminEmail, AdminPassword);
        Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);

        var (page, document, _, _) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Admin/Users");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Null(document.QuerySelector("#invite-email"));
        Assert.NotNull(document.QuerySelector("#registration-mode-heading"));

        using var response = await PostInviteAsync(client, $"invitee-{Guid.NewGuid():N}@trail.local");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(_localizer["accounts.invitation.registration_closed"].Value, await MainTextAsync(response), StringComparison.Ordinal);
        await using (var scope = baseFactory.Services.CreateAsyncScope())
        {
            var gateway = scope.ServiceProvider.GetRequiredService<IAccountIdentityGateway>();
            Assert.Empty(await gateway.ListInvitationsAsync(TestContext.Current.CancellationToken));
        }
    }

    private static TestAccountIdentityGateway AdminGateway(RegistrationMode mode) => new() { RegistrationMode = mode };

    private WebApplicationFactory<Program> CreateAdminFactory(TestAccountIdentityGateway gateway, TestEmailSender? emailSender = null)
    {
        var adminId = UserId.New();
        gateway.Users.Add(new AccountUserDto(adminId, "admin@example.com", true, true, true, DateTime.UtcNow));

        return AccountTestHelper.CreateTestFactory(
            baseFactory,
            gateway,
            emailSender ?? new TestEmailSender(),
            new TestUserSettingsStore(),
            new TestCurrentUser { IsAuthenticated = true, IsAdmin = true, Id = adminId });
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static async Task<HttpResponseMessage> PostInviteAsync(HttpClient client, string email)
    {
        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Admin/Users");

        return await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Admin/Users?handler=Invite",
            new Dictionary<string, string> { ["email"] = email },
            csrfHeader,
            csrfToken);
    }

    private static async Task<string> MainTextAsync(HttpResponseMessage response) =>
        MainText(new HtmlParser().ParseDocument(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));

    private static string MainText(IHtmlDocument document) => document.QuerySelector("main")?.TextContent ?? string.Empty;
}
