using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using Paperdown.Core.Models;
using Paperdown.Core.Services;
using Paperdown.Markdown;
using Paperdown.Pdf;
using Paperdown.Rendering;
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
    private readonly BrandingPresetStore _brandingPresetStore = new();

    private readonly DispatcherTimer _previewDebounce = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly DispatcherTimer _draftTimer = new() { Interval = TimeSpan.FromSeconds(30) };

    private string? _currentFilePath;
    private bool _webViewReady;
    private AppPreferences _preferences = new();
    private CancellationTokenSource? _exportCts;

    public MainPage()
    {
        InitializeComponent();
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
        FitWidthButton.Click += (_, _) => _viewModel.PreviewZoom = 1.0;
        ToggleEditorButton.Click += (_, _) => ToggleEditor();

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
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _preferences = _preferencesStore.Load();
        _viewModel.PreviewZoom = _preferences.PreviewZoom;
        _viewModel.WordWrap = _preferences.EditorWordWrap;
        MarkdownEditor.TextWrapping = _viewModel.WordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;

        PopulateThemeCombo();
        await InitializeWebViewAsync();
        TryRestoreDraft();

        if (string.IsNullOrWhiteSpace(MarkdownEditor.Text))
        {
            MarkdownEditor.Text = GetWelcomeMarkdown();
        }

        UpdateZoomLabel();
        UpdateTitle();
        _ = UpdatePreviewAsync();

        var accelerator = new KeyboardAccelerator { Key = VirtualKey.S, Modifiers = VirtualKeyModifiers.Control };
        accelerator.Invoked += async (_, _) => await SaveFileAsync();
        KeyboardAccelerators.Add(accelerator);

        var openAccel = new KeyboardAccelerator { Key = VirtualKey.O, Modifiers = VirtualKeyModifiers.Control };
        openAccel.Invoked += async (_, _) => await OpenFileAsync();
        KeyboardAccelerators.Add(openAccel);

        var exportAccel = new KeyboardAccelerator { Key = VirtualKey.E, Modifiers = VirtualKeyModifiers.Control };
        exportAccel.Invoked += async (_, _) => await ExportPdfAsync();
        KeyboardAccelerators.Add(exportAccel);

        AllowDrop = true;
        DragOver += OnDragOver;
        Drop += OnDrop;
        _draftTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _draftTimer.Stop();
        _exportCts?.Cancel();
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

    private async Task InitializeWebViewAsync()
    {
        try
        {
            await PreviewWebView.EnsureCoreWebView2Async();
            PreviewWebView.CoreWebView2.Settings.IsScriptEnabled = false;
            PreviewWebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
            PreviewWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            PreviewWebView.CoreWebView2.NavigationStarting += (_, args) =>
            {
                if (args.Uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    args.Uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    if (!args.Uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        args.Cancel = true;
                    }
                }
            };
            _webViewReady = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"WebView2 runtime required: {ex.Message}";
        }
    }

    private async Task UpdatePreviewAsync()
    {
        if (!_webViewReady)
        {
            return;
        }

        try
        {
            PreviewProgress.IsActive = true;
            _viewModel.IsPreviewLoading = true;
            _viewModel.Settings.Theme = _viewModel.SelectedTheme;

            var exportHtml = _renderer.RenderCompleteHtmlDocument(MarkdownEditor.Text, _viewModel.Settings);
            var previewHtml = PreviewHtmlHelper.WrapForScreenPreview(exportHtml, _viewModel.PreviewZoom);
            _viewModel.EstimatedPageCount = EstimatePagesFromHtml(exportHtml);

            var temp = Path.Combine(Path.GetTempPath(), $"paperdown_preview_{Guid.NewGuid():N}.html");
            await File.WriteAllTextAsync(temp, previewHtml, Encoding.UTF8);
            PreviewWebView.CoreWebView2.Navigate(new Uri(temp).AbsoluteUri);

            PageCountText.Text = $"~{_viewModel.EstimatedPageCount} page(s)";
            StatusText.Text = _viewModel.IsDirty ? "Unsaved changes" : "Ready";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Preview error: {ex.Message}";
        }
        finally
        {
            PreviewProgress.IsActive = false;
            _viewModel.IsPreviewLoading = false;
        }
    }

    private static int EstimatePagesFromHtml(string html)
    {
        var length = html.Length;
        return Math.Max(1, (int)Math.Ceiling(length / 6000.0));
    }

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
        StatusText.Text = "Saved";
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
    }

    private async Task ExportPdfAsync()
    {
        var picker = new FileSavePicker();
        InitializePicker(picker);
        picker.FileTypeChoices.Add("PDF", [".pdf"]);
        var suggested = Path.GetFileNameWithoutExtension(_currentFilePath ?? "document") + ".pdf";
        picker.SuggestedFileName = suggested;
        if (!string.IsNullOrWhiteSpace(_preferences.LastExportDirectory) && Directory.Exists(_preferences.LastExportDirectory))
        {
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        }

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
        StatusText.Text = "Exporting PDF…";

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
                await ShowErrorAsync(result.ErrorMessage ?? "PDF export failed.");
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

            StatusText.Text = "Export complete";
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
        finally
        {
            ExportPdfButton.IsEnabled = true;
        }
    }

    private async Task ShowPageSettingsAsync()
    {
        var page = _viewModel.Settings.Page;
        var sizeBox = new ComboBox { Header = "Page size", HorizontalAlignment = HorizontalAlignment.Stretch };
        sizeBox.Items.Add(new ComboBoxItem { Content = "A4", Tag = PageSize.A4 });
        sizeBox.Items.Add(new ComboBoxItem { Content = "Letter", Tag = PageSize.Letter });
        sizeBox.SelectedIndex = page.PageSize == PageSize.Letter ? 1 : 0;

        var orientationBox = new ComboBox { Header = "Orientation", HorizontalAlignment = HorizontalAlignment.Stretch };
        orientationBox.Items.Add(new ComboBoxItem { Content = "Portrait", Tag = PageOrientation.Portrait });
        orientationBox.Items.Add(new ComboBoxItem { Content = "Landscape", Tag = PageOrientation.Landscape });
        orientationBox.SelectedIndex = page.Orientation == PageOrientation.Landscape ? 1 : 0;

        var marginBox = new NumberBox { Header = "Margins (mm)", Value = page.MarginTopMm, Minimum = 5, Maximum = 40, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };

        var panel = new StackPanel { Spacing = 12, MinWidth = 320 };
        panel.Children.Add(sizeBox);
        panel.Children.Add(orientationBox);
        panel.Children.Add(marginBox);

        var dialog = new ContentDialog
        {
            Title = "Page settings",
            Content = panel,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (sizeBox.SelectedItem is ComboBoxItem sizeItem && sizeItem.Tag is PageSize ps)
        {
            page.PageSize = ps;
        }

        if (orientationBox.SelectedItem is ComboBoxItem orientItem && orientItem.Tag is PageOrientation po)
        {
            page.Orientation = po;
        }

        var margin = marginBox.Value;
        page.MarginTopMm = margin;
        page.MarginRightMm = margin;
        page.MarginBottomMm = margin;
        page.MarginLeftMm = margin;
        await UpdatePreviewAsync();
    }

    private async Task ShowBrandingDialogAsync()
    {
        var branding = _viewModel.Settings.Branding;
        var enabled = new CheckBox { Content = "Enable branding", IsChecked = branding.Enabled };
        var title = new TextBox { Header = "Document title", Text = branding.DocumentTitle };
        var company = new TextBox { Header = "Company / project", Text = branding.CompanyName };
        var author = new TextBox { Header = "Author", Text = branding.Author };
        var cover = new CheckBox { Content = "Cover page", IsChecked = branding.ShowCoverPage };
        var pageNumbers = new CheckBox { Content = "Page numbers", IsChecked = branding.ShowPageNumbers };
        var logoButton = new Button { Content = "Upload logo (PNG/JPEG)" };

        logoButton.Click += async (_, _) =>
        {
            var picker = new FileOpenPicker();
            InitializePicker(picker);
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            var bytes = await File.ReadAllBytesAsync(file.Path);
            var mime = file.FileType.ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                _ => "application/octet-stream",
            };
            branding.LogoDataUri = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            branding.LogoFilePath = file.Path;
            logoButton.Content = $"Logo: {file.Name}";
        };

        var panel = new StackPanel { Spacing = 10, MinWidth = 380 };
        panel.Children.Add(enabled);
        panel.Children.Add(title);
        panel.Children.Add(company);
        panel.Children.Add(author);
        panel.Children.Add(logoButton);
        panel.Children.Add(cover);
        panel.Children.Add(pageNumbers);

        var dialog = new ContentDialog
        {
            Title = "Branding",
            Content = panel,
            PrimaryButtonText = "Apply",
            SecondaryButtonText = "Save preset",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.None)
        {
            return;
        }

        branding.Enabled = enabled.IsChecked == true;
        branding.DocumentTitle = title.Text;
        branding.CompanyName = company.Text;
        branding.Author = author.Text;
        branding.ShowCoverPage = cover.IsChecked == true;
        branding.ShowPageNumbers = pageNumbers.IsChecked == true;

        if (result == ContentDialogResult.Secondary)
        {
            var nameBox = new TextBox { Header = "Preset name", PlaceholderText = "Corporate default" };
            var saveDialog = new ContentDialog
            {
                Title = "Save branding preset",
                Content = nameBox,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                XamlRoot = XamlRoot,
            };
            if (await saveDialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(nameBox.Text))
            {
                _brandingPresetStore.Save(nameBox.Text.Trim(), branding);
            }
        }

        if (result == ContentDialogResult.Primary || result == ContentDialogResult.Secondary)
        {
            await UpdatePreviewAsync();
        }
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

    private void AdjustZoom(double delta)
    {
        _viewModel.PreviewZoom = Math.Clamp(_viewModel.PreviewZoom + delta, 0.5, 2.0);
        _preferences.PreviewZoom = _viewModel.PreviewZoom;
        _preferencesStore.Save(_preferences);
        UpdateZoomLabel();
        _ = UpdatePreviewAsync();
    }

    private void UpdateZoomLabel() => ZoomLabel.Text = $"{_viewModel.PreviewZoom * 100:0}%";

    private void ToggleEditor()
    {
        _viewModel.EditorVisible = !_viewModel.EditorVisible;
        EditorColumn.Width = _viewModel.EditorVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        EditorPanel.Visibility = _viewModel.EditorVisible ? Visibility.Visible : Visibility.Collapsed;
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

    private static string GetDraftPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Paperdown",
            "draft.md");
    }

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

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Paperdown",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
        StatusText.Text = message;
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
