using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VelloSharp.Text;

namespace SkiaSharp;

public sealed class SKFontManager : IDisposable
{
    private readonly Dictionary<string, SKTypeface> _families;
    private readonly List<SKTypeface> _typefaces;
    private readonly ParleyFontService _parley = ParleyFontService.Instance;

    private SKFontManager()
    {
        _families = new Dictionary<string, SKTypeface>(StringComparer.OrdinalIgnoreCase);
        _typefaces = new List<SKTypeface>();
        RegisterTypeface(SKTypeface.Default);
        InitializeDefaultTypeface();
    }

    public static SKFontManager Default { get; } = CreateDefault();

    public static SKFontManager CreateDefault() => new SKFontManager();

    public string[] GetFontFamilies() => _parley.GetInstalledFamilyNames().ToArray();

    public SKTypeface? MatchFamily(string? familyName, SKFontStyle? fontStyle = null)
    {
        if (!string.IsNullOrEmpty(familyName) && _families.TryGetValue(familyName, out var match))
        {
            return match;
        }

        if (!string.IsNullOrEmpty(familyName))
        {
            var query = CreateQuery(familyName, fontStyle ?? SKFontStyle.Normal, culture: null);
            if (_parley.TryLoadTypeface(query, out var info))
            {
                var typeface = CreateTypeface(info);
                RegisterTypeface(typeface);
                return typeface;
            }
        }

        return _typefaces.FirstOrDefault();
    }

    public SKTypeface? MatchCharacter(string? familyName, SKFontStyle fontStyle, string[]? bcp47, int codepoint)
    {
        var culture = TryGetCulture(bcp47);
        var query = CreateQuery(familyName, fontStyle, culture);
        if (_parley.TryMatchCharacter((uint)codepoint, query, out var info))
        {
            var typeface = CreateTypeface(info);
            RegisterTypeface(typeface);
            return typeface;
        }

        return MatchFamily(familyName, fontStyle);
    }

    public SKFontStyleSet GetFontStyles(string familyName)
    {
        if (_families.TryGetValue(familyName, out var match))
        {
            return new SKFontStyleSet(new[] { match.FontStyle });
        }

        return new SKFontStyleSet(Array.Empty<SKFontStyle>());
    }

    public void Dispose()
    {
    }

    private void RegisterTypeface(SKTypeface typeface)
    {
        if (typeface is null)
        {
            return;
        }

        _typefaces.Add(typeface);
        if (!_families.ContainsKey(typeface.FamilyName))
        {
            _families[typeface.FamilyName] = typeface;
        }
    }

    private void InitializeDefaultTypeface()
    {
        var query = CreateQuery(_parley.GetDefaultFamilyName(), SKFontStyle.Normal, culture: null);
        if (_parley.TryLoadTypeface(query, out var info))
        {
            var typeface = CreateTypeface(info);
            RegisterTypeface(typeface);
        }
    }

    private SKTypeface CreateTypeface(ParleyFontInfo info)
    {
        var style = new SKFontStyle(
            ToSkiaWeight(info.Weight),
            ToSkiaWidth(info.Stretch),
            ToSkiaSlant(info.Style));
        return SKTypeface.FromFontData(info.FontData, info.FaceIndex, info.FamilyName, style);
    }

    private static CultureInfo? TryGetCulture(string[]? bcp47)
    {
        if (bcp47 is { Length: > 0 } && !string.IsNullOrWhiteSpace(bcp47[0]))
        {
            try
            {
                return CultureInfo.GetCultureInfo(bcp47[0]);
            }
            catch (CultureNotFoundException)
            {
                return null;
            }
        }

        return null;
    }

    private ParleyFontQuery CreateQuery(string? familyName, SKFontStyle fontStyle, CultureInfo? culture)
        => new(
            familyName,
            ToFontWeightValue(fontStyle.Weight),
            ToStretchRatio(fontStyle.Width),
            ToVelloFontStyle(fontStyle.Slant),
            culture);

    private static float ToFontWeightValue(SKFontStyleWeight weight)
        => Math.Clamp((int)weight, 1, 1000);

    private static float ToStretchRatio(SKFontStyleWidth width) => width switch
    {
        SKFontStyleWidth.UltraCondensed => 0.5f,
        SKFontStyleWidth.ExtraCondensed => 0.625f,
        SKFontStyleWidth.Condensed => 0.75f,
        SKFontStyleWidth.SemiCondensed => 0.875f,
        SKFontStyleWidth.SemiExpanded => 1.125f,
        SKFontStyleWidth.Expanded => 1.25f,
        SKFontStyleWidth.ExtraExpanded => 1.5f,
        SKFontStyleWidth.UltraExpanded => 2.0f,
        _ => 1.0f,
    };

    private static VelloFontStyle ToVelloFontStyle(SKFontStyleSlant slant) => slant switch
    {
        SKFontStyleSlant.Italic => VelloFontStyle.Italic,
        SKFontStyleSlant.Oblique => VelloFontStyle.Oblique,
        _ => VelloFontStyle.Normal,
    };

    private static SKFontStyleWeight ToSkiaWeight(float weight)
    {
        if (!float.IsFinite(weight))
        {
            return SKFontStyleWeight.Normal;
        }

        var rounded = (int)MathF.Round(weight / 100f) * 100;
        rounded = Math.Clamp(rounded, (int)SKFontStyleWeight.Thin, (int)SKFontStyleWeight.Black);
        return (SKFontStyleWeight)rounded;
    }

    private static SKFontStyleWidth ToSkiaWidth(float stretch)
    {
        if (!float.IsFinite(stretch) || stretch <= 0)
        {
            return SKFontStyleWidth.Normal;
        }

        return stretch switch
        {
            <= 0.5625f => SKFontStyleWidth.UltraCondensed,
            <= 0.6875f => SKFontStyleWidth.ExtraCondensed,
            <= 0.8125f => SKFontStyleWidth.Condensed,
            <= 0.9375f => SKFontStyleWidth.SemiCondensed,
            < 1.0625f => SKFontStyleWidth.Normal,
            < 1.1875f => SKFontStyleWidth.SemiExpanded,
            < 1.375f => SKFontStyleWidth.Expanded,
            < 1.75f => SKFontStyleWidth.ExtraExpanded,
            _ => SKFontStyleWidth.UltraExpanded,
        };
    }

    private static SKFontStyleSlant ToSkiaSlant(VelloFontStyle style) => style switch
    {
        VelloFontStyle.Italic => SKFontStyleSlant.Italic,
        VelloFontStyle.Oblique => SKFontStyleSlant.Oblique,
        _ => SKFontStyleSlant.Upright,
    };
}

public sealed class SKFontStyleSet : IEnumerable<SKFontStyle>, IDisposable
{
    private readonly SKFontStyle[] _styles;

    internal SKFontStyleSet(IEnumerable<SKFontStyle> styles)
    {
        _styles = styles?.ToArray() ?? Array.Empty<SKFontStyle>();
    }

    public int Count => _styles.Length;

    public SKFontStyle this[int index] => _styles[index];

    public IEnumerator<SKFontStyle> GetEnumerator() => ((IEnumerable<SKFontStyle>)_styles).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
    }
}

public sealed class SKFontStyle
{
    public static SKFontStyle Normal { get; } = new(SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
    public static SKFontStyle Italic { get; } = new(SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Italic);
    public static SKFontStyle Bold { get; } = new(SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
    public static SKFontStyle BoldItalic { get; } = new(SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Italic);

    public SKFontStyle(SKFontStyleWeight weight, SKFontStyleWidth width, SKFontStyleSlant slant)
    {
        Weight = weight;
        Width = width;
        Slant = slant;
    }

    public SKFontStyleWeight Weight { get; }
    public SKFontStyleWidth Width { get; }
    public SKFontStyleSlant Slant { get; }
}

public enum SKFontStyleWeight
{
    Invisible = 0,
    Thin = 100,
    ExtraLight = 200,
    Light = 300,
    Normal = 400,
    Medium = 500,
    SemiBold = 600,
    Bold = 700,
    ExtraBold = 800,
    Black = 900,
}

public enum SKFontStyleWidth
{
    UltraCondensed = 1,
    ExtraCondensed = 2,
    Condensed = 3,
    SemiCondensed = 4,
    Normal = 5,
    SemiExpanded = 6,
    Expanded = 7,
    ExtraExpanded = 8,
    UltraExpanded = 9,
}

public enum SKFontStyleSlant
{
    Upright = 0,
    Italic = 1,
    Oblique = 2,
}
