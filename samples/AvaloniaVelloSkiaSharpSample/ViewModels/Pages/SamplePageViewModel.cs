using AvaloniaVelloSkiaSharpSample.Rendering;

namespace AvaloniaVelloSkiaSharpSample.ViewModels.Pages;

public abstract class SamplePageViewModel : ViewModelBase, ISkiaLeaseRenderer
{
    private bool _isActive;

    protected SamplePageViewModel(string title, string description, string? icon = null)
    {
        Title = title;
        Description = description;
        Icon = icon;
    }

    public string Title { get; }

    public string Description { get; }

    public string? Icon { get; }

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

    public virtual void Render(in SkiaLeaseRenderContext context)
    {
    }

    public virtual void OnActivated()
    {
    }

    public virtual void OnDeactivated()
    {
    }
}
