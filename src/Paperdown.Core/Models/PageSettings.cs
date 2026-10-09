namespace Paperdown.Core.Models;

public sealed class PageSettings
{
    public PageSize PageSize { get; set; } = PageSize.A4;
    public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;
    public double MarginTopMm { get; set; } = 20;
    public double MarginRightMm { get; set; } = 18;
    public double MarginBottomMm { get; set; } = 22;
    public double MarginLeftMm { get; set; } = 18;
    public bool PrintBackgroundGraphics { get; set; } = true;
}
