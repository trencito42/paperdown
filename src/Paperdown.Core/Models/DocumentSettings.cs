namespace Paperdown.Core.Models;

public sealed class DocumentSettings
{
    public DocumentTheme Theme { get; set; } = DocumentTheme.Minimal;
    public PageSettings Page { get; set; } = new();
    public BrandingSettings Branding { get; set; } = new();
    public bool SanitizeHtml { get; set; } = true;
    public bool EnableEmojiProcessing { get; set; } = true;
    public string? BaseDirectoryForRelativeAssets { get; set; }
}
