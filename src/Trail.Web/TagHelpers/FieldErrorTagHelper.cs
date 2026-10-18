using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Trail.Web.Forms;

namespace Trail.Web.TagHelpers;

[HtmlTargetElement("input", Attributes = ForAttribute)]
[HtmlTargetElement("select", Attributes = ForAttribute)]
[HtmlTargetElement("textarea", Attributes = ForAttribute)]
[HtmlTargetElement("span", Attributes = ValidationForAttribute)]
public sealed class FieldErrorTagHelper : TagHelper
{
    private const string ForAttribute = "asp-for";
    private const string ValidationForAttribute = "asp-validation-for";
    private const string ErrorSpanAttribute = "asp-error-span";
    private const string ErrorSpanIdAttribute = "asp-error-span-id";
    private const string ErrorIdSuffix = "-error";

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    [HtmlAttributeName(ForAttribute)]
    public ModelExpression? For { get; set; }

    [HtmlAttributeName(ValidationForAttribute)]
    public ModelExpression? ValidationFor { get; set; }

    [HtmlAttributeName(ErrorSpanAttribute)]
    public bool ErrorSpan { get; set; }

    [HtmlAttributeName(ErrorSpanIdAttribute)]
    public string? ErrorSpanId { get; set; }

    public override int Order => 1;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (ValidationFor is not null)
        {
            ProcessValidationSpan(output, FullName(ValidationFor.Name));
            return;
        }

        if (For is null)
        {
            return;
        }

        var fullName = FullName(For.Name);
        if (!HasErrors(fullName))
        {
            return;
        }

        output.Attributes.SetAttribute("aria-invalid", "true");

        if (!ErrorSpan && ErrorSpanId is null)
        {
            return;
        }

        var errorId = ErrorSpanId ?? DerivedErrorId(fullName);
        RenderedFieldErrors.ReferencedSpanIds(ViewContext.HttpContext)[fullName] = errorId;

        var describedBy = output.Attributes["aria-describedby"]?.Value?.ToString();
        output.Attributes.SetAttribute(
            "aria-describedby",
            string.IsNullOrWhiteSpace(describedBy) ? errorId : $"{describedBy} {errorId}");
    }

    private void ProcessValidationSpan(TagHelperOutput output, string fullName)
    {
        var authoredId = output.Attributes["id"]?.Value?.ToString();
        var spanId = string.IsNullOrWhiteSpace(authoredId) ? DerivedErrorId(fullName) : authoredId;

        if (RenderedFieldErrors.ReferencedSpanIds(ViewContext.HttpContext).TryGetValue(fullName, out var referencedId)
            && !string.Equals(referencedId, spanId, StringComparison.Ordinal))
        {
            RenderedFieldErrors.RecordMismatch(
                ViewContext.HttpContext,
                $"The field '{fullName}' points aria-describedby at '{referencedId}', but its validation span has the id '{spanId}'. Set asp-error-span-id on the field to the span id.");
            spanId = referencedId;
            authoredId = null;
        }

        if (string.IsNullOrWhiteSpace(authoredId))
        {
            output.Attributes.SetAttribute("id", spanId);
        }

        RenderedFieldErrors.MarkRendered(ViewContext.HttpContext, fullName);
    }

    private bool HasErrors(string fullName) =>
        ViewContext.ViewData.ModelState.TryGetValue(fullName, out var entry) && entry.Errors.Count > 0;

    private static string DerivedErrorId(string fullName) =>
        TagBuilder.CreateSanitizedId(fullName, "_") + ErrorIdSuffix;

    private string FullName(string expression) =>
        ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(expression);
}
