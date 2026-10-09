using System.Diagnostics;
using System.Globalization;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Paperdown.Core.Interfaces;
using Paperdown.Core.Models;

namespace Paperdown.Pdf;

public sealed class WebView2PdfExportService : IPdfExportService
{
    public Task<ExportResult> ExportHtmlToPdfAsync(
        string htmlContent,
        string outputFilePath,
        PageSettings pageSettings,
        CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<ExportResult>();
        var thread = new Thread(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = RunExportOnStaThread(htmlContent, outputFilePath, pageSettings, cancellationToken);
                tcs.TrySetResult(result);
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetResult(new ExportResult
                {
                    Success = false,
                    ErrorMessage = "Export was cancelled.",
                });
            }
            catch (Exception ex)
            {
                tcs.TrySetResult(new ExportResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                });
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }

    private static ExportResult RunExportOnStaThread(
        string htmlContent,
        string outputFilePath,
        PageSettings pageSettings,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var outputDir = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var exportWorkDir = Path.Combine(
            Path.GetTempPath(),
            "Paperdown",
            "export");
        Directory.CreateDirectory(exportWorkDir);
        var tempHtml = Path.Combine(exportWorkDir, $"document_{Guid.NewGuid():N}.html");
        File.WriteAllText(tempHtml, htmlContent, System.Text.Encoding.UTF8);

        ExportResult? exportResult = null;
        using var form = new Form
        {
            Width = 1024,
            Height = 768,
            ShowInTaskbar = false,
            WindowState = FormWindowState.Minimized,
            Opacity = 0,
        };

        var webView = new WebView2 { Dock = DockStyle.Fill };
        form.Controls.Add(webView);

        form.Shown += async (_, _) =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                WebView2EnvironmentHelper.PrepareNativeLoader();
                var envResult = await WebView2EnvironmentHelper.CreateEnvironmentAsync(cancellationToken);
                if (!envResult.Success)
                {
                    throw new InvalidOperationException(
                        envResult.UserMessage?.Summary ?? envResult.Exception?.Message ?? "WebView2 failed to start.");
                }

                await webView.EnsureCoreWebView2Async(
                    await CoreWebView2Environment.CreateAsync(null, WebView2EnvironmentHelper.GetUserDataFolder()));

                webView.CoreWebView2.Settings.IsScriptEnabled = true;
                webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                webView.CoreWebView2.Settings.IsWebMessageEnabled = false;

                var settings = webView.CoreWebView2.Environment.CreatePrintSettings();
                settings.ShouldPrintBackgrounds = pageSettings.PrintBackgroundGraphics;
                settings.MarginTop = MmToInches(pageSettings.MarginTopMm);
                settings.MarginRight = MmToInches(pageSettings.MarginRightMm);
                settings.MarginBottom = MmToInches(pageSettings.MarginBottomMm);
                settings.MarginLeft = MmToInches(pageSettings.MarginLeftMm);

                var (widthIn, heightIn) = GetPageDimensionsInches(pageSettings);
                settings.PageWidth = widthIn;
                settings.PageHeight = heightIn;

                var tcsNav = new TaskCompletionSource<bool>();
                webView.CoreWebView2.NavigationCompleted += (_, args) => tcsNav.TrySetResult(args.IsSuccess);
                webView.CoreWebView2.Navigate(new Uri(tempHtml).AbsoluteUri);
                var navigated = await tcsNav.Task;
                if (!navigated)
                {
                    throw new InvalidOperationException("Failed to load document HTML for PDF export.");
                }

                await WebView2DocumentLoadWaiter.WaitForDocumentReadyAsync(
                    webView.CoreWebView2,
                    cancellationToken,
                    TimeSpan.FromSeconds(30));

                webView.CoreWebView2.Settings.IsScriptEnabled = false;

                var success = await webView.CoreWebView2.PrintToPdfAsync(outputFilePath, settings);
                if (!success)
                {
                    throw new InvalidOperationException("WebView2 PrintToPdfAsync returned false.");
                }

                var pageCount = PdfPageCounter.CountPages(outputFilePath);
                exportResult = new ExportResult
                {
                    Success = true,
                    OutputPdfPath = outputFilePath,
                    PageCount = pageCount,
                    ElapsedTime = stopwatch.Elapsed,
                };
            }
            catch (Exception ex)
            {
                exportResult = new ExportResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ElapsedTime = stopwatch.Elapsed,
                };
            }
            finally
            {
                try
                {
                    if (File.Exists(tempHtml))
                    {
                        File.Delete(tempHtml);
                    }
                }
                catch
                {
                    // ignore temp cleanup errors
                }

                form.Close();
            }
        };

        Application.Run(form);
        return exportResult ?? new ExportResult { Success = false, ErrorMessage = "Export did not complete." };
    }

    private static double MmToInches(double mm) => mm / 25.4;

    private static (double width, double height) GetPageDimensionsInches(PageSettings settings)
    {
        var (wMm, hMm) = PageDimensions.GetPageSizeMm(settings);
        var w = wMm / 25.4;
        var h = hMm / 25.4;

        if (settings.Orientation == PageOrientation.Landscape)
        {
            return (h, w);
        }

        return (w, h);
    }

}
