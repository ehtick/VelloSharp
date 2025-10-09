using Avalonia;

namespace AvaloniaSkiaMotionMark;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions()
            {
                // TODO: Win32CompositionMode.LowLatencyDxgiSwapChain is required to enable Vello rendering.
                CompositionMode = [Win32CompositionMode.LowLatencyDxgiSwapChain]
            })
            .With(new SkiaOptions()
            {
                MaxGpuResourceSizeBytes = 4_294_967_296,
            })
            .LogToTrace();
}
