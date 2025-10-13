using System.Threading.Tasks;
using OpenQA.Selenium.Appium;
using Xunit;

namespace VelloSharp.Tests.Windows;

public sealed class UwpSmokeTests : WinAppSmokeTestBase
{
    protected override string SampleRelativePath => "samples/UwpVelloGallery";

    protected override string ExecutableName => "UwpVelloGallery.exe";

    [Fact(DisplayName = "UWP swapchain panel renders")]
    public async Task SwapChainPanelShouldRender()
    {
        AppiumElement element = await WaitForElementAsync("SwapChain").ConfigureAwait(false);
        Assert.True(element.Displayed);
    }

    [Fact(DisplayName = "UWP diagnostics label initialises")]
    public async Task DiagnosticsLabelShouldReportReady()
    {
        AppiumElement element = await WaitForElementAsync("DiagnosticsLabel").ConfigureAwait(false);
        Assert.Equal("Ready", element.Text);
    }
}
