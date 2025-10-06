using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.DynamicDependency;

namespace VelloSharp.Uno.WinAppSdkSample;

internal static class Program
{
    // Encodes Windows App SDK major/minor (1.5) expected by the bootstrapper.
    private const uint WinAppSdkMajorMinorVersion = (1u << 16) | 5u;
    private const int AppModelErrorNoPackage = unchecked((int)0x80670016);

    [STAThread]
    private static void Main(string[] args)
    {
        var bootstrapInitialized = TryBootstrapWindowsAppSdk(out var bootstrapHr);

        if (!bootstrapInitialized)
        {
            if (!TryLoadSelfContainedRuntime(out var loadFailure))
            {
                var message = $"Failed to locate Windows App SDK runtime 1.5 (HRESULT 0x{bootstrapHr:X8})." +
                              " Install the corresponding redistributable or enable the self-contained runtime payload.";
                if (loadFailure is not null)
                {
                    throw new InvalidOperationException(message, loadFailure);
                }

                throw new InvalidOperationException(message);
            }
        }

        try
        {
            NativeMethods.XamlCheckProcessRequirements();
            WinRT.ComWrappersSupport.InitializeComWrappers();

            Application.Start(_ =>
            {
                var syncContext = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(syncContext);
                new App();
            });
        }
        finally
        {
            if (bootstrapInitialized)
            {
                Bootstrap.Shutdown();
            }
        }
    }

    private static bool TryBootstrapWindowsAppSdk(out int failureHr)
    {
        try
        {
            Bootstrap.Initialize(WinAppSdkMajorMinorVersion);
            failureHr = 0;
            return true;
        }
        catch (DllNotFoundException ex)
        {
            failureHr = ex.HResult;
            return false;
        }
        catch (COMException ex) when (ex.HResult == AppModelErrorNoPackage)
        {
            failureHr = ex.HResult;
            return false;
        }
    }

    private static bool TryLoadSelfContainedRuntime(out Exception? failure)
    {
        failure = null;

        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, "Microsoft.ui.xaml.dll"),
            Path.Combine(baseDirectory, "runtimes", "win10-x64", "native", "Microsoft.ui.xaml.dll")
        };

        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                _ = NativeLibrary.Load(candidate);
                return true;
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }

        failure ??= new FileNotFoundException("Microsoft.ui.xaml.dll not found in the app output.", baseDirectory);
        return false;
    }

    private static class NativeMethods
    {
        [DllImport("Microsoft.ui.xaml.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void XamlCheckProcessRequirements();
    }
}
