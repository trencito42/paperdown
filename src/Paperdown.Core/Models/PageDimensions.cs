namespace Paperdown.Core.Models;

public static class PageDimensions
{
    public static (double WidthMm, double HeightMm) GetPageSizeMm(PageSettings page)
    {
        var (w, h) = page.PageSize switch
        {
            PageSize.A5 => (148.0, 210.0),
            PageSize.Letter => (215.9, 279.4),
            PageSize.Legal => (215.9, 355.6),
            _ => (210.0, 297.0),
        };

        if (page.Orientation == PageOrientation.Landscape)
        {
            return (h, w);
        }

        return (w, h);
    }

    public static string ToCssPageSize(PageSettings page)
    {
        var name = page.PageSize switch
        {
            PageSize.A5 => "A5",
            PageSize.Letter => "letter",
            PageSize.Legal => "legal",
            _ => "A4",
        };

        var orientation = page.Orientation == PageOrientation.Landscape ? " landscape" : " portrait";
        return name + orientation;
    }
}
