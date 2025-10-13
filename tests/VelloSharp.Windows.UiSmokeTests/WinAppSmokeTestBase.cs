using System;
using System.Threading;
using System.Threading.Tasks;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using Xunit;

namespace VelloSharp.Tests.Windows;

public abstract class WinAppSmokeTestBase : IAsyncLifetime
{
    private WindowsDriver? _session;
    private string? _appPath;

    protected WindowsDriver Session
        => _session ?? throw new InvalidOperationException("The WinAppDriver session has not been initialised.");

    protected abstract string SampleRelativePath { get; }

    protected abstract string ExecutableName { get; }

    protected virtual string TargetFramework => "net8.0-windows10.0.19041";

    public async Task InitializeAsync()
    {
        _appPath = SampleLocator.GetSampleExecutable(SampleRelativePath, ExecutableName, TargetFramework);
        _session = await WinAppDriverSession.LaunchAsync(_appPath).ConfigureAwait(false);
        await WaitForElementAsync("SwapChain").ConfigureAwait(false);
    }

    protected Task<AppiumElement> WaitForElementAsync(string automationId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        return WaitHelpers.WaitForElementAsync(Session, automationId, timeout, cancellationToken);
    }

    public async Task DisposeAsync()
    {
        if (_session is not null)
        {
            try
            {
                _session.Quit();
            }
            catch
            {
                // Ignore teardown failures.
            }
            finally
            {
                _session.Dispose();
                _session = null;
            }
        }

        if (!string.IsNullOrEmpty(_appPath))
        {
            ProcessUtils.TerminateProcessTree(_appPath!);
        }

        await Task.CompletedTask;
    }
}
