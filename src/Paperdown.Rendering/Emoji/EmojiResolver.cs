using HtmlAgilityPack;
using Paperdown.Core.Interfaces;

namespace Paperdown.Rendering.Emoji;

public sealed class EmojiResolver : IEmojiResolver
{
    public string ProcessEmojisInHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var doc = new HtmlDocument();
        doc.LoadHtml($"<wrapper id=\"paperdown-emoji-root\">{html}</wrapper>");
        var root = doc.GetElementbyId("paperdown-emoji-root");
        if (root is null)
        {
            return html;
        }

        ProcessTextNodes(root);
        return root.InnerHtml;
    }

    private static void ProcessTextNodes(HtmlNode node)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            var parentName = node.ParentNode?.Name ?? string.Empty;
            if (parentName is "script" or "style" or "textarea" or "code" or "pre")
            {
                return;
            }

            var text = node.InnerText;
            if (string.IsNullOrEmpty(text) || !MightContainEmoji(text))
            {
                return;
            }

            var replaced = ReplaceEmojiInText(text);
            if (!ReferenceEquals(replaced, text))
            {
                var span = HtmlNode.CreateNode("<span></span>");
                span.InnerHtml = replaced;
                node.ParentNode?.ReplaceChild(span, node);
            }

            return;
        }

        if (node.Name is "script" or "style" or "textarea" or "code" or "pre")
        {
            return;
        }

        foreach (var child in node.ChildNodes.ToList())
        {
            ProcessTextNodes(child);
        }
    }

    private static bool MightContainEmoji(string text)
    {
        foreach (var ch in text)
        {
            if (char.IsSurrogate(ch) || ch > 255)
            {
                return true;
            }
        }

        return false;
    }

    private static string ReplaceEmojiInText(string text)
    {
        var ordered = EmojiCatalog.RegisteredGraphemes.Keys.OrderByDescending(k => k.Length).ToList();
        var result = text;
        foreach (var grapheme in ordered)
        {
            if (!result.Contains(grapheme, StringComparison.Ordinal))
            {
                continue;
            }

            if (!EmojiCatalog.TryGetDataUri(grapheme, out var dataUri))
            {
                continue;
            }

            var img =
                $"<img class=\"paperdown-emoji\" src=\"{dataUri}\" alt=\"\" role=\"presentation\" data-emoji-code=\"{EmojiCatalog.RegisteredGraphemes[grapheme]}\" />";
            result = result.Replace(grapheme, img, StringComparison.Ordinal);
        }

        return result;
    }

    public IReadOnlyList<string> GetUnresolvedEmojiGraphemes(string html)
    {
        var unresolved = new List<string>();
        if (string.IsNullOrEmpty(html))
        {
            return unresolved;
        }

        foreach (var grapheme in EmojiCatalog.RegisteredGraphemes.Keys)
        {
            if (html.Contains(grapheme, StringComparison.Ordinal) && !EmojiCatalog.TryGetAssetCode(grapheme, out _))
            {
                unresolved.Add(grapheme);
            }
        }

        foreach (var match in EnumerateLikelyEmojiGraphemes(html))
        {
            if (!EmojiCatalog.TryGetAssetCode(match, out _))
            {
                unresolved.Add(match);
            }
        }

        return unresolved.Distinct().ToList();
    }

    private static IEnumerable<string> EnumerateLikelyEmojiGraphemes(string html)
    {
        for (var i = 0; i < html.Length; i++)
        {
            if (!char.IsSurrogate(html[i]))
            {
                continue;
            }

            if (char.IsHighSurrogate(html[i]) && i + 1 < html.Length && char.IsLowSurrogate(html[i + 1]))
            {
                yield return html.Substring(i, 2);
                i++;
            }
        }
    }
}
