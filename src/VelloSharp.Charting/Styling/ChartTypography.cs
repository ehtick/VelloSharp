using System;

namespace VelloSharp.Charting.Styling;

/// <summary>
/// Describes typography tokens used for chart text.
/// </summary>
public sealed class ChartTypography
{
    public ChartTypography(
        string fontFamily,
        double fontSize,
        double? lineHeight = null,
        double? letterSpacing = null,
        string? fontWeight = null)
    {
        if (string.IsNullOrWhiteSpace(fontFamily))
        {
            throw new ArgumentException("Font family is required.", nameof(fontFamily));
        }

        if (!double.IsFinite(fontSize) || fontSize <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSize), fontSize, "Font size must be positive and finite.");
        }

        FontFamily = fontFamily;
        FontSize = fontSize;
        LineHeight = lineHeight;
        LetterSpacing = letterSpacing;
        FontWeight = fontWeight;
    }

    public string FontFamily { get; }

    public double FontSize { get; }

    public double? LineHeight { get; }

    public double? LetterSpacing { get; }

    public string? FontWeight { get; }

    public static ChartTypography Default { get; } = new("Segoe UI", 12d);
}
