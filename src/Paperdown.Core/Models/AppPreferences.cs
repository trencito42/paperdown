namespace Paperdown.Core.Models;

public sealed class AppPreferences
{
    public string? LastExportDirectory { get; set; }
    public List<string> RecentFiles { get; set; } = [];
    public DocumentSettings LastDocumentSettings { get; set; } = new();
    public double PreviewZoom { get; set; } = 1.0;
    public bool EditorWordWrap { get; set; } = true;
    public bool ShowLineNumbers { get; set; }
    public bool EditorCollapsed { get; set; }
    public double EditorPaneWidth { get; set; } = 420;
}
