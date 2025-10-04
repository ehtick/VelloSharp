using Avalonia;
using Avalonia.ReactiveUI;
using VelloSharp.Avalonia.Vello;

namespace AvaloniaVelloX11Demo;

internal static class Program
{
    public static void Main(string[] args) => BuildAvaloniaApp()
        .UseReactiveUI()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseX11()
        .UseVello()
        .WithInterFont()
        .LogToTrace();
}
