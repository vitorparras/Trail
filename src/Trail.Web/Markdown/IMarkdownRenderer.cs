namespace Trail.Web.Markdown;

public interface IMarkdownRenderer
{
    string RenderToHtml(string? markdown);
}
