using Paperdown.Core.Models;
using Paperdown.Markdown;

namespace Paperdown.Tests;

public class MarkdownParsingTests
{
    private readonly MarkdownProcessor _processor = new();

    [Fact]
    public void Tables_RenderWithHeaders()
    {
        const string md = """
| Header 1 | Header 2 |
| --- | --- |
| A | B |
""";
        var html = _processor.ConvertToHtml(md, new DocumentSettings());
        Assert.Contains("<table>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Header 1", html, StringComparison.Ordinal);
        Assert.Contains("Header 2", html, StringComparison.Ordinal);
    }

    [Fact]
    public void TaskLists_RenderCheckboxes()
    {
        const string md = "- [x] Done\n- [ ] Todo";
        var html = _processor.ConvertToHtml(md, new DocumentSettings());
        Assert.Contains("checkbox", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scripts_AreStrippedWhenSanitized()
    {
        const string md = "<script>alert(1)</script>\n\nHello";
        var html = _processor.ConvertToHtml(md, new DocumentSettings { SanitizeHtml = true });
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hello", html, StringComparison.Ordinal);
    }
}
