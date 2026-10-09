using Paperdown.Core.Models;
using Paperdown.Markdown;
using Paperdown.Rendering;
using Paperdown.Rendering.Emoji;

namespace Paperdown.Tests;

public class EmojiResolutionTests
{
    [Fact]
    public void Regression_LockedAndTutorialEmojis_UseBundledSvgImages()
    {
        var renderer = new HtmlDocumentRenderer(new MarkdownProcessor());
        var html = renderer.RenderCompleteHtmlDocument("🔒 Restricted\n\n📘 Tutorial", new DocumentSettings());

        Assert.Contains("paperdown-emoji", html, StringComparison.Ordinal);
        Assert.Contains("data:image/svg+xml;base64,", html, StringComparison.Ordinal);
        Assert.DoesNotContain("🔒", html, StringComparison.Ordinal);
        Assert.DoesNotContain("📘", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RegressionFixture_ContainsAllBundledEmojiCodes()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "emoji-regression.md");
        var markdown = File.ReadAllText(path);
        var renderer = new HtmlDocumentRenderer(new MarkdownProcessor());
        var html = renderer.RenderCompleteHtmlDocument(markdown, new DocumentSettings());

        string[] codes = ["2705", "1f512", "1f4d8", "1f680", "2764", "1f525", "1f389", "26a0", "1f9d1-200d-1f4bb", "1f1f7-1f1f4"];
        foreach (var code in codes)
        {
            Assert.Contains($"data-emoji-code=\"{code}\"", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Catalog_ExposesTwemojiAssetsOnDisk()
    {
        var root = EmojiCatalog.GetAssetsRoot();
        Assert.True(Directory.Exists(root));
        Assert.True(File.Exists(Path.Combine(root, "1f512.svg")));
    }
}
