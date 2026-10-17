using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trail.Application.Abstractions;
using Trail.Application.Accounts;

namespace Trail.Web.Tests.Accounts;

internal static class AccountTestHelper
{
    public static WebApplicationFactory<Program> CreateTestFactory(
        TrailWebApplicationFactory baseFactory,
        TestAccountIdentityGateway gateway,
        TestEmailSender emailSender,
        TestUserSettingsStore settingsStore,
        TestCurrentUser currentUser)
    {
        return baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICurrentUser>();
                services.AddScoped<ICurrentUser>(_ => currentUser);

                services.RemoveAll<IAccountIdentityGateway>();
                services.AddScoped<IAccountIdentityGateway>(_ => gateway);

                services.RemoveAll<IEmailSender>();
                services.AddScoped<IEmailSender>(_ => emailSender);

                services.RemoveAll<IUserSettingsStore>();
                services.AddScoped<IUserSettingsStore>(_ => settingsStore);
            });
        });
    }

    public static async Task<(HttpResponseMessage Response, IHtmlDocument Document, string? CsrfHeader, string? CsrfToken)> GetDocWithAntiforgeryAsync(
        HttpClient client,
        string url)
    {
        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var document = new HtmlParser().ParseDocument(html);

        var meta = document.Head?.QuerySelector("meta[name='csrf-token']");
        var header = meta?.GetAttribute("data-header-name") ?? "X-CSRF-TOKEN";
        var token = meta?.GetAttribute("content");

        return (response, document, header, token);
    }

    public static async Task<HttpResponseMessage> PostFormWithAntiforgeryAsync(
        HttpClient client,
        string url,
        Dictionary<string, string> formData,
        string? csrfHeader = null,
        string? csrfToken = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(formData)
        };

        if (!string.IsNullOrWhiteSpace(csrfHeader) && !string.IsNullOrWhiteSpace(csrfToken))
        {
            request.Headers.Add(csrfHeader, csrfToken);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static async Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string email,
        string password,
        string? inviteToken = null)
    {
        var registerPath = inviteToken is null
            ? "/Account/Register"
            : $"/Account/Register?inviteToken={Uri.EscapeDataString(inviteToken)}";

        var (_, document, csrfHeader, csrfToken) = await GetDocWithAntiforgeryAsync(client, registerPath);
        var form = new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.ConfirmPassword"] = password,
        };

        if (inviteToken is not null)
        {
            form["Input.InviteToken"] = inviteToken;
        }

        AddFormToken(document, form);
        return await PostFormWithAntiforgeryAsync(client, "/Account/Register", form, csrfHeader, csrfToken);
    }

    public static async Task<HttpResponseMessage> SignInAsync(
        HttpClient client,
        string email,
        string password,
        bool rememberMe = true)
    {
        var (_, document, csrfHeader, csrfToken) = await GetDocWithAntiforgeryAsync(client, "/Account/Login");
        var form = new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
        };

        if (rememberMe)
        {
            form["Input.RememberMe"] = "true";
        }

        AddFormToken(document, form);
        return await PostFormWithAntiforgeryAsync(client, "/Account/Login", form, csrfHeader, csrfToken);
    }

    private static void AddFormToken(IHtmlDocument document, Dictionary<string, string> form)
    {
        var formToken = document.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");
        if (!string.IsNullOrEmpty(formToken))
        {
            form["__RequestVerificationToken"] = formToken;
        }
    }
}
