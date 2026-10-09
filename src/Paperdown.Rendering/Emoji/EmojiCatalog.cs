namespace Paperdown.Rendering.Emoji;

public static class EmojiCatalog
{
    private static readonly Dictionary<string, string> GraphemeToCode = new()
    {
        ["🔒"] = "1f512",
        ["📘"] = "1f4d8",
        ["✅"] = "2705",
        ["🚀"] = "1f680",
        ["❤️"] = "2764",
        ["❤"] = "2764",
        ["🔥"] = "1f525",
        ["🎉"] = "1f389",
        ["⚠️"] = "26a0",
        ["⚠"] = "26a0",
        ["🧑‍💻"] = "1f9d1-200d-1f4bb",
        ["🧑💻"] = "1f9d1-200d-1f4bb",
        ["🇷🇴"] = "1f1f7-1f1f4",
    };

    private static readonly Dictionary<string, string> DataUriCache = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, string> RegisteredGraphemes => GraphemeToCode;

    public static bool TryGetAssetCode(string grapheme, out string code)
    {
        if (GraphemeToCode.TryGetValue(grapheme, out var found))
        {
            code = found;
            return true;
        }

        code = string.Empty;
        return false;
    }

    public static bool TryGetDataUri(string grapheme, out string dataUri)
    {
        if (!GraphemeToCode.TryGetValue(grapheme, out var code))
        {
            dataUri = string.Empty;
            return false;
        }

        if (DataUriCache.TryGetValue(code, out dataUri))
        {
            return true;
        }

        var path = Path.Combine(GetAssetsRoot(), $"{code}.svg");
        if (!File.Exists(path))
        {
            dataUri = string.Empty;
            return false;
        }

        var svg = File.ReadAllText(path);
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg));
        dataUri = $"data:image/svg+xml;base64,{base64}";
        DataUriCache[code] = dataUri;
        return true;
    }

    public static string GetAssetsRoot()
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "Twemoji", "svg");
    }
}
