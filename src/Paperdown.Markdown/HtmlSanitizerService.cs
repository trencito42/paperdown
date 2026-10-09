using Ganss.Xss;

namespace Paperdown.Markdown;

internal static class HtmlSanitizerService
{
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    public static string Sanitize(string html) => Sanitizer.Sanitize(html);

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer
        {
            KeepChildNodes = true,
            AllowDataAttributes = true,
        };

        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("mailto");
        sanitizer.AllowedSchemes.Add("data");

        sanitizer.AllowedTags.Add("img");
        sanitizer.AllowedTags.Add("figure");
        sanitizer.AllowedTags.Add("figcaption");
        sanitizer.AllowedTags.Add("table");
        sanitizer.AllowedTags.Add("thead");
        sanitizer.AllowedTags.Add("tbody");
        sanitizer.AllowedTags.Add("tr");
        sanitizer.AllowedTags.Add("th");
        sanitizer.AllowedTags.Add("td");
        sanitizer.AllowedTags.Add("colgroup");
        sanitizer.AllowedTags.Add("col");
        sanitizer.AllowedTags.Add("details");
        sanitizer.AllowedTags.Add("summary");
        sanitizer.AllowedTags.Add("input");
        sanitizer.AllowedTags.Add("del");
        sanitizer.AllowedTags.Add("ins");
        sanitizer.AllowedTags.Add("mark");
        sanitizer.AllowedTags.Add("kbd");
        sanitizer.AllowedTags.Add("sup");
        sanitizer.AllowedTags.Add("sub");
        sanitizer.AllowedTags.Add("section");
        sanitizer.AllowedTags.Add("article");
        sanitizer.AllowedTags.Add("footer");
        sanitizer.AllowedTags.Add("header");
        sanitizer.AllowedTags.Add("main");
        sanitizer.AllowedTags.Add("div");
        sanitizer.AllowedTags.Add("span");

        sanitizer.AllowedAttributes.Add("class");
        sanitizer.AllowedAttributes.Add("id");
        sanitizer.AllowedAttributes.Add("href");
        sanitizer.AllowedAttributes.Add("src");
        sanitizer.AllowedAttributes.Add("alt");
        sanitizer.AllowedAttributes.Add("title");
        sanitizer.AllowedAttributes.Add("role");
        sanitizer.AllowedAttributes.Add("width");
        sanitizer.AllowedAttributes.Add("height");
        sanitizer.AllowedAttributes.Add("style");
        sanitizer.AllowedAttributes.Add("colspan");
        sanitizer.AllowedAttributes.Add("rowspan");
        sanitizer.AllowedAttributes.Add("align");
        sanitizer.AllowedAttributes.Add("type");
        sanitizer.AllowedAttributes.Add("checked");
        sanitizer.AllowedAttributes.Add("disabled");
        sanitizer.AllowedAttributes.Add("data-emoji-code");
        sanitizer.AllowedAttributes.Add("start");

        return sanitizer;
    }
}
