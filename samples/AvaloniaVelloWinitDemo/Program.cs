using Avalonia;
using Avalonia.ReactiveUI;
using Avalonia.Winit;
using VelloSharp.Avalonia.Vello;

namespace AvaloniaVelloWinitDemo;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .UseReactiveUI()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseWinit()
        .UseVello()
        .WithInterFont()
        .LogToTrace();
}
