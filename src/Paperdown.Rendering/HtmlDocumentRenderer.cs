using System.Reflection;
using System.Text;
using Paperdown.Core.Interfaces;
using Paperdown.Core.Models;
using Paperdown.Rendering.Emoji;

namespace Paperdown.Rendering;

public sealed class HtmlDocumentRenderer : IDocumentRenderer
{
    private readonly IMarkdownProcessor _markdownProcessor;
    private readonly IEmojiResolver _emojiResolver;

    public HtmlDocumentRenderer(IMarkdownProcessor markdownProcessor, IEmojiResolver? emojiResolver = null)
    {
        _markdownProcessor = markdownProcessor;
        _emojiResolver = emojiResolver ?? new EmojiResolver();
    }

    public string RenderCompleteHtmlDocument(string markdown, DocumentSettings settings)
    {
        var bodyHtml = _markdownProcessor.ConvertToHtml(markdown, settings);
        if (settings.EnableEmojiProcessing)
        {
            bodyHtml = _emojiResolver.ProcessEmojisInHtml(bodyHtml);
        }

        var baseCss = LoadEmbeddedCss("base-print.css");
        var themeCss = LoadEmbeddedCss($"theme-{settings.Theme.ToString().ToLowerInvariant()}.css");

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        sb.AppendLine($"  <title>{Escape(settings.Branding.DocumentTitle)}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(baseCss);
        sb.AppendLine(themeCss);

        if (settings.Branding.Enabled)
        {
            sb.AppendLine($"  :root {{ --accent-color: {settings.Branding.AccentColorHex}; }}");
            sb.AppendLine(
                $"  body {{ font-family: {settings.Branding.BodyFont}; font-size: {settings.Branding.BaseFontSizePt}pt; line-height: {settings.Branding.LineSpacing}; }}");
            sb.AppendLine($"  h1, h2, h3, h4, h5, h6 {{ font-family: {settings.Branding.HeadingFont}; }}");
        }

        var pageSize = settings.Page.PageSize == PageSize.A4 ? "A4" : "letter";
        var orientation = settings.Page.Orientation == PageOrientation.Landscape ? "landscape" : "portrait";
        sb.AppendLine(
            $"  @page {{ size: {pageSize} {orientation}; margin: {settings.Page.MarginTopMm}mm {settings.Page.MarginRightMm}mm {settings.Page.MarginBottomMm}mm {settings.Page.MarginLeftMm}mm; }}");

        if (settings.Branding.Enabled && settings.Branding.ShowPageNumbers)
        {
            sb.AppendLine("  @page { @bottom-center { content: counter(page); font-size: 9pt; color: #64748b; } }");
        }

        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine($"<body class=\"theme-{settings.Theme.ToString().ToLowerInvariant()}\">");

        if (settings.Branding.Enabled && !string.IsNullOrWhiteSpace(settings.Branding.WatermarkText))
        {
            sb.AppendLine(
                $"  <div class=\"watermark\">{Escape(settings.Branding.WatermarkText)}</div>");
        }

        if (settings.Branding.Enabled && settings.Branding.ShowCoverPage)
        {
            AppendCoverPage(sb, settings.Branding);
        }

        sb.AppendLine("  <main class=\"paperdown-document\">");
        sb.AppendLine(bodyHtml);
        sb.AppendLine("  </main>");

        if (settings.Branding.Enabled && !string.IsNullOrWhiteSpace(settings.Branding.FooterText))
        {
            sb.AppendLine($"  <footer class=\"document-footer\">{Escape(settings.Branding.FooterText)}</footer>");
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static void AppendCoverPage(StringBuilder sb, BrandingSettings branding)
    {
        sb.AppendLine("  <section class=\"cover-page\">");
        if (!string.IsNullOrWhiteSpace(branding.LogoDataUri))
        {
            var align = branding.LogoAlignment.ToString().ToLowerInvariant();
            sb.AppendLine(
                $"    <div class=\"cover-logo cover-logo-{align}\"><img src=\"{branding.LogoDataUri}\" style=\"max-width:{branding.LogoWidthPx}px;height:auto;\" alt=\"Logo\" /></div>");
        }

        if (!string.IsNullOrWhiteSpace(branding.CompanyName))
        {
            sb.AppendLine($"    <p class=\"cover-company\">{Escape(branding.CompanyName)}</p>");
        }

        sb.AppendLine($"    <h1 class=\"cover-title\">{Escape(branding.DocumentTitle)}</h1>");
        if (!string.IsNullOrWhiteSpace(branding.Subtitle))
        {
            sb.AppendLine($"    <p class=\"cover-subtitle\">{Escape(branding.Subtitle)}</p>");
        }

        sb.AppendLine("    <div class=\"cover-meta\">");
        if (!string.IsNullOrWhiteSpace(branding.Author))
        {
            sb.AppendLine($"      <div><strong>Author:</strong> {Escape(branding.Author)}</div>");
        }

        sb.AppendLine($"      <div><strong>Date:</strong> {Escape(branding.DocumentDate)}</div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </section>");
    }

    private static string LoadEmbeddedCss(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"Paperdown.Rendering.Themes.Styles.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string Escape(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : System.Net.WebUtility.HtmlEncode(value);
}
