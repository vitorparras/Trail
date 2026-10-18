using System.Net;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Localization;
using Trail.Application.Accounts;
using Trail.Domain.Common;

namespace Trail.Web.Tests.Accounts;

public sealed class AccountFormErrorTests(TrailWebApplicationFactory baseFactory) : IClassFixture<TrailWebApplicationFactory>
{
    private readonly IStringLocalizer<SharedResource> _localizer = baseFactory.Services.GetRequiredService<IStringLocalizer<SharedResource>>();

    [Fact]
    public async Task Post_Register_WithPasswordBelowMinimumLength_Returns422AndShowsThePasswordRule()
    {
        await using var factory = new TrailWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        using var response = await AccountTestHelper.RegisterAsync(client, "first@trail.local", "abcde");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains(_localizer["accounts.identity.password_too_short"].Value, await FormErrorsAsync(response));
    }

    [Fact]
    public async Task Post_Login_WithBlankEmail_Returns422AndShowsTheFieldError()
    {
        var currentUser = new TestCurrentUser { IsAuthenticated = false };
        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, new TestAccountIdentityGateway(), new TestEmailSender(), new TestUserSettingsStore(), currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        using var response = await AccountTestHelper.SignInAsync(client, " ", "SomePassword123!");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains(_localizer["accounts.login.email_required"].Value, await FormErrorsAsync(response));
    }

    [Fact]
    public async Task Post_AdminToggleActive_OnOwnAccount_Returns422AndShowsTheFieldError()
    {
        var adminId = UserId.New();
        var gateway = new TestAccountIdentityGateway();
        gateway.Users.Add(new AccountUserDto(adminId, "admin@example.com", true, true, true, DateTime.UtcNow));
        var currentUser = new TestCurrentUser { IsAuthenticated = true, IsAdmin = true, Id = adminId };
        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, new TestEmailSender(), new TestUserSettingsStore(), currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Admin/Users");

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Admin/Users?handler=ToggleActive",
            new Dictionary<string, string> { ["userId"] = adminId.Value.ToString(), ["isActive"] = "false" },
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains(_localizer["accounts.admin.cannot_deactivate_self"].Value, await FormErrorsAsync(response));
    }

    private static async Task<List<string>> FormErrorsAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var document = new HtmlParser().ParseDocument(html);

        return [.. document.QuerySelectorAll("[role='alert'] li").Select(item => item.TextContent.Trim()).Where(text => text.Length > 0)];
    }
}
