using Paperdown.Core.Models;
using Paperdown.Markdown;
using Paperdown.Pdf;
using Paperdown.Rendering;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    PrintHelp();
    return 0;
}

if (!string.Equals(args[0], "convert", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Unknown command. Use: paperdown convert <input.md> -o <output.pdf> [--theme minimal]");
    return 1;
}

var inputPath = args.Length > 1 ? args[1] : null;
if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
{
    Console.Error.WriteLine("Input Markdown file not found.");
    return 1;
}

var outputPath = GetArgValue(args, "-o") ?? Path.ChangeExtension(inputPath, ".pdf");
var themeName = GetArgValue(args, "--theme") ?? "minimal";

if (!Enum.TryParse<DocumentTheme>(themeName, ignoreCase: true, out var theme))
{
    Console.Error.WriteLine($"Unknown theme '{themeName}'.");
    return 1;
}

var markdown = await File.ReadAllTextAsync(inputPath);
var settings = new DocumentSettings { Theme = theme };
var renderer = new HtmlDocumentRenderer(new MarkdownProcessor());
var html = renderer.RenderCompleteHtmlDocument(markdown, settings);

var exporter = new WebView2PdfExportService();
var result = await exporter.ExportHtmlToPdfAsync(html, outputPath, settings.Page);

if (!result.Success)
{
    Console.Error.WriteLine(result.ErrorMessage ?? "Export failed.");
    return 1;
}

Console.WriteLine($"Wrote {result.OutputPdfPath} ({result.PageCount} page(s)) in {result.ElapsedTime.TotalSeconds:F1}s");
return 0;

static string? GetArgValue(string[] args, string key)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

static void PrintHelp()
{
    Console.WriteLine("""
Paperdown CLI 0.1.0-beta

Usage:
  paperdown convert <input.md> -o <output.pdf> [--theme minimal|github|documentation|corporate|dark]

Examples:
  paperdown convert README.md -o README.pdf --theme github
""");
}
