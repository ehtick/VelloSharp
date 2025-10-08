using System;
using System.Collections.Generic;
using System.Linq;
using VelloSharp.ChartEngine.Annotations;

namespace VelloSharp.ChartEngine;

/// <summary>
/// Declarative description of a multi-pane chart including shared axes and overlay layers.
/// </summary>
public sealed class ChartComposition
{
internal ChartComposition(
    IReadOnlyList<ChartPaneDefinition> panes,
    IReadOnlyList<CompositionAnnotationLayer> annotationLayers,
    ChartAnimationProfile animationProfile,
    bool hasCustomAnimations)
{
    Panes = panes;
    AnnotationLayers = annotationLayers;
    Animations = animationProfile;
    HasCustomAnimations = hasCustomAnimations;
}

/// <summary>
/// Gets the ordered list of panes composing the chart.
/// </summary>
public IReadOnlyList<ChartPaneDefinition> Panes { get; }

/// <summary>
/// Gets the overlay annotation layers applied across the composition.
/// </summary>
public IReadOnlyList<CompositionAnnotationLayer> AnnotationLayers { get; }

/// <summary>
/// Gets the animation profile applied to this composition.
/// </summary>
public ChartAnimationProfile Animations { get; }

/// <summary>
/// Indicates whether the composition overrides the engine-level animation profile.
/// </summary>
public bool HasCustomAnimations { get; }

    /// <summary>
    /// Creates a composition blueprint via a fluent builder.
    /// </summary>
    public static ChartComposition Create(Action<ChartCompositionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new ChartCompositionBuilder();
        configure(builder);
        return builder.Build();
    }
}

/// <summary>
/// Builder for creating <see cref="ChartComposition"/> instances.
/// </summary>
public sealed class ChartCompositionBuilder
{
    private readonly List<ChartPaneDefinition> _panes = new();
    private readonly List<CompositionAnnotationLayer> _annotationLayers = new();
    private ChartAnimationProfile? _animationProfile;

    public ChartPaneBuilder Pane(string id)
    {
        if (_panes.Any(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A pane with id '{id}' has already been added to this composition.");
        }

        var pane = new ChartPaneDefinition(id);
        _panes.Add(pane);
        return new ChartPaneBuilder(pane, this);
    }

    public ChartCompositionBuilder AnnotationLayer(string id, AnnotationZOrder zOrder, Action<CompositionAnnotationLayer>? configure = null)
    {
        var layer = new CompositionAnnotationLayer(id, zOrder);
        configure?.Invoke(layer);
        _annotationLayers.Add(layer);
        return this;
    }

    public ChartCompositionBuilder UseAnimations(ChartAnimationProfile profile)
    {
        _animationProfile = profile ?? ChartAnimationProfile.Default;
        return this;
    }

    internal ChartComposition Build()
    {
        if (_panes.Count == 0)
        {
            throw new InvalidOperationException("At least one pane must be added to the composition.");
        }

        var normalized = NormalizePaneWeights(_panes);
        var hasCustomAnimations = _animationProfile is not null;
        var profile = _animationProfile ?? ChartAnimationProfile.Default;
        return new ChartComposition(normalized, _annotationLayers.ToArray(), profile, hasCustomAnimations);
    }

    private static IReadOnlyList<ChartPaneDefinition> NormalizePaneWeights(IEnumerable<ChartPaneDefinition> panes)
    {
        var paneList = panes.ToList();
        var total = paneList.Sum(p => p.HeightRatio);
        if (total <= 0)
        {
            return paneList;
        }

        foreach (var pane in paneList)
        {
            pane.SetNormalizedRatio(pane.HeightRatio / total);
        }

        return paneList;
    }
}

/// <summary>
/// Builder that configures a pane within the composition.
/// </summary>
public sealed class ChartPaneBuilder
{
    private readonly ChartPaneDefinition _pane;
    private readonly ChartCompositionBuilder _owner;

    internal ChartPaneBuilder(ChartPaneDefinition pane, ChartCompositionBuilder owner)
    {
        _pane = pane;
        _owner = owner;
    }

    public ChartPaneBuilder WithSeries(params uint[] seriesIds)
    {
        _pane.SeriesIds.AddRange(seriesIds);
        return this;
    }

    public ChartPaneBuilder ShareXAxisWithPrimary(bool share = true)
    {
        _pane.ShareXAxisWithPrimary = share;
        return this;
    }

    public ChartPaneBuilder WithHeightRatio(double ratio)
    {
        if (!double.IsFinite(ratio) || ratio <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ratio), ratio, "Pane height ratio must be positive and finite.");
        }

        _pane.HeightRatio = ratio;
        return this;
    }

    public ChartCompositionBuilder Done() => _owner;
}

/// <summary>
/// Represents a logical chart pane.
/// </summary>
public sealed class ChartPaneDefinition
{
    internal ChartPaneDefinition(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Pane id must be specified.", nameof(id));
        }

        Id = id;
    }

    public string Id { get; }

    /// <summary>
    /// Gets the series identifiers assigned to this pane.
    /// </summary>
    public List<uint> SeriesIds { get; } = new();

    /// <summary>
    /// Gets or sets the relative pane height ratio.
    /// </summary>
    public double HeightRatio { get; set; } = 1.0;

    /// <summary>
    /// Gets the normalized ratio derived during composition build.
    /// </summary>
    public double NormalizedRatio { get; private set; } = 1.0;

    /// <summary>
    /// Gets or sets whether this pane reuses the primary pane's X axis.
    /// </summary>
    public bool ShareXAxisWithPrimary { get; set; } = true;

    internal void SetNormalizedRatio(double ratio) => NormalizedRatio = ratio;
}

/// <summary>
/// Describes an annotation layer applied across one or more panes.
/// </summary>
public sealed class CompositionAnnotationLayer
{
    internal CompositionAnnotationLayer(string id, AnnotationZOrder zOrder)
    {
        Id = id;
        ZOrder = zOrder;
    }

    public string Id { get; }

    public AnnotationZOrder ZOrder { get; }

    public List<ChartAnnotation> Annotations { get; } = new();

    public ISet<string> TargetPaneIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public CompositionAnnotationLayer ForPanes(params string[] paneIds)
    {
        if (paneIds is null)
        {
            throw new ArgumentNullException(nameof(paneIds));
        }

        foreach (var paneId in paneIds)
        {
            if (string.IsNullOrWhiteSpace(paneId))
            {
                continue;
            }

            TargetPaneIds.Add(paneId);
        }

        return this;
    }
}

/// <summary>
/// Determines the drawing order of composition annotation layers.
/// </summary>
public enum AnnotationZOrder
{
    BelowSeries,
    Overlay,
    AboveSeries,
}
