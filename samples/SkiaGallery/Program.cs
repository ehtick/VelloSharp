using System;
using Avalonia;
using Avalonia.Native;
using Avalonia.Win32;
using Avalonia.X11;

namespace SkiaGallery;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
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
