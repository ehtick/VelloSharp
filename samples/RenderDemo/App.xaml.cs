using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VelloSharp.Avalonia.Vello;

namespace RenderDemo
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow();
            base.OnFrameworkInitializationCompleted();
        }

        // TODO: Make this work with GTK/Skia/Cairo depending on command-line args
        // again.
        static void Main(string[] args) 
            => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        // App configuration, used by the entry point and previewer
        public static AppBuilder BuildAvaloniaApp()
        {
            var builder = AppBuilder.Configure<App>()
                .WithInterFont()
                .LogToTrace();

            ConfigureWindowing(builder);

            builder.UseVello();

            return builder;
        }
 
        private static void ConfigureWindowing(AppBuilder builder)
        {
            if (OperatingSystem.IsWindows())
            {
                builder.UseWin32();
                builder.With(new Win32PlatformOptions()
                {
                    OverlayPopups = true,
                    // TODO: Win32CompositionMode.LowLatencyDxgiSwapChain is required to enable Vello rendering.
                    CompositionMode = [Win32CompositionMode.LowLatencyDxgiSwapChain]
                });
            }
            /*
            if (OperatingSystem.IsLinux())
            {
                builder.UseX11();
            }
            */
            if (OperatingSystem.IsMacOS())
            {
                builder.UseAvaloniaNative();
            }
        }
    }
}
