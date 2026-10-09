using System.Text;
using System.Text.RegularExpressions;
using Paperdown.Core.Interfaces;

namespace Paperdown.Rendering.Emoji;

public sealed class EmojiResolver : IEmojiResolver
{
    private static readonly Regex EmojiLikeRegex = new(
        @"(\p{Extended_Pictographic}[\uFE0F\uFE0E]?(?:\u200D\p{Extended_Pictographic}[\uFE0F\uFE0E]?)*|\uD83C[\uDDE6-\uDDFF]\uD83C[\uDDE6-\uDDFF])",
        RegexOptions.Compiled);

    public string ProcessEmojisInHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var ordered = EmojiCatalog.RegisteredGraphemes.Keys.OrderByDescending(k => k.Length).ToList();
        var result = html;

        foreach (var grapheme in ordered)
        {
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

        foreach (Match match in EmojiLikeRegex.Matches(html))
        {
            var grapheme = match.Value;
            if (grapheme.Contains("paperdown-emoji", StringComparison.Ordinal))
            {
                continue;
            }

            if (!EmojiCatalog.TryGetAssetCode(grapheme, out _))
            {
                unresolved.Add(grapheme);
            }
        }

        return unresolved.Distinct().ToList();
    }
}
