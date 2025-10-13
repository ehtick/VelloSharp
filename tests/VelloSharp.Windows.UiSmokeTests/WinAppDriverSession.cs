using System;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace VelloSharp.Tests.Windows;

internal static class WinAppDriverSession
{
    private static readonly Uri ServiceEndpoint = new("http://127.0.0.1:4723/");

    public static async Task<WindowsDriver> LaunchAsync(string appPath)
    {
        if (string.IsNullOrWhiteSpace(appPath))
        {
            throw new ArgumentException("Application path must be provided.", nameof(appPath));
        }

        var options = new AppiumOptions();
        options.AddAdditionalAppiumOption("app", appPath);
        options.AddAdditionalAppiumOption("platformName", "Windows");
        options.AddAdditionalAppiumOption("deviceName", "WindowsPC");
        options.AddAdditionalAppiumOption("ms:waitForAppLaunch", "15");

        WindowsDriver session;
        try
        {
            session = new WindowsDriver(ServiceEndpoint, options);
        }
        catch (WebDriverException ex)
        {
            throw new InvalidOperationException("Failed to create a WinAppDriver session. Ensure WinAppDriver is running on the build agent before executing the smoke tests.", ex);
        }

        session.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);

        await Task.Delay(TimeSpan.FromSeconds(2));

        return session;
    }
}
