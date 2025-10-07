using System;
using System.Collections.Generic;

namespace VelloSharp.Charting.Layout;

/// <summary>
/// Requests chart layout computation for a viewport.
/// </summary>
public sealed class ChartLayoutRequest
{
    public ChartLayoutRequest(
        double viewportWidth,
        double viewportHeight,
        double devicePixelRatio,
        IReadOnlyList<AxisLayoutRequest>? axes = null)
    {
        if (!double.IsFinite(viewportWidth) || viewportWidth < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportWidth), viewportWidth, "Viewport width must be a non-negative finite value.");
        }

        if (!double.IsFinite(viewportHeight) || viewportHeight < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportHeight), viewportHeight, "Viewport height must be a non-negative finite value.");
        }

        if (!double.IsFinite(devicePixelRatio) || devicePixelRatio <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(devicePixelRatio), devicePixelRatio, "Device pixel ratio must be positive and finite.");
        }

        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
        DevicePixelRatio = devicePixelRatio;
        Axes = axes ?? Array.Empty<AxisLayoutRequest>();
    }

    /// <summary>
    /// Gets the width of the available viewport (DIPs).
    /// </summary>
    public double ViewportWidth { get; }

    /// <summary>
    /// Gets the height of the available viewport (DIPs).
    /// </summary>
    public double ViewportHeight { get; }

    /// <summary>
    /// Gets the device pixel ratio used for DPI-aware rounding.
    /// </summary>
    public double DevicePixelRatio { get; }

    /// <summary>
    /// Gets the axis layout requests.
    /// </summary>
    public IReadOnlyList<AxisLayoutRequest> Axes { get; }
}
