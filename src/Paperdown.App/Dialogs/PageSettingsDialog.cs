using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Paperdown.Core.Models;

namespace Paperdown_App.Dialogs;

internal static class PageSettingsDialog
{
    public static async Task<PageSettings?> ShowAsync(PageSettings current, XamlRoot xamlRoot)
    {
        var working = new PageSettings
        {
            PageSize = current.PageSize,
            Orientation = current.Orientation,
            MarginTopMm = current.MarginTopMm,
            MarginRightMm = current.MarginRightMm,
            MarginBottomMm = current.MarginBottomMm,
            MarginLeftMm = current.MarginLeftMm,
            PrintBackgroundGraphics = current.PrintBackgroundGraphics,
        };

        var sizeBox = new ComboBox { Header = "Page size", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var size in Enum.GetValues<PageSize>())
        {
            sizeBox.Items.Add(new ComboBoxItem { Content = size.ToString(), Tag = size });
        }

        sizeBox.SelectedIndex = Array.IndexOf(Enum.GetValues<PageSize>(), working.PageSize);

        var orientationBox = new ComboBox { Header = "Orientation", HorizontalAlignment = HorizontalAlignment.Stretch };
        orientationBox.Items.Add(new ComboBoxItem { Content = "Portrait", Tag = PageOrientation.Portrait });
        orientationBox.Items.Add(new ComboBoxItem { Content = "Landscape", Tag = PageOrientation.Landscape });
        orientationBox.SelectedIndex = working.Orientation == PageOrientation.Landscape ? 1 : 0;

        var sameMargins = new CheckBox { Content = "Use same margins on all sides", IsChecked = MarginsEqual(working) };
        var top = CreateMarginBox("Top (mm)", working.MarginTopMm);
        var right = CreateMarginBox("Right (mm)", working.MarginRightMm);
        var bottom = CreateMarginBox("Bottom (mm)", working.MarginBottomMm);
        var left = CreateMarginBox("Left (mm)", working.MarginLeftMm);
        var printBg = new CheckBox { Content = "Print background graphics", IsChecked = working.PrintBackgroundGraphics };

        void SyncLinkedMargins(bool linked)
        {
            right.IsEnabled = !linked;
            bottom.IsEnabled = !linked;
            left.IsEnabled = !linked;
            if (linked)
            {
                right.Value = top.Value;
                bottom.Value = top.Value;
                left.Value = top.Value;
            }
        }

        sameMargins.Checked += (_, _) => SyncLinkedMargins(true);
        sameMargins.Unchecked += (_, _) => SyncLinkedMargins(false);
        top.ValueChanged += (_, _) =>
        {
            if (sameMargins.IsChecked == true)
            {
                right.Value = top.Value;
                bottom.Value = top.Value;
                left.Value = top.Value;
            }
        };

        SyncLinkedMargins(sameMargins.IsChecked == true);

        var paperHeader = new TextBlock { Text = "Paper", FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        var marginHeader = new TextBlock { Text = "Margins", FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        var marginGrid = new Grid
        {
            ColumnSpacing = 8,
            RowSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(),
                new ColumnDefinition(),
            },
            RowDefinitions =
            {
                new RowDefinition(),
                new RowDefinition(),
            },
        };
        top.SetValue(Grid.RowProperty, 0);
        top.SetValue(Grid.ColumnProperty, 0);
        right.SetValue(Grid.RowProperty, 0);
        right.SetValue(Grid.ColumnProperty, 1);
        bottom.SetValue(Grid.RowProperty, 1);
        bottom.SetValue(Grid.ColumnProperty, 0);
        left.SetValue(Grid.RowProperty, 1);
        left.SetValue(Grid.ColumnProperty, 1);
        marginGrid.Children.Add(top);
        marginGrid.Children.Add(right);
        marginGrid.Children.Add(bottom);
        marginGrid.Children.Add(left);

        var panel = new StackPanel { Spacing = 12, MinWidth = 360 };
        panel.Children.Add(paperHeader);
        panel.Children.Add(sizeBox);
        panel.Children.Add(orientationBox);
        panel.Children.Add(marginHeader);
        panel.Children.Add(sameMargins);
        panel.Children.Add(marginGrid);
        panel.Children.Add(printBg);

        var scroll = new ScrollViewer { Content = panel, MaxHeight = 480 };

        var dialog = new ContentDialog
        {
            Title = "Page settings",
            Content = scroll,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            XamlRoot = xamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        if (sizeBox.SelectedItem is ComboBoxItem sizeItem && sizeItem.Tag is PageSize ps)
        {
            working.PageSize = ps;
        }

        if (orientationBox.SelectedItem is ComboBoxItem orientItem && orientItem.Tag is PageOrientation po)
        {
            working.Orientation = po;
        }

        working.MarginTopMm = top.Value;
        working.MarginRightMm = right.Value;
        working.MarginBottomMm = bottom.Value;
        working.MarginLeftMm = left.Value;
        working.PrintBackgroundGraphics = printBg.IsChecked == true;

        var (w, h) = PageDimensions.GetPageSizeMm(working);
        var minDim = Math.Min(w, h) - working.MarginLeftMm - working.MarginRightMm;
        if (minDim < 40)
        {
            var warn = new ContentDialog
            {
                Title = "Margins too large",
                Content = "Margins leave too little space on the page. Reduce margins and try again.",
                CloseButtonText = "OK",
                XamlRoot = xamlRoot,
            };
            await warn.ShowAsync();
            return null;
        }

        return working;
    }

    private static bool MarginsEqual(PageSettings page) =>
        Math.Abs(page.MarginTopMm - page.MarginRightMm) < 0.01
        && Math.Abs(page.MarginTopMm - page.MarginBottomMm) < 0.01
        && Math.Abs(page.MarginTopMm - page.MarginLeftMm) < 0.01;

    private static NumberBox CreateMarginBox(string header, double value) =>
        new()
        {
            Header = header,
            Value = value,
            Minimum = 5,
            Maximum = 60,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            SmallChange = 1,
            LargeChange = 5,
        };
}
