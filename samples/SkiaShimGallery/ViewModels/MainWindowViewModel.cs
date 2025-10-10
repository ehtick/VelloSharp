using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using SkiaGallery.SharedScenes;

namespace SkiaShimGallery.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IReadOnlyList<ISkiaGalleryScene> _allScenes = SkiaGallerySceneRegistry.All;
    private readonly IReadOnlyList<SceneFeatureToggle> _featureToggles;
    private IReadOnlyList<ISkiaGalleryScene> _scenes;

    public MainWindowViewModel()
    {
        _featureToggles = BuildFeatureToggles();
        foreach (var toggle in _featureToggles)
        {
            toggle.PropertyChanged += OnFeatureToggleChanged;
        }

        _scenes = FilterScenes();
    }

    public IReadOnlyList<ISkiaGalleryScene> Scenes
    {
        get => _scenes;
        private set
        {
            if (!ReferenceEquals(_scenes, value))
            {
                _scenes = value;
                OnPropertyChanged();
            }
        }
    }

    public IReadOnlyList<SceneFeatureToggle> FeatureToggles => _featureToggles;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnFeatureToggleChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SceneFeatureToggle.IsEnabled))
        {
            Scenes = FilterScenes();
        }
    }

    private IReadOnlyList<ISkiaGalleryScene> FilterScenes()
    {
        var enabledFeatures = new HashSet<SkiaSceneFeature>(
            _featureToggles.Where(t => t.IsEnabled).Select(t => t.Feature));

        if (enabledFeatures.Count == _featureToggles.Count)
        {
            return _allScenes;
        }

        return _allScenes.Where(scene => enabledFeatures.Contains(scene.Feature)).ToList();
    }

    private IReadOnlyList<SceneFeatureToggle> BuildFeatureToggles()
    {
        var features = _allScenes
            .Select(scene => scene.Feature)
            .Distinct()
            .OrderBy(feature => feature switch
            {
                SkiaSceneFeature.CoreCanvas => 0,
                SkiaSceneFeature.Paint => 1,
                SkiaSceneFeature.BlendModes => 2,
                SkiaSceneFeature.Gradients => 3,
                SkiaSceneFeature.ImageCodecs => 4,
                SkiaSceneFeature.Text => 5,
                SkiaSceneFeature.Geometry => 6,
                SkiaSceneFeature.Resources => 7,
                _ => 8,
            })
            .ToList();

        return features.Select(feature => new SceneFeatureToggle(feature, isEnabled: true)).ToList();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
