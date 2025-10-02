using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using Avalonia.Media;
using Avalonia.Platform;
using VelloSharp;
using VelloSharp.Text;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal sealed class VelloFontManagerImpl : IFontManagerImpl
{
    private const string EmbeddedFamilyName = "Roboto";

    private readonly object _sync = new();
    private readonly byte[] _embeddedFontData;
    private readonly ParleyFontService _parley = ParleyFontService.Instance;
    private string? _defaultFamilyName;
    private string[]? _installedFamilies;
    private HashSet<string>? _installedFamilyLookup;

    public VelloFontManagerImpl()
    {
        _embeddedFontData = LoadEmbeddedFont();
    }

    public string GetDefaultFontFamilyName()
    {
        lock (_sync)
        {
            if (!string.IsNullOrWhiteSpace(_defaultFamilyName))
            {
                return _defaultFamilyName!;
            }


            var name = _parley.GetDefaultFamilyName();
            _defaultFamilyName = !string.IsNullOrWhiteSpace(name) ? name : EmbeddedFamilyName;

            return _defaultFamilyName!;
        }
    }

    public string[] GetInstalledFontFamilyNames(bool checkForUpdates = false)
    {
        lock (_sync)
        {
            if (!checkForUpdates && _installedFamilies is { Length: > 0 })
            {
                return _installedFamilies;
            }

            var names = _parley.GetInstalledFamilyNames(checkForUpdates).ToArray();
            _installedFamilies = names;
            _installedFamilyLookup = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            return names;
        }
    }

    public bool TryMatchCharacter(int codepoint, FontStyle fontStyle, FontWeight fontWeight, FontStretch fontStretch, CultureInfo? culture, out Typeface typeface)
    {
        typeface = default;

        if (_parley.TryMatchCharacter((uint)codepoint, CreateQuery(null, fontStyle, fontWeight, fontStretch, culture), out var info))
        {
            typeface = new Typeface(
                string.IsNullOrWhiteSpace(info.FamilyName) ? GetDefaultFontFamilyName() : info.FamilyName,
                FromFontStyle(info.Style),
                FromFontWeightValue(info.Weight),
                FromStretchRatio(info.Stretch));
            return true;
        }

        typeface = default;
        return false;
    }

    public bool TryCreateGlyphTypeface(
        string familyName,
        FontStyle style,
        FontWeight weight,
        FontStretch stretch,
        [NotNullWhen(true)] out IGlyphTypeface? glyphTypeface)
    {
        glyphTypeface = null;

        var requestedFamily = familyName;

        if (!IsFamilyInstalled(familyName))
        {
            familyName = GetDefaultFontFamilyName();
        }

        if (_parley.TryLoadTypeface(CreateQuery(familyName, style, weight, stretch, null), out var info))
        {
            var resolvedFamily = string.IsNullOrWhiteSpace(info.FamilyName) ? familyName : info.FamilyName;
            var resolvedStyle = FromFontStyle(info.Style);
            var resolvedWeight = FromFontWeightValue(info.Weight);
            var resolvedStretch = FromStretchRatio(info.Stretch);

            var simulations = ComputeSimulations(style, weight, resolvedStyle, resolvedWeight);

            if (simulations.HasFlag(FontSimulations.Oblique))
            {
                resolvedStyle = style;
            }

            if (simulations.HasFlag(FontSimulations.Bold))
            {
                resolvedWeight = weight;
            }

            glyphTypeface = new VelloGlyphTypeface(
                resolvedFamily,
                resolvedStyle,
                resolvedWeight,
                resolvedStretch,
                info.FontData,
                info.FaceIndex,
                simulations);
            return true;
        }

        var fallback = ComputeSimulations(style, weight, FontStyle.Normal, FontWeight.Normal);
        return TryCreateEmbeddedGlyphTypeface(requestedFamily, style, weight, stretch, fallback, out glyphTypeface);
    }

    public bool TryCreateGlyphTypeface(Stream stream, FontSimulations fontSimulations, [NotNullWhen(true)] out IGlyphTypeface? glyphTypeface)
    {
        glyphTypeface = null;

        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var fontData = ms.ToArray();

        if (fontData.Length == 0)
        {
            return false;
        }

        glyphTypeface = new VelloGlyphTypeface(
            EmbeddedFamilyName,
            fontSimulations.HasFlag(FontSimulations.Oblique) ? FontStyle.Italic : FontStyle.Normal,
            fontSimulations.HasFlag(FontSimulations.Bold) ? FontWeight.Bold : FontWeight.Normal,
            FontStretch.Normal,
            fontData,
            simulations: fontSimulations);
        return true;
    }

    private bool TryCreateEmbeddedGlyphTypeface(
        string requestedFamily,
        FontStyle style,
        FontWeight weight,
        FontStretch stretch,
        FontSimulations simulations,
        [NotNullWhen(true)] out IGlyphTypeface? glyphTypeface)
    {
        if (_embeddedFontData.Length == 0)
        {
            glyphTypeface = null;
            return false;
        }

        var familyName = string.IsNullOrWhiteSpace(requestedFamily) ? EmbeddedFamilyName : requestedFamily;

        glyphTypeface = new VelloGlyphTypeface(
            familyName,
            style,
            weight,
            stretch,
            _embeddedFontData,
            simulations: simulations);
        return true;
    }

    private bool IsFamilyInstalled(string familyName)
    {
        if (string.IsNullOrWhiteSpace(familyName))
        {
            return false;
        }

        var lookup = _installedFamilyLookup;

        if (lookup is null)
        {
            GetInstalledFontFamilyNames();
            lookup = _installedFamilyLookup;
        }

        return lookup is not null && lookup.Contains(familyName);
    }

    private static FontSimulations ComputeSimulations(FontStyle requestedStyle, FontWeight requestedWeight, FontStyle resolvedStyle, FontWeight resolvedWeight)
    {
        var simulations = FontSimulations.None;

        if (requestedStyle != FontStyle.Normal && resolvedStyle != requestedStyle)
        {
            simulations |= FontSimulations.Oblique;
        }

        if ((int)requestedWeight >= 600 && (int)resolvedWeight < (int)requestedWeight)
        {
            simulations |= FontSimulations.Bold;
        }

        return simulations;
    }

    private static float ToFontWeightValue(FontWeight weight)
    {
        var value = (int)weight;
        return Math.Clamp(value, 1, 1000);
    }

    private static FontWeight FromFontWeightValue(float weight)
    {
        var value = (int)MathF.Round(weight);
        value = Math.Clamp(value, 1, 1000);
        return (FontWeight)value;
    }

    private static float ToStretchRatio(FontStretch stretch) => stretch switch
    {
        FontStretch.UltraCondensed => 0.5f,
        FontStretch.ExtraCondensed => 0.625f,
        FontStretch.Condensed => 0.75f,
        FontStretch.SemiCondensed => 0.875f,
        FontStretch.SemiExpanded => 1.125f,
        FontStretch.Expanded => 1.25f,
        FontStretch.ExtraExpanded => 1.5f,
        FontStretch.UltraExpanded => 2.0f,
        _ => 1.0f,
    };

    private ParleyFontQuery CreateQuery(string? familyName, FontStyle style, FontWeight weight, FontStretch stretch, CultureInfo? culture)
        => new(
            familyName,
            ToFontWeightValue(weight),
            ToStretchRatio(stretch),
            ToVelloFontStyle(style),
            culture);

    private static VelloFontStyle ToVelloFontStyle(FontStyle style) => style switch
    {
        FontStyle.Italic => VelloFontStyle.Italic,
        FontStyle.Oblique => VelloFontStyle.Oblique,
        _ => VelloFontStyle.Normal,
    };

    private static FontStyle FromFontStyle(VelloFontStyle style) => style switch
    {
        VelloFontStyle.Italic => FontStyle.Italic,
        VelloFontStyle.Oblique => FontStyle.Oblique,
        _ => FontStyle.Normal,
    };

    private static FontStretch FromStretchRatio(float ratio)
    {
        if (!float.IsFinite(ratio) || ratio <= 0)
        {
            return FontStretch.Normal;
        }

        return ratio switch
        {
            <= 0.5625f => FontStretch.UltraCondensed,
            <= 0.6875f => FontStretch.ExtraCondensed,
            <= 0.8125f => FontStretch.Condensed,
            <= 0.9375f => FontStretch.SemiCondensed,
            < 1.0625f => FontStretch.Normal,
            < 1.1875f => FontStretch.SemiExpanded,
            < 1.375f => FontStretch.Expanded,
            < 1.75f => FontStretch.ExtraExpanded,
            _ => FontStretch.UltraExpanded,
        };
    }

    private static byte[] LoadEmbeddedFont()
    {
        var assembly = typeof(VelloFontManagerImpl).Assembly;
        const string resourceName = "VelloSharp.Avalonia.Vello.Assets.Fonts.Roboto-Regular.ttf";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return Array.Empty<byte>();
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
