using CommunityToolkit.Mvvm.ComponentModel;
using Paperdown.Core.Models;

namespace Paperdown_App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _markdownText = string.Empty;

    [ObservableProperty]
    private string _documentDisplayName = "Untitled.md";

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isPreviewLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private int _estimatedPageCount = 1;

    [ObservableProperty]
    private double _previewZoom = 1.0;

    [ObservableProperty]
    private DocumentTheme _selectedTheme = DocumentTheme.Minimal;

    [ObservableProperty]
    private bool _wordWrap = true;

    [ObservableProperty]
    private bool _editorVisible = true;

    public DocumentSettings Settings { get; } = new();
}
