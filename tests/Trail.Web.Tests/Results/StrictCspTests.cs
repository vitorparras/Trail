using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;
using Trail.Domain.Common;
using Trail.Web.Results;

namespace Trail.Web.Tests.Results;

public sealed class StrictCspTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task StatusPartials_HaveNoStrictCspViolations()
    {
        var notFoundDoc = await PartialRenderer.RenderAsync(factory.Services, "/Pages/Shared/_NotFound.cshtml", new NotFoundViewModel());
        var conflictDoc = await PartialRenderer.RenderAsync(factory.Services, "/Pages/Shared/_Conflict.cshtml", new ConflictViewModel(ReloadUrl: "/test"));
        var forbiddenDoc = await PartialRenderer.RenderAsync(factory.Services, "/Pages/Shared/_Forbidden.cshtml", new ForbiddenViewModel());
        var errorDoc = await PartialRenderer.RenderAsync(factory.Services, "/Pages/Shared/_Error.cshtml", new ErrorViewModel("trace-123"));

        AssertStrictCsp(notFoundDoc);
        AssertStrictCsp(conflictDoc);
        AssertStrictCsp(forbiddenDoc);
        AssertStrictCsp(errorDoc);
    }

    [Fact]
    public async Task StatusPages_HaveNoStrictCspViolations()
    {
        await using var notFoundContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: false);
        var (nfContext, notFoundPage) = notFoundContext;
        var notFoundResult = notFoundPage.Respond(Result.NotFound(), "Find");
        var (_, _, _, notFoundDoc) = await ResultTestHelper.ExecuteAsync(notFoundResult, nfContext);

        await using var conflictContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: false);
        var (cContext, conflictPage) = conflictContext;
        var conflictResult = conflictPage.Respond(Result.ConcurrencyConflict(), "Update");
        var (_, _, _, conflictDoc) = await ResultTestHelper.ExecuteAsync(conflictResult, cContext);

        await using var forbiddenContext = ResultTestHelper.CreateContext(factory.Services, isHtmx: false);
        var (fContext, forbiddenPage) = forbiddenContext;
        var forbiddenResult = forbiddenPage.Respond(Result.Forbidden(), "Admin");
        var (_, _, _, forbiddenDoc) = await ResultTestHelper.ExecuteAsync(forbiddenResult, fContext);

        using var client = factory.CreateClient();
        using var errorResponse = await client.GetAsync("/Error", TestContext.Current.CancellationToken);
        var errorHtml = await errorResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var errorDoc = new HtmlParser().ParseDocument(errorHtml);

        AssertStrictCsp(notFoundDoc);
        AssertStrictCsp(conflictDoc);
        AssertStrictCsp(forbiddenDoc);
        AssertStrictCsp(errorDoc);
    }

    private static void AssertStrictCsp(IHtmlDocument document)
    {
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
}
