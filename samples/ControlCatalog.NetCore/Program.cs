using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using Avalonia.Winit;
using ControlCatalog.Pages;
using VelloSharp.Avalonia.Vello;

namespace ControlCatalog.NetCore;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--wait-for-attach"))
        {
            WaitForDebugger();
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);

        return 0;
    }

    private static void WaitForDebugger()
    {
        Console.WriteLine("Attach debugger and use 'Set next statement'.");
        while (!Debugger.IsAttached)
        {
            Thread.Sleep(100);
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UseReactiveUI()
            .WithInterFont()
            .LogToTrace();

        ConfigureWindowing(builder);

        builder
            .UseVello()
            .AfterSetup(_ => EmbedSample.Implementation = CreateNativeDemoControl());

        return builder;
    }

    private static void ConfigureWindowing(AppBuilder builder)
    {
        if (OperatingSystem.IsWindows())
        {
            builder.UseWin32();
        }
        if (OperatingSystem.IsLinux())
        {
            builder.UseX11();
        }
        if (OperatingSystem.IsMacOS())
        {
            builder.UseAvaloniaNative();
        }
        /*
        var windowOptions = new VelloSharp.WinitWindowOptions
        {
            Title = "Avalonia Control Catalog"
        };

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            builder.UseWinit(new WinitPlatformOptions
            {
                Window = windowOptions
            });
        }
        else
        {
            builder.UsePlatformDetect();
        }
        */
    }

    private static INativeDemoControl? CreateNativeDemoControl()
    {
        if (OperatingSystem.IsWindows())
        {
            return new EmbedSampleWin();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new EmbedSampleMac();
        }

        if (OperatingSystem.IsLinux())
        {
            return new EmbedSampleGtk();
        }

        return null;
    }
}
