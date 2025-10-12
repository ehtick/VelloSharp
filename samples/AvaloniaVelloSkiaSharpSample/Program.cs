using System;
using Avalonia;
using Avalonia.Win32;
using VelloSharp.Avalonia.Vello;

namespace AvaloniaVelloSkiaSharpSample;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
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
                CompositionMode = [Win32CompositionMode.LowLatencyDxgiSwapChain],
            });
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            builder.UseX11();
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            builder.UseAvaloniaNative();
            return;
        }

        builder.UseSkia();
    }
}
