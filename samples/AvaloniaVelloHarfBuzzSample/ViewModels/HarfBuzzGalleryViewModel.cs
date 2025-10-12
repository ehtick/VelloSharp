using System;
using System.Collections.ObjectModel;
using System.Linq;
using AvaloniaVelloHarfBuzzSample.ViewModels.Pages;
using AvaloniaVelloHarfBuzzSample.Services;
using AvaloniaVelloHarfBuzzSample.Diagnostics;

namespace AvaloniaVelloHarfBuzzSample.ViewModels;

public sealed class HarfBuzzGalleryViewModel : ViewModelBase, IDisposable
{
    private readonly FontAssetService _fontAssets;
    private readonly HarfBuzzShapeService _shapeService;
    private readonly ShapeCaptureRecorder _captureRecorder;
    private SamplePageViewModel? _selectedPage;
    private bool _disposed;

    public HarfBuzzGalleryViewModel()
    {
        Title = "VelloSharp HarfBuzz Sample Gallery";
        Subtitle = "Exercise the HarfBuzzSharp shim end-to-end through the Vello lease pipeline.";

        _fontAssets = new FontAssetService();
        _shapeService = new HarfBuzzShapeService(_fontAssets);
        _captureRecorder = new ShapeCaptureRecorder();
        Services = new HarfBuzzSampleServices(_fontAssets, _shapeService, _captureRecorder);

        Pages = new ObservableCollection<SamplePageViewModel>
        {
            new WelcomePageViewModel(Services),
        };

        SelectedPage = Pages.FirstOrDefault();
    }

    public string Title { get; }

    public string Subtitle { get; }

    public ObservableCollection<SamplePageViewModel> Pages { get; }

    public HarfBuzzSampleServices Services { get; }

    public SamplePageViewModel? SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (ReferenceEquals(_selectedPage, value))
            {
                return;
            }

            var previous = _selectedPage;
            if (RaiseAndSetIfChanged(ref _selectedPage, value))
            {
                if (previous is not null)
                {
                    previous.IsActive = false;
                }

                if (_selectedPage is not null)
                {
                    _selectedPage.IsActive = true;
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _fontAssets.Dispose();
    }
}
