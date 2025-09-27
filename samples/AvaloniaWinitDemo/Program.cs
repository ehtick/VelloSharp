using Avalonia;
using Avalonia.Winit;
using Avalonia.ReactiveUI;
using VelloSharp.Avalonia.Vello;

namespace AvaloniaWinitDemo;

internal static class Program
{
    public static void Main(string[] args) => BuildAvaloniaApp()
        .UseReactiveUI()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseWinit()
        .UseVello();
}
