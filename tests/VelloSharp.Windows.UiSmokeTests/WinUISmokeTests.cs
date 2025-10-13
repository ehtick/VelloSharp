using System.Threading.Tasks;
using OpenQA.Selenium.Appium;
using Xunit;

namespace VelloSharp.Tests.Windows;

public sealed class WinUISmokeTests : WinAppSmokeTestBase
{
    protected override string SampleRelativePath => "samples/WinUIVelloGallery";

    protected override string ExecutableName => "WinUIVelloGallery.exe";

    [Fact(DisplayName = "WinUI swapchain control renders")]
    public async Task SwapChainControlShouldRender()
    {
        AppiumElement element = await WaitForElementAsync("SwapChain").ConfigureAwait(false);
        Assert.True(element.Displayed);
    }

    [Fact(DisplayName = "WinUI diagnostics label initialises")]
    public async Task DiagnosticsLabelShouldReportReady()
    {
        AppiumElement element = await WaitForElementAsync("DiagnosticsLabel").ConfigureAwait(false);
        Assert.Equal("Ready", element.Text);
    }
}
