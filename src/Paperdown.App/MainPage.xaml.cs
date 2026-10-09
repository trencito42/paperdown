using System.Text;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using Paperdown.Core.Models;
using Paperdown.Core.Services;
using Paperdown.Markdown;
using Paperdown.Pdf;
using Paperdown.Rendering;
using Paperdown_App.Dialogs;
using Paperdown_App.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace Paperdown_App;

public sealed partial class MainPage : Page
{
    private readonly MainViewModel _viewModel = new();
    private readonly HtmlDocumentRenderer _renderer = new(new MarkdownProcessor());
    private readonly WebView2PdfExportService _pdfExportService = new();
    private readonly AppPreferencesStore _preferencesStore = new();
    private readonly BrandingDialog _brandingDialog;

    private readonly DispatcherTimer _previewDebounce = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly DispatcherTimer _draftTimer = new() { Interval = TimeSpan.FromSeconds(30) };

    private string? _currentFilePath;
    private bool _webViewReady;
    private bool _pdfEngineReady;
    private string _lastDiagnostics = string.Empty;
    private AppPreferences _preferences = new();
    private CancellationTokenSource? _exportCts;
    private CancellationTokenSource? _previewCts;
    private int _previewGeneration;
    private DocumentViewMode _viewMode = DocumentViewMode.Split;
    private static readonly string PreviewHtmlPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Paperdown",
        "preview",
        "current.html");
    private double _savedEditorWidth = 420;
    private bool _splitterDragging;
    private double _splitterStartX;
    private double _splitterStartWidth;

    public MainPage()
    {
        InitializeComponent();
        _brandingDialog = new BrandingDialog(App.Window);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        _previewDebounce.Tick += (_, _) =>
        {
            _previewDebounce.Stop();
            _ = UpdatePreviewAsync();
        };

        _draftTimer.Tick += (_, _) => SaveDraft();

        MarkdownEditor.TextChanged += (_, _) =>
        {
            _viewModel.IsDirty = true;
            UpdateTitle();
            UpdateWordCount();
            UpdateLineNumbers();
            _previewDebounce.Stop();
            _previewDebounce.Start();
        };

        OpenButton.Click += async (_, _) => await OpenFileAsync();
        SaveButton.Click += async (_, _) => await SaveFileAsync();
        ExportPdfButton.Click += async (_, _) => await ExportPdfAsync();
        PageSettingsButton.Click += async (_, _) => await ShowPageSettingsAsync();
        BrandingButton.Click += async (_, _) => await ShowBrandingDialogAsync();
        FindButton.Click += async (_, _) => await ShowFindReplaceAsync();
        ZoomInButton.Click += (_, _) => AdjustZoom(0.1);
        ZoomOutButton.Click += (_, _) => AdjustZoom(-0.1);
        FitWidthButton.Click += (_, _) => SetPreviewZoom(1.0);
        FitPageButton.Click += (_, _) => SetPreviewZoom(0.85);
        ToggleEditorButton.Click += (_, _) => ToggleEditor();
        WordWrapButton.Click += (_, _) => ToggleWordWrap();

        PreviewInfoRetryButton.Click += async (_, _) => await RetryWebViewAsync();
        PreviewFallbackRetryButton.Click += async (_, _) => await RetryWebViewAsync();
        CopyDiagnosticsButton.Click += async (_, _) => await CopyDiagnosticsAsync();

        ThemeComboBox.SelectionChanged += (_, _) =>
        {
            if (ThemeComboBox.SelectedItem is ComboBoxItem item &&
                Enum.TryParse<DocumentTheme>(item.Tag?.ToString(), out var theme))
            {
                _viewModel.SelectedTheme = theme;
                _viewModel.Settings.Theme = theme;
                _ = UpdatePreviewAsync();
            }
        };

        SplitterBar.PointerPressed += OnSplitterPointerPressed;
        SplitterBar.PointerMoved += OnSplitterPointerMoved;
        SplitterBar.PointerReleased += OnSplitterPointerReleased;
        SplitterBar.PointerCanceled += OnSplitterPointerReleased;

        PreviewWebView.Loaded += async (_, _) => await InitializeWebViewAsync();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _preferences = _preferencesStore.Load();
        _viewModel.PreviewZoom = _preferences.PreviewZoom;
        _viewModel.WordWrap = _preferences.EditorWordWrap;
        _savedEditorWidth = Math.Clamp(_preferences.EditorPaneWidth, 240, 900);
        MarkdownEditor.TextWrapping = _viewModel.WordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;

        if (_preferences.LastDocumentSettings is not null)
        {
            CopySettings(_preferences.LastDocumentSettings, _viewModel.Settings);
            _viewModel.SelectedTheme = _viewModel.Settings.Theme;
        }

        PopulateThemeCombo();
        TryRestoreDraft();

        if (string.IsNullOrWhiteSpace(MarkdownEditor.Text))
        {
            MarkdownEditor.Text = GetWelcomeMarkdown();
        }

        EditorColumn.Width = new GridLength(_savedEditorWidth);
        if (_preferences.EditorCollapsed)
        {
            ApplyViewMode(DocumentViewMode.PreviewOnly);
        }
        else
        {
            ApplyViewMode(DocumentViewMode.Split);
        }

        LineNumberPanel.Visibility = _preferences.ShowLineNumbers ? Visibility.Visible : Visibility.Collapsed;
        UpdateZoomLabel();
        UpdateTitle();
        UpdateWordCount();
        UpdateLineNumbers();
        SetStatus("Ready");

        RegisterAccelerators();
        AllowDrop = true;
        DragOver += OnDragOver;
        Drop += OnDrop;
        _draftTimer.Start();

        await ProbePdfEngineAsync();
        if (_webViewReady)
        {
            await UpdatePreviewAsync();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _draftTimer.Stop();
        _exportCts?.Cancel();
        _previewCts?.Cancel();
    }

    private void RegisterAccelerators()
    {
        AddAccelerator(VirtualKey.S, VirtualKeyModifiers.Control, async () => await SaveFileAsync());
        AddAccelerator(VirtualKey.O, VirtualKeyModifiers.Control, async () => await OpenFileAsync());
        AddAccelerator(VirtualKey.E, VirtualKeyModifiers.Control, async () => await ExportPdfAsync());
        AddAccelerator(VirtualKey.E, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, () => ToggleEditor());
        AddAccelerator(VirtualKey.F, VirtualKeyModifiers.Control, async () => await ShowFindReplaceAsync());
    }

    private void AddAccelerator(VirtualKey key, VirtualKeyModifiers modifiers, Func<Task> action)
    {
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        accelerator.Invoked += async (_, _) => await action();
        KeyboardAccelerators.Add(accelerator);
    }

    private void AddAccelerator(VirtualKey key, VirtualKeyModifiers modifiers, Action action)
    {
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        accelerator.Invoked += (_, _) => action();
        KeyboardAccelerators.Add(accelerator);
    }

    private void PopulateThemeCombo()
    {
        ThemeComboBox.Items.Clear();
        foreach (var theme in Enum.GetValues<DocumentTheme>())
        {
            ThemeComboBox.Items.Add(new ComboBoxItem
            {
                Content = theme.ToString(),
                Tag = theme.ToString(),
            });
        }

        ThemeComboBox.SelectedIndex = (int)_viewModel.SelectedTheme;
    }

    private async Task ProbePdfEngineAsync()
    {
        var probe = WebView2EnvironmentHelper.ProbeRuntime();
        _pdfEngineReady = probe.RuntimeInstalled && probe.LoaderPresent;
        ExportPdfButton.IsEnabled = _pdfEngineReady;
        if (!_pdfEngineReady)
        {
            ToolTipService.SetToolTip(ExportPdfButton, "PDF export requires WebView2 (install runtime or fix deployment).");
        }
    }

    private async Task InitializeWebViewAsync()
    {
        if (_webViewReady)
        {
            return;
        }

        try
        {
            WebView2EnvironmentHelper.PrepareNativeLoader();
            var probe = WebView2EnvironmentHelper.ProbeRuntime();
            _lastDiagnostics = WebView2EnvironmentHelper.BuildDiagnosticsText(null, probe);

            await PreviewWebView.EnsureCoreWebView2Async();
            var core = PreviewWebView.CoreWebView2;
            core.Settings.IsScriptEnabled = false;
            core.Settings.AreDefaultScriptDialogsEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.NavigationStarting += (_, args) =>
            {
                if (args.Uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    args.Uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    args.Cancel = true;
                }
            };

            _webViewReady = true;
            _pdfEngineReady = true;
            PreviewWebView.Visibility = Visibility.Visible;
            PreviewFallbackPanel.Visibility = Visibility.Collapsed;
            PreviewInfoBar.IsOpen = false;
            ExportPdfButton.IsEnabled = true;
            await UpdatePreviewAsync();
        }
        catch (Exception ex)
        {
            var failed = WebView2EnvironmentResult.Failed(ex, WebView2EnvironmentHelper.ProbeRuntime());
            _lastDiagnostics = failed.DiagnosticsText;
            ShowPreviewFailure(failed.UserMessage, _lastDiagnostics);
            await ProbePdfEngineAsync();
        }
    }

    private async Task RetryWebViewAsync()
    {
        _webViewReady = false;
        PreviewInfoBar.IsOpen = false;
        SetStatus("Retrying preview…");
        await InitializeWebViewAsync();
    }

    private void ShowPreviewFailure(WebView2UserMessage? message, string diagnostics)
    {
        _lastDiagnostics = diagnostics;
        var title = message?.Title ?? "Preview unavailable";
        var summary = message?.Summary ?? "The document preview could not start.";

        PreviewWebView.Visibility = Visibility.Collapsed;
        PreviewFallbackPanel.Visibility = Visibility.Visible;
        PreviewFallbackTitle.Text = title;
        PreviewFallbackSummary.Text = summary;
        PreviewRuntimeLink.Visibility = message?.IsRuntimeMissing == true ? Visibility.Visible : Visibility.Collapsed;

        PreviewInfoBar.Title = title;
        PreviewInfoBar.Message = summary;
        PreviewInfoBar.IsOpen = true;

        SetStatus("Preview unavailable — editing still works");
    }

    private async Task CopyDiagnosticsAsync()
    {
        if (string.IsNullOrWhiteSpace(_lastDiagnostics))
        {
            _lastDiagnostics = WebView2EnvironmentHelper.BuildDiagnosticsText(null, WebView2EnvironmentHelper.ProbeRuntime());
        }

        var package = new DataPackage();
        package.SetText(_lastDiagnostics);
        Clipboard.SetContent(package);
        SetStatus("Diagnostics copied");
        await Task.CompletedTask;
    }

    private async Task UpdatePreviewAsync()
    {
        if (!_webViewReady || _viewMode == DocumentViewMode.EditorOnly)
        {
            return;
        }

        _previewCts?.Cancel();
        _previewCts = new CancellationTokenSource();
        var token = _previewCts.Token;
        var generation = Interlocked.Increment(ref _previewGeneration);

        try
        {
            PreviewProgress.IsActive = true;
            _viewModel.IsPreviewLoading = true;
            _viewModel.Settings.Theme = _viewModel.SelectedTheme;

            var exportHtml = _renderer.RenderCompleteHtmlDocument(MarkdownEditor.Text, _viewModel.Settings);
            var previewHtml = PreviewHtmlHelper.WrapForScreenPreview(exportHtml, _viewModel.Settings, _viewModel.PreviewZoom);
            _viewModel.EstimatedPageCount = EstimatePagesFromHtml(exportHtml);

            Directory.CreateDirectory(Path.GetDirectoryName(PreviewHtmlPath)!);
            await File.WriteAllTextAsync(PreviewHtmlPath, previewHtml, Encoding.UTF8, token);
            if (generation != _previewGeneration || token.IsCancellationRequested)
            {
                return;
            }

            PreviewWebView.CoreWebView2.Navigate(new Uri(PreviewHtmlPath).AbsoluteUri);

            PageCountText.Text = $"~{_viewModel.EstimatedPageCount} page(s)";
            SetStatus(_viewModel.IsDirty ? "Unsaved changes" : "Ready");
        }
        catch (Exception ex)
        {
            SetStatus("Preview update failed");
            ShowPreviewFailure(
                new WebView2UserMessage("Preview error", ex.Message, false, true),
                WebView2EnvironmentHelper.BuildDiagnosticsText(ex, WebView2EnvironmentHelper.ProbeRuntime()));
        }
        finally
        {
            PreviewProgress.IsActive = false;
            _viewModel.IsPreviewLoading = false;
        }
    }

    private static int EstimatePagesFromHtml(string html) =>
        Math.Max(1, (int)Math.Ceiling(html.Length / 6000.0));

    private async Task OpenFileAsync()
    {
        if (_viewModel.IsDirty && !await ConfirmDiscardAsync())
        {
            return;
        }

        var picker = new FileOpenPicker();
        InitializePicker(picker);
        picker.FileTypeFilter.Add(".md");
        picker.FileTypeFilter.Add(".markdown");

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        var text = await FileIO.ReadTextAsync(file);
        MarkdownEditor.Text = text;
        _currentFilePath = file.Path;
        _viewModel.IsDirty = false;
        _viewModel.Settings.BaseDirectoryForRelativeAssets = Path.GetDirectoryName(file.Path);
        RememberRecent(file.Path);
        UpdateTitle();
        UpdateWordCount();
        await UpdatePreviewAsync();
    }

    private async Task SaveFileAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
        {
            await SaveFileAsAsync();
            return;
        }

        await File.WriteAllTextAsync(_currentFilePath, MarkdownEditor.Text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        _viewModel.IsDirty = false;
        UpdateTitle();
        SetStatus("Saved");
        PersistDocumentSettings();
    }

    private async Task SaveFileAsAsync()
    {
        var picker = new FileSavePicker();
        InitializePicker(picker);
        picker.FileTypeChoices.Add("Markdown", [".md"]);
        picker.SuggestedFileName = Path.GetFileName(_currentFilePath ?? "document.md");

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        await FileIO.WriteTextAsync(file, MarkdownEditor.Text);
        _currentFilePath = file.Path;
        _viewModel.IsDirty = false;
        _viewModel.Settings.BaseDirectoryForRelativeAssets = Path.GetDirectoryName(file.Path);
        RememberRecent(file.Path);
        UpdateTitle();
        PersistDocumentSettings();
    }

    private async Task ExportPdfAsync()
    {
        if (!_pdfEngineReady)
        {
            await ShowMessageAsync("PDF export unavailable", "Install the WebView2 Runtime or use Retry on the preview panel, then export again.");
            return;
        }

        var picker = new FileSavePicker();
        InitializePicker(picker);
        picker.FileTypeChoices.Add("PDF", [".pdf"]);
        var suggested = Path.GetFileNameWithoutExtension(_currentFilePath ?? "document") + ".pdf";
        picker.SuggestedFileName = suggested;

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        if (File.Exists(file.Path))
        {
            var confirm = new ContentDialog
            {
                Title = "Replace existing PDF?",
                Content = $"A file already exists at {file.Name}. Replace it?",
                PrimaryButtonText = "Replace",
                CloseButtonText = "Cancel",
                XamlRoot = XamlRoot,
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        _exportCts?.Cancel();
        _exportCts = new CancellationTokenSource();
        ExportPdfButton.IsEnabled = false;
        SetStatus("Exporting PDF…");

        try
        {
            var html = _renderer.RenderCompleteHtmlDocument(MarkdownEditor.Text, _viewModel.Settings);
            var result = await _pdfExportService.ExportHtmlToPdfAsync(
                html,
                file.Path,
                _viewModel.Settings.Page,
                _exportCts.Token);

            if (!result.Success)
            {
                await ShowMessageAsync("Export failed", result.ErrorMessage ?? "PDF export failed.");
                return;
            }

            _preferences.LastExportDirectory = Path.GetDirectoryName(file.Path);
            _preferencesStore.Save(_preferences);

            var dialog = new ContentDialog
            {
                Title = "PDF exported",
                Content = $"{file.Name} ({result.PageCount} page(s))",
                PrimaryButtonText = "Open PDF",
                SecondaryButtonText = "Open folder",
                CloseButtonText = "Close",
                XamlRoot = XamlRoot,
            };

            var choice = await dialog.ShowAsync();
            if (choice == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(new Uri(file.Path));
            }
            else if (choice == ContentDialogResult.Secondary)
            {
                var folder = Path.GetDirectoryName(file.Path);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    await Launcher.LaunchUriAsync(new Uri(folder));
                }
            }

            SetStatus("Export complete");
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Export failed", ex.Message);
        }
        finally
        {
            ExportPdfButton.IsEnabled = _pdfEngineReady;
        }
    }

    private async Task ShowPageSettingsAsync()
    {
        var updated = await PageSettingsDialog.ShowAsync(_viewModel.Settings.Page, XamlRoot);
        if (updated is null)
        {
            return;
        }

        _viewModel.Settings.Page = updated;
        PersistDocumentSettings();
        await UpdatePreviewAsync();
    }

    private async Task ShowBrandingDialogAsync()
    {
        var updated = await _brandingDialog.ShowAsync(_viewModel.Settings.Branding, XamlRoot);
        if (updated is null)
        {
            return;
        }

        _viewModel.Settings.Branding = updated;
        PersistDocumentSettings();
        await UpdatePreviewAsync();
    }

    private async Task ShowFindReplaceAsync()
    {
        var find = new TextBox { Header = "Find", PlaceholderText = "Search text" };
        var replace = new TextBox { Header = "Replace with", PlaceholderText = "Replacement" };
        var panel = new StackPanel { Spacing = 8, MinWidth = 360 };
        panel.Children.Add(find);
        panel.Children.Add(replace);

        var dialog = new ContentDialog
        {
            Title = "Find and replace",
            Content = panel,
            PrimaryButtonText = "Replace all",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary || string.IsNullOrEmpty(find.Text))
        {
            return;
        }

        MarkdownEditor.Text = MarkdownEditor.Text.Replace(find.Text, replace.Text, StringComparison.Ordinal);
        _viewModel.IsDirty = true;
        await UpdatePreviewAsync();
    }

    private void AdjustZoom(double delta) => SetPreviewZoom(_viewModel.PreviewZoom + delta);

    private void SetPreviewZoom(double zoom)
    {
        _viewModel.PreviewZoom = Math.Clamp(zoom, 0.5, 2.0);
        _preferences.PreviewZoom = _viewModel.PreviewZoom;
        _preferencesStore.Save(_preferences);
        UpdateZoomLabel();
        _ = UpdatePreviewAsync();
    }

    private void UpdateZoomLabel() => ZoomLabel.Text = $"{_viewModel.PreviewZoom * 100:0}%";

    private void ToggleEditor()
    {
        if (_viewMode == DocumentViewMode.PreviewOnly)
        {
            ApplyViewMode(DocumentViewMode.Split);
        }
        else
        {
            ApplyViewMode(DocumentViewMode.PreviewOnly);
        }
    }

    private void HideEditorPane() => ApplyViewMode(DocumentViewMode.PreviewOnly);

    private void ShowEditorPane() => ApplyViewMode(DocumentViewMode.Split);

    private void ApplyViewMode(DocumentViewMode mode)
    {
        if (mode == DocumentViewMode.PreviewOnly)
        {
            _savedEditorWidth = EditorColumn.ActualWidth > 0 ? EditorColumn.ActualWidth : _savedEditorWidth;
        }

        _viewMode = mode;
        _viewModel.EditorVisible = mode is DocumentViewMode.Split or DocumentViewMode.EditorOnly;

        switch (mode)
        {
            case DocumentViewMode.Split:
                EditorColumn.MinWidth = 240;
                EditorColumn.Width = new GridLength(Math.Clamp(_savedEditorWidth, 240, 900));
                EditorPanel.Visibility = Visibility.Visible;
                PreviewPanel.Visibility = Visibility.Visible;
                PreviewColumn.MinWidth = 280;
                PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
                SplitterBar.Visibility = Visibility.Visible;
                _preferences.EditorCollapsed = false;
                break;
            case DocumentViewMode.PreviewOnly:
                EditorColumn.Width = new GridLength(0);
                EditorColumn.MinWidth = 0;
                EditorPanel.Visibility = Visibility.Collapsed;
                PreviewPanel.Visibility = Visibility.Visible;
                PreviewColumn.MinWidth = 280;
                PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
                SplitterBar.Visibility = Visibility.Collapsed;
                _preferences.EditorCollapsed = true;
                _preferences.EditorPaneWidth = _savedEditorWidth;
                _ = UpdatePreviewAsync();
                break;
            case DocumentViewMode.EditorOnly:
                EditorColumn.MinWidth = 240;
                EditorColumn.Width = new GridLength(1, GridUnitType.Star);
                EditorPanel.Visibility = Visibility.Visible;
                PreviewPanel.Visibility = Visibility.Collapsed;
                PreviewColumn.Width = new GridLength(0);
                PreviewColumn.MinWidth = 0;
                SplitterBar.Visibility = Visibility.Collapsed;
                _preferences.EditorCollapsed = false;
                break;
        }

        _preferencesStore.Save(_preferences);
        UpdateEditorToggleUi();
        if (mode == DocumentViewMode.Split)
        {
            MarkdownEditor.Focus(FocusState.Programmatic);
        }
    }

    private void UpdateEditorToggleUi()
    {
        var previewOnly = _viewMode == DocumentViewMode.PreviewOnly;
        ToggleEditorButton.Label = previewOnly ? "Show editor" : "Hide editor";
        ToolTipService.SetToolTip(
            ToggleEditorButton,
            previewOnly ? "Show editor (Ctrl+Shift+E)" : "Hide editor — preview only (Ctrl+Shift+E)");
        ToggleEditorButton.Icon = new SymbolIcon(previewOnly ? Symbol.Edit : Symbol.FullScreen);
    }

    private enum DocumentViewMode
    {
        Split,
        PreviewOnly,
        EditorOnly,
    }

    private void ToggleWordWrap()
    {
        _viewModel.WordWrap = !_viewModel.WordWrap;
        MarkdownEditor.TextWrapping = _viewModel.WordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        _preferences.EditorWordWrap = _viewModel.WordWrap;
        _preferencesStore.Save(_preferences);
        WordWrapButton.Label = _viewModel.WordWrap ? "Wrap on" : "Wrap off";
    }

    private void OnSplitterPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!_viewModel.EditorVisible)
        {
            return;
        }

        _splitterDragging = true;
        _splitterStartX = e.GetCurrentPoint(EditorPreviewGrid).Position.X;
        _splitterStartWidth = EditorColumn.ActualWidth;
        SplitterBar.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnSplitterPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_splitterDragging)
        {
            return;
        }

        var x = e.GetCurrentPoint(EditorPreviewGrid).Position.X;
        var delta = x - _splitterStartX;
        var width = Math.Clamp(_splitterStartWidth + delta, 240, EditorPreviewGrid.ActualWidth - 300);
        EditorColumn.Width = new GridLength(width);
        _savedEditorWidth = width;
        e.Handled = true;
    }

    private void OnSplitterPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_splitterDragging)
        {
            return;
        }

        _splitterDragging = false;
        SplitterBar.ReleasePointerCapture(e.Pointer);
        _preferences.EditorPaneWidth = _savedEditorWidth;
        _preferencesStore.Save(_preferences);
        e.Handled = true;
    }

    private void UpdateTitle()
    {
        var name = string.IsNullOrWhiteSpace(_currentFilePath) ? "Untitled.md" : Path.GetFileName(_currentFilePath);
        _viewModel.DocumentDisplayName = _viewModel.IsDirty ? $"{name} *" : name;
        if (App.Window is MainWindow window)
        {
            window.SetDocumentStatus(_viewModel.DocumentDisplayName);
        }
    }

    private void UpdateWordCount()
    {
        var words = string.IsNullOrWhiteSpace(MarkdownEditor.Text)
            ? 0
            : MarkdownEditor.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        WordCountText.Text = $"{words} words";
    }

    private void UpdateLineNumbers()
    {
        if (LineNumberPanel.Visibility != Visibility.Visible)
        {
            return;
        }

        var lines = string.IsNullOrEmpty(MarkdownEditor.Text) ? 1 : MarkdownEditor.Text.Count(c => c == '\n') + 1;
        LineNumberText.Text = string.Join('\n', Enumerable.Range(1, lines));
    }

    private void SetStatus(string message) => StatusText.Text = message;

    private void PersistDocumentSettings()
    {
        CopySettings(_viewModel.Settings, _preferences.LastDocumentSettings);
        _preferencesStore.Save(_preferences);
    }

    private static void CopySettings(DocumentSettings source, DocumentSettings target)
    {
        target.Theme = source.Theme;
        target.Page = new PageSettings
        {
            PageSize = source.Page.PageSize,
            Orientation = source.Page.Orientation,
            MarginTopMm = source.Page.MarginTopMm,
            MarginRightMm = source.Page.MarginRightMm,
            MarginBottomMm = source.Page.MarginBottomMm,
            MarginLeftMm = source.Page.MarginLeftMm,
            PrintBackgroundGraphics = source.Page.PrintBackgroundGraphics,
        };
        target.Branding = new BrandingSettings
        {
            Enabled = source.Branding.Enabled,
            CompanyName = source.Branding.CompanyName,
            DocumentTitle = source.Branding.DocumentTitle,
            Subtitle = source.Branding.Subtitle,
            Author = source.Branding.Author,
            DocumentDate = source.Branding.DocumentDate,
            FooterText = source.Branding.FooterText,
            WatermarkText = source.Branding.WatermarkText,
            ShowCoverPage = source.Branding.ShowCoverPage,
            ShowPageNumbers = source.Branding.ShowPageNumbers,
            LogoAlignment = source.Branding.LogoAlignment,
            LogoWidthPx = source.Branding.LogoWidthPx,
            LogoFilePath = source.Branding.LogoFilePath,
            LogoDataUri = source.Branding.LogoDataUri,
            AccentColorHex = source.Branding.AccentColorHex,
            BodyFont = source.Branding.BodyFont,
            HeadingFont = source.Branding.HeadingFont,
            BaseFontSizePt = source.Branding.BaseFontSizePt,
            LineSpacing = source.Branding.LineSpacing,
        };
        target.BaseDirectoryForRelativeAssets = source.BaseDirectoryForRelativeAssets;
        target.EnableEmojiProcessing = source.EnableEmojiProcessing;
    }

    private void RememberRecent(string path)
    {
        _preferences.RecentFiles.Remove(path);
        _preferences.RecentFiles.Insert(0, path);
        _preferences.RecentFiles = _preferences.RecentFiles.Take(12).ToList();
        _preferencesStore.Save(_preferences);
    }

    private void SaveDraft()
    {
        if (!_viewModel.IsDirty)
        {
            return;
        }

        var draftPath = GetDraftPath();
        Directory.CreateDirectory(Path.GetDirectoryName(draftPath)!);
        File.WriteAllText(draftPath, MarkdownEditor.Text, Encoding.UTF8);
    }

    private void TryRestoreDraft()
    {
        var draftPath = GetDraftPath();
        if (!File.Exists(draftPath) || !string.IsNullOrWhiteSpace(_currentFilePath))
        {
            return;
        }

        try
        {
            var draft = File.ReadAllText(draftPath);
            if (!string.IsNullOrWhiteSpace(draft))
            {
                MarkdownEditor.Text = draft;
                _viewModel.IsDirty = true;
            }
        }
        catch
        {
            // ignore draft restore failures
        }
    }

    private static string GetDraftPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Paperdown", "draft.md");

    private async Task<bool> ConfirmDiscardAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "Unsaved changes",
            Content = "Discard unsaved changes?",
            PrimaryButtonText = "Discard",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
        SetStatus(message);
    }

    private void InitializePicker(object picker)
    {
        var hwnd = WindowNative.GetWindowHandle(App.Window);
        InitializeWithWindow.Initialize(picker, hwnd);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        if (items.Count == 0 || items[0] is not StorageFile file)
        {
            return;
        }

        var ext = Path.GetExtension(file.Path).ToLowerInvariant();
        if (ext is not ".md" and not ".markdown")
        {
            return;
        }

        MarkdownEditor.Text = await FileIO.ReadTextAsync(file);
        _currentFilePath = file.Path;
        _viewModel.IsDirty = false;
        RememberRecent(file.Path);
        UpdateTitle();
        UpdateWordCount();
        await UpdatePreviewAsync();
    }

    private static string GetWelcomeMarkdown() =>
        """
        # Paperdown

        > **Beautiful Markdown. Perfect PDFs.**

        ## Emoji in PDF

        - 🔒 Restricted
        - 📘 Tutorial
        - ✅ 🚀 ❤️ 🔥 🎉 ⚠️ 🧑‍💻 🇷🇴

        ## Română

        Înțelepciune în București — diacritice **ăâîșț**.

        ```csharp
        var html = renderer.RenderCompleteHtmlDocument(markdown, settings);
        ```
        """;
}
