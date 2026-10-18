using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Trail.Web.Forms;
using Trail.Web.TagHelpers;

namespace Trail.Web.Tests.Forms;

public sealed class FieldErrorTagHelperTests
{
    [Fact]
    public void Process_InputWithErrors_MarksItInvalidWithoutDescribingItByDefault()
    {
        var output = ProcessInput(withError: true, errorSpan: false);

        Assert.Equal("true", output.Attributes["aria-invalid"].Value);
        Assert.False(output.Attributes.ContainsName("aria-describedby"));
    }

    [Fact]
    public void Process_InputWithErrorsAndErrorSpan_PointsAtTheValidationSpan()
    {
        var output = ProcessInput(withError: true, errorSpan: true);

        Assert.Equal("true", output.Attributes["aria-invalid"].Value);
        Assert.Equal("Input_Email-error", output.Attributes["aria-describedby"].Value);
    }

    [Fact]
    public void Process_InputWithErrorsAndAnAuthoredDescription_KeepsItAndAppendsTheSpanOnlyWhenAsked()
    {
        var authored = new TagHelperAttribute("aria-describedby", "email-hint");

        Assert.Equal("email-hint", ProcessInput(withError: true, errorSpan: false, authored).Attributes["aria-describedby"].Value);
        Assert.Equal("email-hint Input_Email-error", ProcessInput(withError: true, errorSpan: true, authored).Attributes["aria-describedby"].Value);
    }

    [Fact]
    public void Process_InputWithoutErrors_ChangesNothing()
    {
        var output = ProcessInput(withError: false, errorSpan: true);

        Assert.Empty(output.Attributes);
    }

    [Fact]
    public void Process_InputInsideAFieldPrefix_UsesTheFullFieldNameForTheErrorLookupAndTheId()
    {
        var viewContext = CreateViewContext();
        viewContext.ViewData.TemplateInfo.HtmlFieldPrefix = "PasswordInput";
        viewContext.ViewData.ModelState.AddModelError("PasswordInput.NewPassword", "Problem");
        var output = Process("input", viewContext, helper =>
        {
            helper.For = Expression("NewPassword");
            helper.ErrorSpan = true;
        });

        Assert.Equal("PasswordInput_NewPassword-error", output.Attributes["aria-describedby"].Value);
    }

    [Fact]
    public void Process_ValidationSpan_GetsTheDerivedId()
    {
        var output = Process("span", CreateViewContext(), helper => helper.ValidationFor = Expression("Input.Email"));

        Assert.Equal("Input_Email-error", output.Attributes["id"].Value);
    }

    [Fact]
    public void Process_ValidationSpanWithAnAuthoredId_KeepsIt()
    {
        var output = Process(
            "span",
            CreateViewContext(),
            helper => helper.ValidationFor = Expression("Input.Email"),
            new TagHelperAttribute("id", "custom-error"));

        Assert.Equal("custom-error", output.Attributes["id"].Value);
    }

    [Fact]
    public void Process_InputPointingAtTheIdOfAnAuthoredSpanId_ReferencesTheSpansActualId()
    {
        var viewContext = CreateViewContext();
        viewContext.ViewData.ModelState.AddModelError("Input.Email", "Problem");

        var input = Process("input", viewContext, helper =>
        {
            helper.For = Expression("Input.Email");
            helper.ErrorSpan = true;
            helper.ErrorSpanId = "custom-error";
        });
        var span = Process(
            "span",
            viewContext,
            helper => helper.ValidationFor = Expression("Input.Email"),
            new TagHelperAttribute("id", "custom-error"));

        Assert.Equal("custom-error", input.Attributes["aria-describedby"].Value);
        Assert.Equal("custom-error", span.Attributes["id"].Value);
    }

    [Fact]
    public void Process_InputAndSpanDisagreeingOnTheSpanId_FallsBackToTheReferencedIdAndRecordsTheMismatch()
    {
        var viewContext = CreateViewContext();
        viewContext.ViewData.ModelState.AddModelError("Input.Email", "Problem");
        Process("input", viewContext, helper =>
        {
            helper.For = Expression("Input.Email");
            helper.ErrorSpan = true;
        });

        var span = Process(
            "span",
            viewContext,
            helper => helper.ValidationFor = Expression("Input.Email"),
            new TagHelperAttribute("id", "custom-error"));

        Assert.Equal("Input_Email-error", span.Attributes["id"].Value);
        Assert.Contains("custom-error", Assert.Single(RenderedFieldErrors.Mismatches(viewContext.HttpContext)), StringComparison.Ordinal);
    }

    [Fact]
    public void Process_ValidationSpan_IsRecordedAsRenderedForItsField()
    {
        var viewContext = CreateViewContext();

        Process("span", viewContext, helper => helper.ValidationFor = Expression("Input.Email"));

        Assert.True(RenderedFieldErrors.IsRendered(viewContext.HttpContext, "Input.Email"));
        Assert.False(RenderedFieldErrors.IsRendered(viewContext.HttpContext, "Input.Password"));
    }

    private static TagHelperOutput ProcessInput(bool withError, bool errorSpan, TagHelperAttribute? authored = null)
    {
        var viewContext = CreateViewContext();
        if (withError)
        {
            viewContext.ViewData.ModelState.AddModelError("Input.Email", "Problem");
        }

        return Process(
            "input",
            viewContext,
            helper =>
            {
                helper.For = Expression("Input.Email");
                helper.ErrorSpan = errorSpan;
            },
            authored);
    }

    private static TagHelperOutput Process(string tag, ViewContext viewContext, Action<FieldErrorTagHelper> configure, TagHelperAttribute? authored = null)
    {
        var helper = new FieldErrorTagHelper { ViewContext = viewContext };
        configure(helper);
        var attributes = new TagHelperAttributeList();
        if (authored is not null)
        {
            attributes.Add(authored);
        }

        var context = new TagHelperContext(tag, new TagHelperAttributeList(), new Dictionary<object, object>(), Guid.NewGuid().ToString("N"));
        var output = new TagHelperOutput(tag, attributes, (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        helper.Process(context, output);
        return output;
    }

    private static ModelExpression Expression(string name) =>
        new(name, new EmptyModelMetadataProvider().GetModelExplorerForType(typeof(string), null));

    private static ViewContext CreateViewContext() => new(
        new ActionContext(new DefaultHttpContext(), new Microsoft.AspNetCore.Routing.RouteData(), new ActionDescriptor()),
        new NullView(),
        new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()),
        new TempDataDictionary(new DefaultHttpContext(), new NullTempDataProvider()),
        TextWriter.Null,
        new HtmlHelperOptions());

    private sealed class NullView : IView
    {
        public string Path => string.Empty;

        public Task RenderAsync(ViewContext context) => Task.CompletedTask;
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
