using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using Avalonia.Winit;
using VelloSharp.Avalonia.Vello;

namespace VelloSharp.Charting.AvaloniaSample;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--wait-for-attach"))
        {
            WaitForDebugger();
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UseReactiveUI()
            .WithInterFont()
            .LogToTrace();

        ConfigureWindowing(builder);

        builder.UseVello();

        return builder;
    }

    private static void ConfigureWindowing(AppBuilder builder)
    {
        if (OperatingSystem.IsWindows())
        {
            builder.UseWin32();
            builder.With(new Win32PlatformOptions
            {
                CompositionMode = [Win32CompositionMode.LowLatencyDxgiSwapChain]
            });
        }
        else if (OperatingSystem.IsLinux())
        {
            builder.UseX11();
        }
        else if (OperatingSystem.IsMacOS())
        {
            builder.UseAvaloniaNative();
        }
        else
        {
            builder.UsePlatformDetect();
        }
    }

    private static void WaitForDebugger()
    {
        Console.WriteLine("Waiting for debugger to attach...");
        while (!Debugger.IsAttached)
        {
            Thread.Sleep(100);
        }
    }
}
