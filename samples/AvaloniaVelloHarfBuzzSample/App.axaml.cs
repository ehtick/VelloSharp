using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvaloniaVelloHarfBuzzSample.ViewModels;
using AvaloniaVelloHarfBuzzSample.Views;

namespace AvaloniaVelloHarfBuzzSample;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var galleryViewModel = new HarfBuzzGalleryViewModel();

            desktop.MainWindow = new MainWindow
            {
                DataContext = galleryViewModel,
            };

            desktop.MainWindow.Closed += (_, _) => galleryViewModel.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
