using Paperdown.Core.Models;
using Paperdown.Markdown;
using Paperdown.Rendering;

namespace Paperdown.Tests;

public class RomanianDiacriticsTests
{
    [Fact]
    public void Fixture_PreservesRomanianCharactersInHtml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "romanian-diacritics.md");
        var markdown = File.ReadAllText(path);
        var renderer = new HtmlDocumentRenderer(new MarkdownProcessor());
        var html = renderer.RenderCompleteHtmlDocument(markdown, new DocumentSettings());

        Assert.Contains("Înțelepciune", html, StringComparison.Ordinal);
        Assert.Contains("ăâîșț", html, StringComparison.Ordinal);
        Assert.Contains("București", html, StringComparison.Ordinal);
    }
}
