using Paperdown.Core.Models;

namespace Paperdown.Rendering;

public static class PreviewHtmlHelper
{
    public static string WrapForScreenPreview(string exportHtml, DocumentSettings settings, double zoom = 1.0)
    {
        var (pageWidthMm, pageHeightMm) = PageDimensions.GetPageSizeMm(settings.Page);
        var pageWidth = pageWidthMm.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var pageHeight = pageHeightMm.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var previewCss = $$"""
<style id="paperdown-preview-style">
  @media screen {
    html { background: #e4e4e7; }
    body {
      margin: 0;
      padding: 28px 0 56px;
      background: #e4e4e7;
    }
    .paperdown-preview-note {
      max-width: {{pageWidth}}mm;
      margin: 0 auto 12px;
      text-align: center;
      font-size: 11px;
      color: #64748b;
    }
    main.paperdown-document {
      width: {{pageWidth}}mm;
      min-height: {{pageHeight}}mm;
      margin: 0 auto 28px;
      border: 1px solid #cbd5e1;
      padding: {{settings.Page.MarginTopMm.ToString(System.Globalization.CultureInfo.InvariantCulture)}}mm {{settings.Page.MarginRightMm.ToString(System.Globalization.CultureInfo.InvariantCulture)}}mm {{settings.Page.MarginBottomMm.ToString(System.Globalization.CultureInfo.InvariantCulture)}}mm {{settings.Page.MarginLeftMm.ToString(System.Globalization.CultureInfo.InvariantCulture)}}mm;
      background: #ffffff;
      box-shadow: 0 2px 8px rgba(15, 23, 42, 0.08), 0 12px 32px rgba(15, 23, 42, 0.12);
      box-sizing: border-box;
    }
    body.theme-dark main.paperdown-document {
      background: #0f172a;
      color: #e2e8f0;
    }
    .cover-page {
      width: {{pageWidth}}mm;
      min-height: {{pageHeight}}mm;
      margin: 0 auto 28px;
      padding: 24mm 18mm;
      background: #ffffff;
      box-shadow: 0 2px 8px rgba(15, 23, 42, 0.08), 0 12px 32px rgba(15, 23, 42, 0.12);
      box-sizing: border-box;
    }
    body.theme-dark .cover-page {
      background: #0f172a;
    }
  }
</style>
""";

        var zoomStyle = $"<style>body {{ zoom: {zoom.ToString(System.Globalization.CultureInfo.InvariantCulture)}; }}</style>";
        const string note =
            "<p class=\"paperdown-preview-note\">Screen preview uses your page size and margins. Final pagination matches the exported PDF, not this continuous layout.</p>";

        if (exportHtml.Contains("</body>", StringComparison.OrdinalIgnoreCase))
        {
            exportHtml = exportHtml.Replace("</body>", note + "</body>", StringComparison.OrdinalIgnoreCase);
        }

        if (exportHtml.Contains("</head>", StringComparison.OrdinalIgnoreCase))
        {
            return exportHtml.Replace("</head>", previewCss + zoomStyle + "</head>", StringComparison.OrdinalIgnoreCase);
        }

        return previewCss + zoomStyle + exportHtml;
    }
}
