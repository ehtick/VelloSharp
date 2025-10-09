using System;
using Avalonia;
using Avalonia.Win32;
using Avalonia.X11;
using Avalonia.Native;
using VelloSharp.Avalonia.Vello;

namespace SkiaShimGallery;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UseVello()
            .WithInterFont()
            .LogToTrace();

        ConfigureWindowing(builder);
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
    }
}
