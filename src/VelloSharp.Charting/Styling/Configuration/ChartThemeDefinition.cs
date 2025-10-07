using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using VelloSharp.Charting.Styling;

namespace VelloSharp.Charting.Styling.Configuration;

public sealed class ChartThemeDefinition
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("palette")]
    public ChartPaletteDefinition? Palette { get; set; }

    [JsonPropertyName("axis")]
    public AxisStyleDefinition? Axis { get; set; }

    [JsonPropertyName("legend")]
    public LegendStyleDefinition? Legend { get; set; }

    [JsonPropertyName("typography")]
    public Dictionary<string, ChartTypographyDefinition>? Typography { get; set; }

    public ChartTheme ToTheme()
    {
        if (Palette is null)
        {
            throw new InvalidOperationException("Theme palette must be specified.");
        }

        var palette = Palette.ToPalette();
        var axisTypography = Axis?.LabelTypography?.ToTypography() ?? ChartTypography.Default;
        var axisStyle = Axis?.ToAxisStyle(palette, axisTypography) ?? AxisStyle.FromPalette(palette, axisTypography);

        var legendTypography = Legend?.LabelTypography?.ToTypography() ?? axisTypography;
        var legendStyle = Legend?.ToLegendStyle(palette, legendTypography)
                          ?? new LegendStyle(
                              background: palette.LegendBackground,
                              border: palette.LegendBorder,
                              borderThickness: 1d,
                              labelTypography: legendTypography,
                              markerSize: 10d,
                              itemSpacing: 6d,
                              padding: 12d,
                              labelSpacing: 6d);

        var variants = Typography?.Count > 0
            ? Typography.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToTypography(),
                StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, ChartTypography>(StringComparer.OrdinalIgnoreCase);

        var name = string.IsNullOrWhiteSpace(Name) ? "Custom" : Name!;
        return new ChartTheme(name, palette, axisStyle, legendStyle, variants);
    }
}

public sealed class ChartPaletteDefinition
{
    [JsonPropertyName("background")]
    public string? Background { get; set; }

    [JsonPropertyName("foreground")]
    public string? Foreground { get; set; }

    [JsonPropertyName("axisLine")]
    public string? AxisLine { get; set; }

    [JsonPropertyName("axisTick")]
    public string? AxisTick { get; set; }

    [JsonPropertyName("gridLine")]
    public string? GridLine { get; set; }

    [JsonPropertyName("legendBackground")]
    public string? LegendBackground { get; set; }

    [JsonPropertyName("legendBorder")]
    public string? LegendBorder { get; set; }

    [JsonPropertyName("series")]
    public List<string>? Series { get; set; }

    public ChartPalette ToPalette()
    {
        var background = ThemeColorParser.Parse(Background, nameof(Background));
        var foreground = ThemeColorParser.Parse(Foreground, nameof(Foreground));
        var axisLine = ThemeColorParser.Parse(AxisLine, nameof(AxisLine));
        var axisTick = ThemeColorParser.Parse(AxisTick, nameof(AxisTick));
        var gridLine = ThemeColorParser.Parse(GridLine, nameof(GridLine));
        var legendBackground = ThemeColorParser.Parse(LegendBackground, nameof(LegendBackground));
        var legendBorder = ThemeColorParser.Parse(LegendBorder, nameof(LegendBorder));

        if (Series is null || Series.Count == 0)
        {
            throw new InvalidOperationException("At least one series color must be specified.");
        }

        var series = Series
            .Select((value, index) => ThemeColorParser.Parse(value, $"series[{index}]"))
            .ToList();

        return new ChartPalette(
            background,
            foreground,
            axisLine,
            axisTick,
            gridLine,
            legendBackground,
            legendBorder,
            series);
    }
}

public sealed class AxisStyleDefinition
{
    [JsonPropertyName("lineColor")]
    public string? LineColor { get; set; }

    [JsonPropertyName("tickColor")]
    public string? TickColor { get; set; }

    [JsonPropertyName("tickLength")]
    public double? TickLength { get; set; }

    [JsonPropertyName("labelMargin")]
    public double? LabelMargin { get; set; }

    [JsonPropertyName("labelTypography")]
    public ChartTypographyDefinition? LabelTypography { get; set; }

    public AxisStyle ToAxisStyle(ChartPalette palette, ChartTypography fallbackTypography)
    {
        var lineColor = ThemeColorParser.ParseOrDefault(LineColor, palette.AxisLine, nameof(LineColor));
        var tickColor = ThemeColorParser.ParseOrDefault(TickColor, palette.AxisTick, nameof(TickColor));
        var tickLength = TickLength ?? 6d;
        var labelMargin = LabelMargin ?? 4d;
        var typography = LabelTypography?.ToTypography() ?? fallbackTypography;
        return new AxisStyle(lineColor, tickColor, tickLength, typography, labelMargin);
    }
}

public sealed class LegendStyleDefinition
{
    [JsonPropertyName("background")]
    public string? Background { get; set; }

    [JsonPropertyName("border")]
    public string? Border { get; set; }

    [JsonPropertyName("borderThickness")]
    public double? BorderThickness { get; set; }

    [JsonPropertyName("markerSize")]
    public double? MarkerSize { get; set; }

    [JsonPropertyName("itemSpacing")]
    public double? ItemSpacing { get; set; }

    [JsonPropertyName("padding")]
    public double? Padding { get; set; }

    [JsonPropertyName("labelSpacing")]
    public double? LabelSpacing { get; set; }

    [JsonPropertyName("labelTypography")]
    public ChartTypographyDefinition? LabelTypography { get; set; }

    public LegendStyle ToLegendStyle(ChartPalette palette, ChartTypography fallbackTypography)
    {
        var background = ThemeColorParser.ParseOrDefault(Background, palette.LegendBackground, nameof(Background));
        var border = ThemeColorParser.ParseOrDefault(Border, palette.LegendBorder, nameof(Border));
        var typography = LabelTypography?.ToTypography() ?? fallbackTypography;
        var borderThickness = BorderThickness ?? 1d;
        var markerSize = MarkerSize ?? 10d;
        var itemSpacing = ItemSpacing ?? 6d;
        var padding = Padding ?? 12d;
        var labelSpacing = LabelSpacing ?? 6d;

        return new LegendStyle(
            background,
            border,
            borderThickness,
            typography,
            markerSize,
            itemSpacing,
            padding,
            labelSpacing);
    }
}

public sealed class ChartTypographyDefinition
{
    [JsonPropertyName("fontFamily")]
    public string? FontFamily { get; set; }

    [JsonPropertyName("fontSize")]
    public double? FontSize { get; set; }

    [JsonPropertyName("lineHeight")]
    public double? LineHeight { get; set; }

    [JsonPropertyName("letterSpacing")]
    public double? LetterSpacing { get; set; }

    [JsonPropertyName("fontWeight")]
    public string? FontWeight { get; set; }

    public ChartTypography ToTypography()
    {
        if (string.IsNullOrWhiteSpace(FontFamily))
        {
            throw new InvalidOperationException("Typography fontFamily is required.");
        }

        if (FontSize is null || !double.IsFinite(FontSize.Value) || FontSize.Value <= 0d)
        {
            throw new InvalidOperationException("Typography fontSize must be a positive number.");
        }

        return new ChartTypography(
            FontFamily,
            FontSize.Value,
            LineHeight,
            LetterSpacing,
            FontWeight);
    }
}
