using Paperdown.Core.Models;
using Paperdown.Markdown;

namespace Paperdown.Tests;

public class HtmlSanitizerTests
{
    [Fact]
    public void Sanitize_removes_script_and_event_handlers()
    {
        var processor = new MarkdownProcessor();
        var settings = new DocumentSettings { SanitizeHtml = true };
        var html = processor.ConvertToHtml(
            "<script>alert(1)</script><p onclick=\"evil()\">Hi</p><a href=\"javascript:alert(1)\">x</a>",
            settings);

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_preserves_safe_markdown_output()
    {
        var processor = new MarkdownProcessor();
        var settings = new DocumentSettings { SanitizeHtml = true };
        var html = processor.ConvertToHtml("**bold** and `code`", settings);
        Assert.Contains("<strong>bold</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<code>code</code>", html, StringComparison.Ordinal);
    }
}
