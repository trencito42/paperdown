using System.Net;
using HtmlAgilityPack;

namespace Paperdown.Rendering;

internal static class RelativeAssetResolver
{
    private static readonly HashSet<string> AllowedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".bmp" };

    public static string ResolveImages(string html, string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(html) || string.IsNullOrWhiteSpace(baseDirectory))
        {
            return html;
        }

        var baseFull = Path.GetFullPath(baseDirectory);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        foreach (var img in doc.DocumentNode.SelectNodes("//img[@src]") ?? Enumerable.Empty<HtmlNode>())
        {
            var src = img.GetAttributeValue("src", string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(src)
                || src.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || src.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || src.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                || src.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryResolveLocalImage(baseFull, src, out var dataUri))
            {
                img.SetAttributeValue("alt", "Image not found");
                img.SetAttributeValue("src", string.Empty);
                continue;
            }

            img.SetAttributeValue("src", dataUri);
        }

        return doc.DocumentNode.OuterHtml;
    }

    private static bool TryResolveLocalImage(string baseDirectory, string relativePath, out string dataUri)
    {
        dataUri = string.Empty;
        try
        {
            var combined = Path.GetFullPath(Path.Combine(baseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!combined.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!File.Exists(combined))
            {
                return false;
            }

            var ext = Path.GetExtension(combined);
            if (!AllowedImageExtensions.Contains(ext))
            {
                return false;
            }

            var bytes = File.ReadAllBytes(combined);
            if (bytes.Length > 8 * 1024 * 1024)
            {
                return false;
            }

            var mime = ext.ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream",
            };

            dataUri = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            return true;
        }
        catch
        {
            return false;
        }
    }
}
