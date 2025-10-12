using System;
using System.Threading;
using AvaloniaVelloHarfBuzzSample.Navigation;
using AvaloniaVelloHarfBuzzSample.Services;
using Avalonia.Controls;
using CommunityToolkit.Diagnostics;

namespace AvaloniaVelloHarfBuzzSample.ViewModels.Pages;

public abstract class SamplePageViewModel : ViewModelBase, ISamplePage
{
    private readonly HarfBuzzSampleServices _services;
    private readonly Func<UserControl> _viewFactory;
    private readonly Lazy<UserControl> _view;
    private bool _isActive;

    protected SamplePageViewModel(HarfBuzzSampleServices services, string title, string subtitle, string? glyph, Func<UserControl> viewFactory)
    {
        Guard.IsNotNull(services);
        Guard.IsNotNull(viewFactory);
        Guard.IsNotNullOrWhiteSpace(title);
        Guard.IsNotNullOrWhiteSpace(subtitle);

        _services = services;
        Title = title;
        Subtitle = subtitle;
        Glyph = glyph;
        _viewFactory = viewFactory;
        _view = new Lazy<UserControl>(CreateView, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string Title { get; }

    public string Subtitle { get; }

    public string? Glyph { get; }

    public UserControl View => GetOrCreateView();

    public HarfBuzzSampleServices Services => _services;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (RaiseAndSetIfChanged(ref _isActive, value))
            {
                if (value)
                {
                    OnActivated();
                }
                else
                {
                    OnDeactivated();
                }
            }
        }
    }

    public UserControl GetOrCreateView() => _view.Value;

    protected virtual UserControl CreateView()
    {
        var control = _viewFactory();
        if (control.DataContext is null)
        {
            control.DataContext = this;
        }

        return control;
    }

    public virtual void OnActivated()
    {
    }

    public virtual void OnDeactivated()
    {
    }
}
