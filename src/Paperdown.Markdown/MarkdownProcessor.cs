using System.Text.RegularExpressions;
using Markdig;
using Paperdown.Core.Interfaces;
using Paperdown.Core.Models;

namespace Paperdown.Markdown;

public sealed class MarkdownProcessor : IMarkdownProcessor
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownProcessor()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseYamlFrontMatter()
            .UseAutoIdentifiers()
            .UseTaskLists()
            .UsePipeTables()
            .UseGridTables()
            .UseEmphasisExtras()
            .Build();
    }

    public string ConvertToHtml(string markdown, DocumentSettings settings)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var document = Markdig.Markdown.Parse(markdown, _pipeline);
        using var writer = new StringWriter();
        var renderer = new Markdig.Renderers.HtmlRenderer(writer);
        _pipeline.Setup(renderer);
        renderer.Render(document);
        writer.Flush();

        var html = writer.ToString();
        html = TransformAlerts(html);

        if (settings.SanitizeHtml)
        {
            html = HtmlSanitizerService.Sanitize(html);
        }

        return html;
    }

    private static string TransformAlerts(string html)
    {
        string[] alertTypes = ["NOTE", "TIP", "IMPORTANT", "WARNING", "CAUTION"];

        foreach (var type in alertTypes)
        {
            var pattern = $@"<blockquote>\s*<p>\[!{type}\](.*?)<\/p>(.*?)<\/blockquote>";
            var replacement =
                $@"<div class=""markdown-alert markdown-alert-{type.ToLowerInvariant()}""><div class=""markdown-alert-title"">{type}</div><div class=""markdown-alert-content""><p>$1</p>$2</div></div>";
            html = Regex.Replace(html, pattern, replacement, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        }

        return html;
    }

}
