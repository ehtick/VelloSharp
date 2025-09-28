using Avalonia;

namespace AvaloniaSkiaMotionMark;

internal static class Program
{
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new SkiaOptions()
            {
                MaxGpuResourceSizeBytes = 4_294_967_296,
            })
            .LogToTrace();
}
