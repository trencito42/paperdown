using Paperdown.Pdf;

namespace Paperdown.Tests;

public class WebView2EnvironmentHelperTests
{
    [Fact]
    public void ProbeRuntime_detects_loader_in_app_output()
    {
        var probe = WebView2EnvironmentHelper.ProbeRuntime();
        Assert.True(probe.LoaderPresent, "WebView2Loader.dll should be deployed with test host output.");
        Assert.False(string.IsNullOrWhiteSpace(probe.AppDirectory));
    }

    [Fact]
    public void ProbeRuntime_reports_runtime_version_on_windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var probe = WebView2EnvironmentHelper.ProbeRuntime();
        Assert.True(probe.RuntimeInstalled);
        Assert.False(string.IsNullOrWhiteSpace(probe.RuntimeVersion));
    }

    [Fact]
    public void DescribeFailure_distinguishes_missing_runtime_from_loader_issues()
    {
        var probe = new WebView2RuntimeProbe(
            "X64",
            AppContext.BaseDirectory,
            LoaderDirectory: null,
            LoaderPresent: false,
            RuntimeVersion: null,
            RuntimeProbeError: null,
            RuntimeInstalled: false);

        var message = WebView2EnvironmentHelper.DescribeFailure(
            new DllNotFoundException("The specified module could not be found."),
            probe);

        Assert.True(message.IsRuntimeMissing);
    }
}
