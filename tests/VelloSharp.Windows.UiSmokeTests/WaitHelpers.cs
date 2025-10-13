using System;
using System.Threading;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using Xunit.Sdk;

namespace VelloSharp.Tests.Windows;

internal static class WaitHelpers
{
    public static async Task<AppiumElement> WaitForElementAsync(
        WindowsDriver session,
        string automationId,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(automationId))
        {
            throw new ArgumentException("AutomationId must be provided.", nameof(automationId));
        }

        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        var pollInterval = TimeSpan.FromMilliseconds(250);

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var element = session.FindElement(MobileBy.AccessibilityId(automationId)) as AppiumElement;
                if (element is not null)
                {
                    return element;
                }
            }
            catch (WebDriverException)
            {
                // Element not yet available; poll until timeout.
            }

            await Task.Delay(pollInterval, cancellationToken);
        }

        throw new XunitException($"Element with AutomationId '{automationId}' was not located within the allotted time.");
    }
}
