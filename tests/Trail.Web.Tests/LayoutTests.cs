using System.Globalization;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Localization;
using Trail.Web.Pages;

namespace Trail.Web.Tests;

public sealed class LayoutTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Get_Home_RendersLayoutLandmarksAndControls()
    {
        using var client = HomePage.CreateClient(factory);
        using var document = await HomePage.LoadAsync(client);

        Assert.NotNull(document.QuerySelector("body > a[href='#main-content']"));
        Assert.NotNull(document.QuerySelector("body > header"));
        Assert.Empty(document.QuerySelectorAll("aside"));
        Assert.NotNull(document.QuerySelector("main#main-content[tabindex='-1']"));
        Assert.Single(document.QuerySelectorAll("main"));
        Assert.Equal(2, document.QuerySelectorAll("nav[aria-label]").Length);
        Assert.NotNull(document.QuerySelector("dialog#navigation-dialog[aria-labelledby='navigation-dialog-title']"));
        Assert.NotNull(document.QuerySelector("button[data-dialog-open='navigation-dialog'][aria-controls='navigation-dialog']"));
        Assert.NotNull(document.QuerySelector("button[type='button'][data-theme-toggle][aria-pressed='false']"));
        Assert.NotNull(document.QuerySelector("#toasts[aria-live='polite']"));
        Assert.NotNull(document.QuerySelector("nav a[aria-current='page'][href='/']"));
    }

    [Fact]
    public async Task Get_Home_RendersPortugueseTextFromResources()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");
        var shared = factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>();
        var page = factory.Services.GetRequiredService<IStringLocalizer<IndexModel>>();
        AssertResolved(shared["SkipToContent"], shared["MainNavigation"], shared["OpenMenu"], shared["DarkTheme"]);
        AssertResolved(page["Title"], page["Heading"], page["Description"]);

        using var client = HomePage.CreateClient(factory);
        using var document = await HomePage.LoadAsync(client);

        Assert.Equal("pt-BR", document.DocumentElement.GetAttribute("lang"));
        Assert.Equal(shared["PageTitle", page["Title"], shared["ProductName"]].Value, document.Title);
        Assert.Equal(shared["SkipToContent"].Value, document.QuerySelector("a[href='#main-content']")!.TextContent.Trim());
        Assert.Equal(shared["MainNavigation"].Value, document.QuerySelector("nav")!.GetAttribute("aria-label"));
        Assert.Equal(shared["OpenMenu"].Value, document.QuerySelector("[data-dialog-open] .sr-only")!.TextContent.Trim());
        Assert.Equal(shared["DarkTheme"].Value, document.QuerySelector("[data-theme-toggle] .sr-only")!.TextContent.Trim());
        Assert.Equal(page["Heading"].Value, document.QuerySelector("h1")!.TextContent.Trim());
        Assert.Equal(page["Description"].Value, document.QuerySelector("main p")!.TextContent.Trim());
    }

    [Fact]
    public async Task Get_Home_WithAnotherAcceptLanguage_KeepsPortugueseCulture()
    {
        using var client = HomePage.CreateClient(factory);
        using var document = await HomePage.LoadAsync(client, request => request.Headers.AcceptLanguage.ParseAdd("en-US"));

        Assert.Equal("pt-BR", document.DocumentElement.GetAttribute("lang"));
    }

    [Fact]
    public async Task Get_Home_HasNothingThatAStrictContentSecurityPolicyWouldBlock()
    {
        using var client = HomePage.CreateClient(factory);
        using var document = await HomePage.LoadAsync(client);
        var elements = AllElements(document).ToList();
        var attributes = elements.SelectMany(element => element.Attributes).ToList();

        Assert.All(document.Scripts, script => Assert.False(string.IsNullOrEmpty(script.Source)));
        Assert.All(document.Scripts, script => Assert.True(string.IsNullOrWhiteSpace(script.Text)));
        Assert.DoesNotContain(elements, element => element.LocalName == "style");
        Assert.DoesNotContain(attributes, attribute => attribute.Name == "style");
        Assert.DoesNotContain(attributes, attribute => IsEventHandler(attribute.Name));
        Assert.DoesNotContain(attributes, attribute => IsScriptValue(attribute.Value));
    }

    private static IEnumerable<IElement> AllElements(IHtmlDocument document) =>
        document.All.Concat(document.QuerySelectorAll("template")
            .OfType<IHtmlTemplateElement>()
            .SelectMany(template => template.Content.QuerySelectorAll("*")));

    private static bool IsEventHandler(string name) =>
        name.StartsWith("on", StringComparison.Ordinal)
        || name.StartsWith("hx-on", StringComparison.Ordinal)
        || name.StartsWith("data-hx-on", StringComparison.Ordinal);

    private static bool IsScriptValue(string value)
    {
        var trimmed = value.TrimStart();

        return trimmed.StartsWith("js:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertResolved(params LocalizedString[] texts) =>
        Assert.All(texts, text =>
        {
            Assert.False(text.ResourceNotFound, text.Name);
            Assert.NotEqual(text.Name, text.Value);
        });
}
