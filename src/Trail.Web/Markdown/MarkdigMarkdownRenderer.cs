using Markdig;

namespace Trail.Web.Markdown;

public sealed class MarkdigMarkdownRenderer : IMarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdigMarkdownRenderer()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .DisableHtml()
            .UseAdvancedExtensions()
            .Build();
    }

    public string RenderToHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        return global::Markdig.Markdown.ToHtml(markdown, _pipeline);
    }
}
