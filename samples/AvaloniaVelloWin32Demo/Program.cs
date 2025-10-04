using Avalonia;
using Avalonia.ReactiveUI;
using VelloSharp.Avalonia.Vello;

namespace AvaloniaVelloWin32Demo;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .UseReactiveUI()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseWin32()
        .UseVello()
        .WithInterFont()
        .LogToTrace();
}
