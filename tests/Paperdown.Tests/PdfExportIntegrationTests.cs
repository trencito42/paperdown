using Paperdown.Core.Models;
using Paperdown.Markdown;
using Paperdown.Pdf;
using Paperdown.Rendering;
using UglyToad.PdfPig;

namespace Paperdown.Tests;

public class PdfExportIntegrationTests
{
    [Fact]
    public async Task EmojiFixture_ExportsSearchablePdf_WithBundledEmojiImages()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "emoji-regression.md");
        var markdown = await File.ReadAllTextAsync(fixture);
        var settings = new DocumentSettings { Theme = DocumentTheme.Minimal };
        var renderer = new HtmlDocumentRenderer(new MarkdownProcessor());
        var html = renderer.RenderCompleteHtmlDocument(markdown, settings);

        var output = Path.Combine(Path.GetTempPath(), $"paperdown_test_{Guid.NewGuid():N}.pdf");
        var exporter = new WebView2PdfExportService();
        var result = await exporter.ExportHtmlToPdfAsync(html, output, settings.Page);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(File.Exists(output));
        Assert.True(new FileInfo(output).Length > 2_000);

        Assert.Contains("data:image/svg+xml;base64,", html, StringComparison.OrdinalIgnoreCase);

        var pdfBytes = await File.ReadAllBytesAsync(output);
        var pdfHeader = System.Text.Encoding.ASCII.GetString(pdfBytes.AsSpan(0, Math.Min(8, pdfBytes.Length)));
        Assert.StartsWith("%PDF-", pdfHeader, StringComparison.Ordinal);

        Assert.True(result.PageCount >= 1);
        using (var pdf = PdfDocument.Open(output))
        {
            Assert.True(pdf.NumberOfPages >= 1);
            var text = string.Join(' ', pdf.GetPages().Select(p => p.Text));
            Assert.Contains("Emoji", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task RomanianFixture_ExportsSearchableText()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "romanian-diacritics.md");
        var markdown = await File.ReadAllTextAsync(fixture);
        var settings = new DocumentSettings { Theme = DocumentTheme.Minimal };
        var renderer = new HtmlDocumentRenderer(new MarkdownProcessor());
        var html = renderer.RenderCompleteHtmlDocument(markdown, settings);
        var output = Path.Combine(Path.GetTempPath(), $"paperdown_ro_{Guid.NewGuid():N}.pdf");
        var exporter = new WebView2PdfExportService();
        var result = await exporter.ExportHtmlToPdfAsync(html, output, settings.Page);
        Assert.True(result.Success, result.ErrorMessage);

        var text = PdfPageCounter.ExtractText(output);
        Assert.Contains("București", text, StringComparison.Ordinal);
    }
}
