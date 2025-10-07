using System;
using System.Collections.Generic;

namespace VelloSharp.Charting.Styling;

/// <summary>
/// Represents a cohesive set of styling tokens for charts.
/// </summary>
public sealed class ChartTheme
{
    public ChartTheme(string name, ChartPalette palette, AxisStyle axisStyle, LegendStyle legendStyle, IReadOnlyDictionary<string, ChartTypography>? typographyVariants = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Theme name is required.", nameof(name));
        }

        Name = name;
        Palette = palette ?? throw new ArgumentNullException(nameof(palette));
        Axis = axisStyle ?? throw new ArgumentNullException(nameof(axisStyle));
        Legend = legendStyle ?? throw new ArgumentNullException(nameof(legendStyle));
        TypographyVariants = typographyVariants ?? new Dictionary<string, ChartTypography>(StringComparer.OrdinalIgnoreCase);
    }

    public string Name { get; }

    public ChartPalette Palette { get; }

    public AxisStyle Axis { get; }

    public LegendStyle Legend { get; }

    public IReadOnlyDictionary<string, ChartTypography> TypographyVariants { get; }

    public override string ToString() => Name;

    public static ChartTheme Light { get; } = CreateLightTheme();

    public static ChartTheme Dark { get; } = CreateDarkTheme();

    public static ChartTheme Default => Light;

    private static ChartTheme CreateLightTheme()
    {
        var palette = new ChartPalette(
            background: RgbaColor.FromHex(0xF5F7FBFF),
            foreground: RgbaColor.FromHex(0x1F2430FF),
            axisLine: RgbaColor.FromHex(0x5A6478FF),
            axisTick: RgbaColor.FromHex(0x5A6478FF),
            gridLine: RgbaColor.FromHex(0xD4DAE6FF),
            legendBackground: RgbaColor.FromHex(0xFFFFFFFF),
            legendBorder: RgbaColor.FromHex(0xCED7EAFF),
            series: new[]
            {
                RgbaColor.FromHex(0x3AB8FFFF),
                RgbaColor.FromHex(0xF45E8CFF),
                RgbaColor.FromHex(0x81FFF9FF),
                RgbaColor.FromHex(0xFFD14FFF),
            });

        var axisTypography = new ChartTypography("Segoe UI", 11d);
        var legendTypography = new ChartTypography("Segoe UI", 11d);
        var axisStyle = AxisStyle.FromPalette(palette, axisTypography, tickLength: 6d, labelMargin: 6d);
        var legendStyle = new LegendStyle(
            background: palette.LegendBackground,
            border: palette.LegendBorder,
            borderThickness: 1d,
            labelTypography: legendTypography,
            markerSize: 10d,
            itemSpacing: 6d,
            padding: 12d,
            labelSpacing: 6d);

        var variants = new Dictionary<string, ChartTypography>(StringComparer.OrdinalIgnoreCase)
        {
            ["AxisLabel"] = axisTypography,
            ["LegendLabel"] = legendTypography,
            ["Body"] = new ChartTypography("Segoe UI", 12d)
        };

        return new ChartTheme("Light", palette, axisStyle, legendStyle, variants);
    }

    private static ChartTheme CreateDarkTheme()
    {
        var palette = new ChartPalette(
            background: RgbaColor.FromHex(0x10151FFF),
            foreground: RgbaColor.FromHex(0xECEFF4FF),
            axisLine: RgbaColor.FromHex(0x8FA2C3FF),
            axisTick: RgbaColor.FromHex(0x8FA2C3FF),
            gridLine: RgbaColor.FromHex(0x1E2838FF),
            legendBackground: RgbaColor.FromHex(0x1B2333FF),
            legendBorder: RgbaColor.FromHex(0x2F3A4FFF),
            series: new[]
            {
                RgbaColor.FromHex(0x3AB8FFFF),
                RgbaColor.FromHex(0xF45E8CFF),
                RgbaColor.FromHex(0x81FFF9FF),
                RgbaColor.FromHex(0xFFD14FFF),
            });

        var axisTypography = new ChartTypography("Segoe UI", 11d);
        var legendTypography = new ChartTypography("Segoe UI", 11d);
        var axisStyle = AxisStyle.FromPalette(palette, axisTypography, tickLength: 6d, labelMargin: 6d);
        var legendStyle = new LegendStyle(
            background: palette.LegendBackground,
            border: palette.LegendBorder,
            borderThickness: 1d,
            labelTypography: legendTypography,
            markerSize: 10d,
            itemSpacing: 6d,
            padding: 12d,
            labelSpacing: 6d);

        var variants = new Dictionary<string, ChartTypography>(StringComparer.OrdinalIgnoreCase)
        {
            ["AxisLabel"] = axisTypography,
            ["LegendLabel"] = legendTypography,
            ["Body"] = new ChartTypography("Segoe UI", 12d)
        };

        return new ChartTheme("Dark", palette, axisStyle, legendStyle, variants);
    }
}
