namespace Paperdown.Rendering;

public static class PreviewHtmlHelper
{
    public static string WrapForScreenPreview(string exportHtml, double zoom = 1.0)
    {
        const string previewCss = """
<style id="paperdown-preview-style">
  @media screen {
    html { background: #d4d4d8; }
    body {
      margin: 0;
      padding: 24px 0 48px;
      background: #d4d4d8;
    }
    main.paperdown-document {
      width: 210mm;
      min-height: 297mm;
      margin: 0 auto 24px;
      padding: 18mm 16mm;
      background: #ffffff;
      box-shadow: 0 4px 24px rgba(15, 23, 42, 0.12);
    }
    body.theme-dark main.paperdown-document {
      background: #0f172a;
    }
    .cover-page {
      width: 210mm;
      margin: 0 auto 24px;
      padding: 24mm 18mm;
      background: #ffffff;
      box-shadow: 0 4px 24px rgba(15, 23, 42, 0.12);
    }
  }
</style>
""";

        var zoomStyle = $"<style>body {{ zoom: {zoom.ToString(System.Globalization.CultureInfo.InvariantCulture)}; }}</style>";
        if (exportHtml.Contains("</head>", StringComparison.OrdinalIgnoreCase))
        {
            return exportHtml.Replace("</head>", previewCss + zoomStyle + "</head>", StringComparison.OrdinalIgnoreCase);
        }

        return previewCss + zoomStyle + exportHtml;
    }
}
