using Paperdown.Core.Models;
using Paperdown.Markdown;
using Paperdown.Rendering;

namespace Paperdown.Tests;

public class HtmlDocumentRendererTests
{
    [Fact]
    public void Themes_EmbedCssForEachBuiltInTheme()
    {
        var renderer = new HtmlDocumentRenderer(new MarkdownProcessor());
        foreach (var theme in Enum.GetValues<DocumentTheme>())
        {
            var html = renderer.RenderCompleteHtmlDocument("# Title", new DocumentSettings { Theme = theme });
            Assert.Contains($"theme-{theme.ToString().ToLowerInvariant()}", html, StringComparison.Ordinal);
            Assert.Contains("@page", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Branding_CoverPageRendersWhenEnabled()
    {
        var settings = new DocumentSettings
        {
            Branding = new BrandingSettings
            {
                Enabled = true,
                ShowCoverPage = true,
                DocumentTitle = "Quarterly Report",
                CompanyName = "Acme",
            },
        };

        var renderer = new HtmlDocumentRenderer(new MarkdownProcessor());
        var html = renderer.RenderCompleteHtmlDocument("# Body", settings);
        Assert.Contains("cover-page", html, StringComparison.Ordinal);
        Assert.Contains("Quarterly Report", html, StringComparison.Ordinal);
    }
}
