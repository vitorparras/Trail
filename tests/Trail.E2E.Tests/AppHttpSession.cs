using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Playwright;

namespace Trail.E2E.Tests;

internal sealed class AppHttpSession : IAsyncDisposable
{
    private const string LoginPath = "/Account/Login";
    private const string RegisterPath = "/Account/Register";
    private const string AdminUsersPath = "/Admin/Users";
    private const int ResponseExcerptLength = 500;

    private static readonly HtmlParser Parser = new();

    private readonly IAPIRequestContext _request;

    private AppHttpSession(IAPIRequestContext request)
    {
        _request = request;
    }

    public static async Task<AppHttpSession> OpenAsync(IPlaywright playwright, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(playwright);

        var request = await playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions { BaseURL = baseUrl });
        return new AppHttpSession(request);
    }

    public async Task RegisterFirstAccountAsync(string email, string password)
    {
        var response = await RegisterAsync(RegisterPath, email, password);
        await EnsureStatusAsync(response, 302, "Registration of the first account (the end-to-end suite needs a fresh instance)");
    }

    public async Task RegisterInvitedAccountAsync(string invitationPath, string email, string password)
    {
        var response = await RegisterAsync(invitationPath, email, password);
        await EnsureStatusAsync(response, 200, $"Registration of the invited account {email}");
    }

    public async Task ConfirmEmailAsync(string confirmationPath) =>
        await GetDocumentAsync(confirmationPath, "Email confirmation");

    public async Task SignInAsync(string email, string password)
    {
        var response = await PostFormAsync(LoginPath, MainForm, new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.RememberMe"] = "true",
        });

        await EnsureStatusAsync(response, 302, $"Sign-in of {email}");
    }

    public async Task<string> CreateInvitationAsync(string email)
    {
        var document = await SubmitAdminFormAsync(
            document => document.QuerySelector("form[action*='handler=Invite']"),
            new Dictionary<string, string> { ["email"] = email },
            $"Invitation of {email}");

        return GeneratedLinkPath(document, "#generated-invite-link");
    }

    public async Task<string> CreateConfirmationLinkAsync(string email)
    {
        var document = await SubmitAdminFormAsync(
            document => document
                .QuerySelectorAll("tr")
                .Where(row => string.Equals(row.QuerySelector("td")?.TextContent.Trim(), email, StringComparison.OrdinalIgnoreCase))
                .SelectMany(row => row.QuerySelectorAll("form"))
                .SingleOrDefault(form => form.QuerySelector("input[name='kind'][value='EmailConfirmation']") is not null),
            new Dictionary<string, string>(),
            $"Confirmation link of {email}");

        return GeneratedLinkPath(document, "#generated-manual-link");
    }

    public Task<string> StorageStateAsync() => _request.StorageStateAsync();

    public ValueTask DisposeAsync() => _request.DisposeAsync();

    private static IElement? MainForm(IDocument document) => document.QuerySelector("main form[method='post']");

    private static string GeneratedLinkPath(IDocument document, string selector)
    {
        var link = document.QuerySelector(selector)?.GetAttribute("value")
            ?? throw new InvalidOperationException($"The administration page did not show the link '{selector}'.");

        return Uri.TryCreate(link, UriKind.Absolute, out var absolute) ? absolute.PathAndQuery : link;
    }

    private static async Task EnsureStatusAsync(IAPIResponse response, int expectedStatus, string step)
    {
        if (response.Status == expectedStatus)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{step} answered HTTP {response.Status} instead of {expectedStatus}: {await DescribeAsync(response)}");
    }

    private static async Task<string> DescribeAsync(IAPIResponse response)
    {
        var body = await response.TextAsync();

        if (response.Headers.TryGetValue("content-type", out var contentType)
            && contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            var document = await Parser.ParseDocumentAsync(body);
            var text = (document.QuerySelector("main") ?? document.Body)?.TextContent;
            if (!string.IsNullOrWhiteSpace(text))
            {
                body = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            }
        }

        return body.Length > ResponseExcerptLength ? body[..ResponseExcerptLength] : body;
    }

    private Task<IAPIResponse> RegisterAsync(string registrationPath, string email, string password) =>
        PostFormAsync(registrationPath, MainForm, new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.ConfirmPassword"] = password,
        });

    private async Task<IDocument> SubmitAdminFormAsync(
        Func<IDocument, IElement?> findForm,
        IReadOnlyDictionary<string, string> fields,
        string step)
    {
        var response = await PostFormAsync(AdminUsersPath, findForm, fields);
        await EnsureStatusAsync(response, 302, step);

        var location = response.Headers.TryGetValue("location", out var value)
            ? value
            : throw new InvalidOperationException($"{step} redirected without a location.");

        return await GetDocumentAsync(location, step);
    }

    private async Task<IAPIResponse> PostFormAsync(
        string pagePath,
        Func<IDocument, IElement?> findForm,
        IReadOnlyDictionary<string, string> fields)
    {
        var document = await GetDocumentAsync(pagePath, $"Form page {pagePath}");
        var form = findForm(document)
            ?? throw new InvalidOperationException($"The expected form was not found on {pagePath}.");

        var data = _request.CreateFormData();
        foreach (var hidden in form.QuerySelectorAll("input[type='hidden'][name]"))
        {
            data.Set(hidden.GetAttribute("name")!, hidden.GetAttribute("value") ?? string.Empty);
        }

        foreach (var (name, value) in fields)
        {
            data.Set(name, value);
        }

        var action = form.GetAttribute("action");
        var target = string.IsNullOrEmpty(action) ? pagePath : action;

        return await _request.PostAsync(target, new APIRequestContextOptions { Form = data, MaxRedirects = 0 });
    }

    private async Task<IDocument> GetDocumentAsync(string path, string step)
    {
        var response = await _request.GetAsync(path);
        await EnsureStatusAsync(response, 200, step);

        return await Parser.ParseDocumentAsync(await response.TextAsync());
    }
}
