namespace Paperdown.Core.Models;

public sealed class ExportResult
{
    public bool Success { get; set; }
    public string? OutputPdfPath { get; set; }
    public string? ErrorMessage { get; set; }
    public int PageCount { get; set; }
    public TimeSpan ElapsedTime { get; set; }
}
