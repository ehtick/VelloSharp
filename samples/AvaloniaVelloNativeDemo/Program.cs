using Avalonia;
using Avalonia.ReactiveUI;
using VelloSharp.Avalonia.Vello;

namespace AvaloniaVelloNativeDemo;

internal static class Program
{
    public static void Main(string[] args) => BuildAvaloniaApp()
        .UseReactiveUI()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseAvaloniaNative()
        .UseVello()
        .WithInterFont()
        .LogToTrace();
}
