using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trail.Application.Abstractions;
using Trail.Application.Accounts;
using Trail.Web.Tests.Planning;

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

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestCurrentUserAuthenticationHandler>(TestCurrentUserAuthenticationHandler.SchemeName, null);
                services.Configure<AuthenticationOptions>(options =>
                    options.DefaultAuthenticateScheme = TestCurrentUserAuthenticationHandler.SchemeName);
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
        string? csrfToken = null,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(formData)
        };

        foreach (var (name, value) in headers ?? new Dictionary<string, string>())
        {
            request.Headers.Add(name, value);
        }

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

    public static AngleSharp.Dom.IElement? InvalidField(IHtmlDocument document, string name) =>
        document.QuerySelector($"[name='{name}'][aria-invalid='true']");

    public static List<string> FieldErrors(IHtmlDocument document, string name)
    {
        var describedBy = document.QuerySelector($"[name='{name}']")?.GetAttribute("aria-describedby");
        return [.. (describedBy ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(document.GetElementById)
            .OfType<AngleSharp.Dom.IElement>()
            .Select(element => element.TextContent.Trim())
            .Where(text => text.Length > 0)];
    }

    public static List<string> SummaryErrors(IHtmlDocument document) =>
        [.. document.QuerySelectorAll(".validation-summary-errors li")
            .Select(item => item.TextContent.Trim())
            .Where(text => text.Length > 0)];

    private static void AddFormToken(IHtmlDocument document, Dictionary<string, string> form)
    {
        var formToken = document.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");
        if (!string.IsNullOrEmpty(formToken))
        {
            form["__RequestVerificationToken"] = formToken;
        }
    }
}
