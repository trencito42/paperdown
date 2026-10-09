using Paperdown.Core.Models;
using Paperdown.Rendering;

namespace Paperdown.Tests;

public class PreviewHtmlHelperTests
{
    [Fact]
    public void WrapForScreenPreview_uses_page_dimensions_from_settings()
    {
        var settings = new DocumentSettings
        {
            Page = new PageSettings
            {
                PageSize = PageSize.Letter,
                Orientation = PageOrientation.Landscape,
                MarginTopMm = 12,
                MarginRightMm = 10,
                MarginBottomMm = 14,
                MarginLeftMm = 11,
            },
        };

        var html = PreviewHtmlHelper.WrapForScreenPreview("<html><head></head><body></body></html>", settings, 1.0);
        Assert.Contains("279.4mm", html);
        Assert.Contains("215.9mm", html);
        Assert.Contains("12mm", html);
    }
}
