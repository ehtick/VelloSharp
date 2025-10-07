using System;
using System.Collections.Generic;
using VelloSharp.Charting.Styling;

namespace VelloSharp.Charting.Layout;

/// <summary>
/// Provides curated layout presets that demonstrate common dashboard configurations.
/// </summary>
public static class LayoutGallery
{
    /// <summary>
    /// Gets the built-in layout presets ordered from most conservative to most information-dense.
    /// </summary>
    public static IReadOnlyList<ChartLayoutPreset> Presets { get; } = new[]
    {
        new ChartLayoutPreset(
            "single-pane-dark",
            "Single Pane — Dark",
            "Responsive layout tuned for dark mode dashboards with generous left axis padding.",
            ChartTheme.Dark,
            request => Arrange(request, left: 72, bottom: 60)),
        new ChartLayoutPreset(
            "single-pane-light",
            "Single Pane — Light",
            "Balanced layout for light mode with compact axes that favour content over chrome.",
            ChartTheme.Light,
            request => Arrange(request, left: 56, bottom: 48)),
        new ChartLayoutPreset(
            "split-pane-analytics",
            "Split Pane — Analytics",
            "Two-pane layout reserving additional space for comparative indicators underneath the price series.",
            ChartTheme.Dark,
            request =>
            {
                var result = Arrange(request, left: 68, bottom: 52, top: 32);
                // Consumers can stack panes vertically based on HeightRatios.
                return result;
            }),
    };

    private static ChartLayoutResult Arrange(
        ChartLayoutRequest request,
        double left,
        double bottom,
        double? right = null,
        double? top = null)
    {
        var axes = new List<AxisLayoutRequest>
        {
            new AxisLayoutRequest(AxisOrientation.Left, left, minThickness: left * 0.75, maxThickness: left * 1.1),
            new AxisLayoutRequest(AxisOrientation.Bottom, bottom, minThickness: bottom * 0.75, maxThickness: bottom * 1.1),
        };

        if (right.HasValue)
        {
            axes.Add(new AxisLayoutRequest(AxisOrientation.Right, right.Value, minThickness: right.Value * 0.6));
        }

        if (top.HasValue)
        {
            axes.Add(new AxisLayoutRequest(AxisOrientation.Top, top.Value, minThickness: top.Value * 0.6));
        }

        var adjustedRequest = new ChartLayoutRequest(
            request.ViewportWidth,
            request.ViewportHeight,
            request.DevicePixelRatio,
            axes);

        var engine = new ChartLayoutEngine();
        return engine.Arrange(adjustedRequest);
    }
}

/// <summary>
/// Describes a reusable layout preset including the recommended theme pairing.
/// </summary>
public sealed record ChartLayoutPreset(
    string Id,
    string DisplayName,
    string Description,
    ChartTheme Theme,
    Func<ChartLayoutRequest, ChartLayoutResult> Arrange)
{
    public ChartLayoutResult Layout(ChartLayoutRequest request) => Arrange(request);
}
