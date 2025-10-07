using System;
using System.Globalization;
using VelloSharp.Charting.Styling;

namespace VelloSharp.Charting.Styling.Configuration;

internal static class ThemeColorParser
{
    public static RgbaColor Parse(string? value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Color value for '{propertyName}' is required.");
        }

        return ParseInternal(value, propertyName);
    }

    public static RgbaColor ParseOrDefault(string? value, RgbaColor fallback, string propertyName)
        => string.IsNullOrWhiteSpace(value) ? fallback : ParseInternal(value, propertyName);

    private static RgbaColor ParseInternal(string raw, string propertyName)
    {
        var span = raw.Trim();
        if (span.StartsWith("#", StringComparison.Ordinal))
        {
            span = span[1..];
        }

        if (span.Length is not (6 or 8))
        {
            throw new InvalidOperationException($"Color '{raw}' for '{propertyName}' must be in #RRGGBB or #RRGGBBAA format.");
        }

        if (!uint.TryParse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
        {
            throw new InvalidOperationException($"Color '{raw}' for '{propertyName}' is not a valid hexadecimal value.");
        }

        if (span.Length == 6)
        {
            hex = (hex << 8) | 0xFF;
        }

        return RgbaColor.FromHex(hex);
    }
}
