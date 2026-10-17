using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Trail.Web.TagHelpers;

[HtmlTargetElement("icon", Attributes = "name", TagStructure = TagStructure.WithoutEndTag)]
public sealed class IconTagHelper : TagHelper
{
    private const string SpritePath = "lib/lucide/sprite.svg";

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        output.TagName = "svg";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("aria-hidden", "true");
        output.Attributes.SetAttribute("fill", "none");
        output.Attributes.SetAttribute("stroke", "currentColor");
        output.Attributes.SetAttribute("stroke-width", "2");
        output.Attributes.SetAttribute("stroke-linecap", "round");
        output.Attributes.SetAttribute("stroke-linejoin", "round");

        var use = new TagBuilder("use") { TagRenderMode = TagRenderMode.SelfClosing };
        use.Attributes["href"] = $"{SpriteUrl()}#{Name}";
        output.Content.SetHtmlContent(use);
    }

    private string SpriteUrl()
    {
        var httpContext = ViewContext.HttpContext;
        var assets = httpContext.GetEndpoint()?.Metadata.GetMetadata<ResourceAssetCollection>();
        var path = assets?[SpritePath] ?? SpritePath;

        return $"{httpContext.Request.PathBase}/{path}";
    }
}
