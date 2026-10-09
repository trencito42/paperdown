using Paperdown.Core.Models;

namespace Paperdown.Core.Interfaces;

public interface IMarkdownProcessor
{
    string ConvertToHtml(string markdown, DocumentSettings settings);
}

public interface IEmojiResolver
{
    string ProcessEmojisInHtml(string html);
    IReadOnlyList<string> GetUnresolvedEmojiGraphemes(string html);
}

public interface IDocumentRenderer
{
    string RenderCompleteHtmlDocument(string markdown, DocumentSettings settings);
}

public interface IPdfExportService
{
    Task<ExportResult> ExportHtmlToPdfAsync(
        string htmlContent,
        string outputFilePath,
        PageSettings pageSettings,
        CancellationToken cancellationToken = default);
}

public interface IAppPreferencesStore
{
    AppPreferences Load();
    void Save(AppPreferences preferences);
}
