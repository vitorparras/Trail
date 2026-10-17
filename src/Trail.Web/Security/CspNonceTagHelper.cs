using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Trail.Web.Security;

[HtmlTargetElement("script")]
[HtmlTargetElement("style")]
[HtmlTargetElement(Attributes = "hx-get")]
[HtmlTargetElement(Attributes = "hx-post")]
[HtmlTargetElement(Attributes = "hx-put")]
[HtmlTargetElement(Attributes = "hx-delete")]
[HtmlTargetElement(Attributes = "hx-patch")]
[HtmlTargetElement(Attributes = "hx-boost")]
[HtmlTargetElement(Attributes = "data-hx-get")]
[HtmlTargetElement(Attributes = "data-hx-post")]
[HtmlTargetElement(Attributes = "data-hx-put")]
[HtmlTargetElement(Attributes = "data-hx-delete")]
[HtmlTargetElement(Attributes = "data-hx-patch")]
[HtmlTargetElement(Attributes = "data-hx-boost")]
public sealed class CspNonceTagHelper(ICspNonceService nonceService) : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(output);

        var isScriptOrStyle = string.Equals(context.TagName, "script", StringComparison.OrdinalIgnoreCase)
            || string.Equals(context.TagName, "style", StringComparison.OrdinalIgnoreCase);

        if (isScriptOrStyle)
        {
            if (!output.Attributes.ContainsName("nonce")
                || string.IsNullOrWhiteSpace(output.Attributes["nonce"]?.Value?.ToString()))
            {
                output.Attributes.SetAttribute("nonce", nonceService.GetNonce());
            }
        }

        var hasHxAttribute = context.AllAttributes.Any(attribute =>
            attribute.Name.StartsWith("hx-", StringComparison.OrdinalIgnoreCase)
            || attribute.Name.StartsWith("data-hx-", StringComparison.OrdinalIgnoreCase));

        if (hasHxAttribute)
        {
            var forbiddenAttribute = context.AllAttributes.FirstOrDefault(attribute =>
                attribute.Name.StartsWith("hx-on", StringComparison.OrdinalIgnoreCase)
                || attribute.Name.StartsWith("data-hx-on", StringComparison.OrdinalIgnoreCase)
                || (attribute.Value?.ToString()?.TrimStart().StartsWith("js:", StringComparison.OrdinalIgnoreCase) ?? false)
                || (attribute.Value?.ToString()?.TrimStart().StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ?? false));

            if (forbiddenAttribute is not null)
            {
                throw new InvalidOperationException($"Forbidden attribute '{forbiddenAttribute.Name}' detected under strict CSP.");
            }

            output.Attributes.SetAttribute("data-hx-csp", "true");
        }
    }
}
