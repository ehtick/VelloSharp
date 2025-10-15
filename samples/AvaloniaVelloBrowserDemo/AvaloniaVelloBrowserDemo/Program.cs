using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using VelloSharp.Avalonia.Browser;

namespace AvaloniaVelloBrowserDemo;

internal static class Program
{
    [SupportedOSPlatform("browser")]
    private static Task Main(string[] args) => BuildAvaloniaApp()
        .WithInterFont()
        .StartBrowserAppAsync("out");

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder
            .Configure<App>()
            .UseVelloBrowser();
}
