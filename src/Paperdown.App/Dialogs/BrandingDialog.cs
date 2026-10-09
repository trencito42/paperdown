using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Paperdown.Core.Models;
using Paperdown.Core.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Paperdown_App.Dialogs;

internal sealed class BrandingDialog
{
    private readonly BrandingPresetStore _presetStore = new();
    private readonly Window _window;

    public BrandingDialog(Window window) => _window = window;

    public async Task<BrandingSettings?> ShowAsync(BrandingSettings current, XamlRoot xamlRoot)
    {
        var working = Clone(current);

        var enabled = new CheckBox { Content = "Enable branding", IsChecked = working.Enabled };
        var title = new TextBox { Header = "Document title", Text = working.DocumentTitle, PlaceholderText = "Optional title on cover" };
        var subtitle = new TextBox { Header = "Subtitle", Text = working.Subtitle };
        var company = new TextBox { Header = "Company / project", Text = working.CompanyName };
        var author = new TextBox { Header = "Author", Text = working.Author };
        var date = new TextBox { Header = "Date", Text = working.DocumentDate };
        var cover = new CheckBox { Content = "Cover page", IsChecked = working.ShowCoverPage };
        var pageNumbers = new CheckBox { Content = "Page numbers", IsChecked = working.ShowPageNumbers };
        var footer = new TextBox { Header = "Footer text", Text = working.FooterText };
        var accent = new TextBox { Header = "Accent color (#RRGGBB)", Text = working.AccentColorHex };
        var logoWidth = new NumberBox
        {
            Header = "Logo width (px)",
            Value = working.LogoWidthPx,
            Minimum = 48,
            Maximum = 480,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
        };
        var alignment = new ComboBox { Header = "Logo alignment", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var align in Enum.GetValues<LogoAlignment>())
        {
            alignment.Items.Add(new ComboBoxItem { Content = align.ToString(), Tag = align });
        }

        alignment.SelectedIndex = (int)working.LogoAlignment;

        var logoPreview = new Image { Width = 120, Height = 60, Stretch = Stretch.Uniform };
        var logoStatus = new TextBlock { Foreground = Application.Current.Resources["TextFillColorSecondaryBrush"] as Brush, TextWrapping = TextWrapping.Wrap };
        var logoButton = new Button { Content = "Upload logo (PNG, JPEG)" };
        var removeLogo = new Button { Content = "Remove logo", IsEnabled = !string.IsNullOrWhiteSpace(working.LogoDataUri) };

        void RefreshLogoPreview()
        {
            if (!string.IsNullOrWhiteSpace(working.LogoDataUri))
            {
                logoPreview.Source = new BitmapImage(new Uri(working.LogoDataUri));
                logoStatus.Text = working.LogoFilePath is null ? "Logo loaded" : Path.GetFileName(working.LogoFilePath);
                removeLogo.IsEnabled = true;
            }
            else
            {
                logoPreview.Source = null;
                logoStatus.Text = "No logo selected";
                removeLogo.IsEnabled = false;
            }
        }

        RefreshLogoPreview();

        logoButton.Click += async (_, _) =>
        {
            var picker = new FileOpenPicker();
            var hwnd = WindowNative.GetWindowHandle(_window);
            InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            var ext = Path.GetExtension(file.Path).ToLowerInvariant();
            if (ext is not ".png" and not ".jpg" and not ".jpeg")
            {
                logoStatus.Text = "Unsupported image type. Use PNG or JPEG.";
                return;
            }

            var bytes = await File.ReadAllBytesAsync(file.Path);
            if (bytes.Length > 4 * 1024 * 1024)
            {
                logoStatus.Text = "Logo must be smaller than 4 MB.";
                return;
            }

            var mime = ext == ".png" ? "image/png" : "image/jpeg";
            working.LogoDataUri = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            working.LogoFilePath = file.Path;
            RefreshLogoPreview();
        };

        removeLogo.Click += (_, _) =>
        {
            working.LogoDataUri = null;
            working.LogoFilePath = null;
            RefreshLogoPreview();
        };

        var identity = Section("Identity", enabled, title, subtitle, company, author, date);
        var logoSection = Section("Logo", logoButton, removeLogo, logoPreview, logoStatus, logoWidth, alignment);
        var extras = Section("Document extras", cover, pageNumbers, footer, accent);

        var panel = new StackPanel { Spacing = 16, MinWidth = 400 };
        panel.Children.Add(identity);
        panel.Children.Add(logoSection);
        panel.Children.Add(extras);

        var scroll = new ScrollViewer { Content = panel, MaxHeight = 520 };

        var dialog = new ContentDialog
        {
            Title = "Branding",
            Content = scroll,
            PrimaryButtonText = "Apply",
            SecondaryButtonText = "Save preset",
            CloseButtonText = "Cancel",
            XamlRoot = xamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.None)
        {
            return null;
        }

        working.Enabled = enabled.IsChecked == true;
        working.DocumentTitle = title.Text;
        working.Subtitle = subtitle.Text;
        working.CompanyName = company.Text;
        working.Author = author.Text;
        working.DocumentDate = date.Text;
        working.ShowCoverPage = cover.IsChecked == true;
        working.ShowPageNumbers = pageNumbers.IsChecked == true;
        working.FooterText = footer.Text;
        working.AccentColorHex = accent.Text;
        working.LogoWidthPx = (int)logoWidth.Value;
        if (alignment.SelectedItem is ComboBoxItem alignItem && alignItem.Tag is LogoAlignment la)
        {
            working.LogoAlignment = la;
        }

        if (result == ContentDialogResult.Secondary)
        {
            var nameBox = new TextBox { Header = "Preset name", PlaceholderText = "Corporate default" };
            var saveDialog = new ContentDialog
            {
                Title = "Save branding preset",
                Content = nameBox,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                XamlRoot = xamlRoot,
            };
            if (await saveDialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(nameBox.Text))
            {
                _presetStore.Save(nameBox.Text.Trim(), working);
            }
        }

        return working;
    }

    private static StackPanel Section(string title, params UIElement[] children)
    {
        var header = new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(header);
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        return panel;
    }

    private static BrandingSettings Clone(BrandingSettings source) =>
        new()
        {
            Enabled = source.Enabled,
            CompanyName = source.CompanyName,
            DocumentTitle = source.DocumentTitle,
            Subtitle = source.Subtitle,
            Author = source.Author,
            DocumentDate = source.DocumentDate,
            FooterText = source.FooterText,
            WatermarkText = source.WatermarkText,
            ShowCoverPage = source.ShowCoverPage,
            ShowPageNumbers = source.ShowPageNumbers,
            LogoAlignment = source.LogoAlignment,
            LogoWidthPx = source.LogoWidthPx,
            LogoFilePath = source.LogoFilePath,
            LogoDataUri = source.LogoDataUri,
            AccentColorHex = source.AccentColorHex,
            BodyFont = source.BodyFont,
            HeadingFont = source.HeadingFont,
            BaseFontSizePt = source.BaseFontSizePt,
            LineSpacing = source.LineSpacing,
        };
}
