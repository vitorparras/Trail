using System.Net;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Localization;
using Trail.Application.Accounts;

namespace Trail.Web.Tests.Accounts;

public sealed class ResetPasswordFlowTests(TrailWebApplicationFactory factory) : IClassFixture<TrailWebApplicationFactory>
{
    private const string InitialPassword = "InitialPassword123!";
    private const string ValidNewPassword = "ResetPassword456!";

    private readonly IStringLocalizer<SharedResource> _localizer = factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>();

    [Fact]
    public async Task Post_ResetPassword_WithPasswordBelowMinimumLength_Returns422WithThePasswordRule()
    {
        var (userId, token) = await CreateUserWithResetTokenAsync();

        var (statusCode, document) = await PostResetAsync(userId, token, "abc");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, statusCode);
        Assert.Equal([_localizer["accounts.identity.password_too_short"].Value], FormErrors(document));
    }

    [Fact]
    public async Task Post_ResetPassword_AfterAPasswordRuleFailure_KeepsTheLinkUsable()
    {
        var (userId, token) = await CreateUserWithResetTokenAsync();
        await PostResetAsync(userId, token, "abc");

        var (statusCode, document) = await PostResetAsync(userId, token, ValidNewPassword);

        Assert.Equal(HttpStatusCode.OK, statusCode);
        Assert.Empty(FormErrors(document));
    }

    [Fact]
    public async Task Post_ResetPassword_WithInvalidToken_Returns422WithTheInvalidLinkMessage()
    {
        var (userId, _) = await CreateUserWithResetTokenAsync();

        var (statusCode, document) = await PostResetAsync(userId, "invalid-token", ValidNewPassword);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, statusCode);
        Assert.Equal([_localizer["accounts.reset_password.invalid_token"].Value], FormErrors(document));
    }

    [Fact]
    public async Task Post_ResetPassword_WithInvalidTokenAndShortPassword_ReportsOnlyTheInvalidLink()
    {
        var (userId, _) = await CreateUserWithResetTokenAsync();

        var (statusCode, document) = await PostResetAsync(userId, "invalid-token", "abc");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, statusCode);
        Assert.Equal([_localizer["accounts.reset_password.invalid_token"].Value], FormErrors(document));
    }

    [Fact]
    public async Task Post_ResetPassword_ForUnknownUser_ShowsTheInvalidLinkMessage()
    {
        var (statusCode, document) = await PostResetAsync(Guid.NewGuid(), "any-token", ValidNewPassword);

        Assert.Equal(HttpStatusCode.NotFound, statusCode);
        Assert.Contains(_localizer["accounts.reset_password.user_not_found"].Value, document.QuerySelector("main")?.TextContent, StringComparison.Ordinal);
        Assert.Equal(_localizer["accounts.reset_password.invalid_token"].Value, _localizer["accounts.reset_password.user_not_found"].Value);
    }

    private async Task<(Guid UserId, string Token)> CreateUserWithResetTokenAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = factory.Services.CreateScope();
        var gateway = scope.ServiceProvider.GetRequiredService<IAccountIdentityGateway>();

        var created = await gateway.CreateUserAsync($"reset-{Guid.NewGuid():N}@trail.local", InitialPassword, isAdmin: false, emailConfirmed: true, cancellationToken);
        Assert.True(created.IsOk);
        var token = await gateway.GeneratePasswordResetTokenAsync(created.Data, cancellationToken);
        Assert.True(token.IsOk);

        return (created.Data.Value, token.Data);
    }

    private async Task<(HttpStatusCode StatusCode, IHtmlDocument Document)> PostResetAsync(Guid userId, string token, string newPassword)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var resetPath = $"/Account/ResetPassword?userId={userId}&token={Uri.EscapeDataString(token)}";
        var (_, page, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, resetPath);

        var form = new Dictionary<string, string>
        {
            ["Input.UserId"] = userId.ToString(),
            ["Input.Token"] = token,
            ["Input.NewPassword"] = newPassword,
            ["Input.ConfirmNewPassword"] = newPassword,
        };
        var formToken = page.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");
        if (!string.IsNullOrEmpty(formToken))
        {
            form["__RequestVerificationToken"] = formToken;
        }

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(client, "/Account/ResetPassword", form, csrfHeader, csrfToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        return (response.StatusCode, new HtmlParser().ParseDocument(html));
    }

    private static List<string> FormErrors(IHtmlDocument document) =>
        [.. document.QuerySelectorAll("form [role='alert'] li").Select(item => item.TextContent.Trim()).Where(text => text.Length > 0)];
}
