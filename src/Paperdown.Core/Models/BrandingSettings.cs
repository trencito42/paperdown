namespace Paperdown.Core.Models;

public sealed class BrandingSettings
{
    public bool Enabled { get; set; }

    public string CompanyName { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string DocumentDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
    public string FooterText { get; set; } = string.Empty;
    public string WatermarkText { get; set; } = string.Empty;

    public bool ShowCoverPage { get; set; }
    public bool ShowPageNumbers { get; set; }
    public LogoAlignment LogoAlignment { get; set; } = LogoAlignment.Left;
    public int LogoWidthPx { get; set; } = 180;

    public string? LogoFilePath { get; set; }
    public string? LogoDataUri { get; set; }

    public string AccentColorHex { get; set; } = "#2563eb";
    public string BodyFont { get; set; } = "Segoe UI, system-ui, sans-serif";
    public string HeadingFont { get; set; } = "Segoe UI, system-ui, sans-serif";
    public double BaseFontSizePt { get; set; } = 11;
    public double LineSpacing { get; set; } = 1.55;
}
