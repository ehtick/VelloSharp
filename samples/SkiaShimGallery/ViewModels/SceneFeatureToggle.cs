using System.ComponentModel;
using System.Runtime.CompilerServices;
using SkiaGallery.SharedScenes;

namespace SkiaShimGallery.ViewModels;

public sealed class SceneFeatureToggle : INotifyPropertyChanged
{
    private bool _isEnabled;

    public SceneFeatureToggle(SkiaSceneFeature feature, bool isEnabled = true)
    {
        Feature = feature;
        _isEnabled = isEnabled;
    }

    public SkiaSceneFeature Feature { get; }

    public string DisplayName => Feature.ToDisplayName();

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
