using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AvaloniaVelloSkiaSharpSample.Services;
using AvaloniaVelloSkiaSharpSample.ViewModels.Pages;

namespace AvaloniaVelloSkiaSharpSample.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly SkiaBackendService _backendService = new();
    private SamplePageViewModel? _selectedPage;
    private SkiaBackendDescriptor _selectedBackend;

    public MainWindowViewModel()
    {
        Pages = new ObservableCollection<SamplePageViewModel>
        {
            new WelcomePageViewModel(),
        };

        foreach (var page in Pages)
        {
            page.IsActive = false;
        }

        _selectedPage = Pages.FirstOrDefault();
        if (_selectedPage is not null)
        {
            _selectedPage.IsActive = true;
        }

        _selectedBackend = _backendService.CurrentDescriptor;
        _backendService.BackendChanged += (_, descriptor) => SelectedBackend = descriptor;
    }

    public ObservableCollection<SamplePageViewModel> Pages { get; }

    public SamplePageViewModel? SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedPage, value))
            {
                foreach (var page in Pages)
                {
                    page.IsActive = page == value;
                }
            }
        }
    }

    public IReadOnlyList<SkiaBackendDescriptor> Backends => _backendService.Backends;

    public SkiaBackendDescriptor SelectedBackend
    {
        get => _selectedBackend;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedBackend, value) && value.Kind != _backendService.Current)
            {
                _backendService.SetBackend(value.Kind);
            }
        }
    }
}
