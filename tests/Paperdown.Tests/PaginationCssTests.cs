using System.Reflection;
using Paperdown.Rendering;

namespace Paperdown.Tests;

public class PaginationCssTests
{
    [Fact]
    public void BasePrintCss_PreventsOrphanHeadingsAndRepeatingTableHeaders()
    {
        var assembly = typeof(HtmlDocumentRenderer).Assembly;
        using var stream = assembly.GetManifestResourceStream("Paperdown.Rendering.Themes.Styles.base-print.css");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var css = reader.ReadToEnd();

        Assert.Contains("page-break-after: avoid", css, StringComparison.Ordinal);
        Assert.Contains("table-header-group", css, StringComparison.Ordinal);
        Assert.Contains("text-align: left", css, StringComparison.Ordinal);
    }
}
