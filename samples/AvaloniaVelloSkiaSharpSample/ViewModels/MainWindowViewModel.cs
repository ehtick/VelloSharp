using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Styling;
using AvaloniaVelloSkiaSharpSample.Diagnostics;
using AvaloniaVelloSkiaSharpSample.Services;
using AvaloniaVelloSkiaSharpSample.ViewModels.Pages;

namespace AvaloniaVelloSkiaSharpSample.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly SkiaBackendService _backendService;
    private readonly SkiaCaptureRecorder _captureRecorder;
    private readonly SkiaResourceService _resourceService;
    private readonly SkiaSceneRecorder _sceneRecorder;
    private SamplePageViewModel? _selectedPage;
    private SkiaBackendDescriptor _selectedBackend;
    private bool _isDarkTheme = true;

    public MainWindowViewModel(
        SkiaBackendService? backendService = null,
        SkiaCaptureRecorder? captureRecorder = null)
    {
        _backendService = backendService ?? new SkiaBackendService();
        _captureRecorder = captureRecorder ?? new SkiaCaptureRecorder(Path.Combine(AppContext.BaseDirectory, "artifacts", "samples", "skiasharp"));
        _resourceService = new SkiaResourceService(Path.Combine(AppContext.BaseDirectory, "Assets"));
        _sceneRecorder = new SkiaSceneRecorder();

        Pages = new ObservableCollection<SamplePageViewModel>
        {
            new WelcomePageViewModel(_captureRecorder, _backendService, _resourceService),
            new SurfaceDashboardViewModel(_backendService, _captureRecorder, _resourceService),
            new CanvasPaintStudioViewModel(_captureRecorder, _backendService, _resourceService),
            new GeometryExplorerViewModel(_captureRecorder, _backendService, _resourceService),
            new ImageWorkshopViewModel(_captureRecorder, _backendService, _resourceService),
            new IoDiagnosticsViewModel(_resourceService, _captureRecorder, _backendService),
            new TypographyPlaygroundViewModel(_captureRecorder, _backendService, _resourceService),
            new AdvancedUtilitiesViewModel(_resourceService, _captureRecorder, _backendService),
            new RecordingStudioViewModel(_sceneRecorder, _captureRecorder, _backendService, _resourceService),
            new RuntimeEffectForgeViewModel(_captureRecorder, _backendService, _resourceService),
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
        ApplyTheme(_isDarkTheme);
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

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (RaiseAndSetIfChanged(ref _isDarkTheme, value))
            {
                ApplyTheme(value);
            }
        }
    }

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

    private static void ApplyTheme(bool isDark)
    {
        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        application.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    public void Dispose()
    {
        _resourceService.Dispose();
        _sceneRecorder.Dispose();
    }
}
