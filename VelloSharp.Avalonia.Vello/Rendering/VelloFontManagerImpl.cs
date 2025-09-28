using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Platform;
using VelloSharp;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal sealed class VelloFontManagerImpl : IFontManagerImpl
{
    private const string EmbeddedFamilyName = "Roboto";

    private readonly object _sync = new();
    private readonly byte[] _embeddedFontData;
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

            var ptr = NativeMethods.vello_parley_get_default_family();
            try
            {
                var name = ptr != IntPtr.Zero ? Marshal.PtrToStringUTF8(ptr) : null;
                _defaultFamilyName = !string.IsNullOrWhiteSpace(name) ? name : EmbeddedFamilyName;
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    NativeMethods.vello_string_destroy(ptr);
                }
            }

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

            var status = NativeMethods.vello_parley_get_family_names(out var handle, out var array);
            try
            {
                if (status != VelloStatus.Success || array.Count == 0 || array.Items == IntPtr.Zero)
                {
                    var fallback = new[] { GetDefaultFontFamilyName() };
                    _installedFamilyLookup = new HashSet<string>(fallback, StringComparer.OrdinalIgnoreCase);
                    return _installedFamilies = fallback;
                }

                var count = checked((int)array.Count);
                var names = new string[count];

                unsafe
                {
                    var items = (IntPtr*)array.Items;
                    for (var i = 0; i < count; i++)
                    {
                        names[i] = Marshal.PtrToStringUTF8(items[i]) ?? string.Empty;
                    }
                }

                Array.Sort(names, StringComparer.OrdinalIgnoreCase);
                _installedFamilies = names;
                _installedFamilyLookup = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
                return names;
            }
            finally
            {
                if (handle != IntPtr.Zero)
                {
                    NativeMethods.vello_parley_string_array_destroy(handle);
                }
            }
        }
    }

    public bool TryMatchCharacter(int codepoint, FontStyle fontStyle, FontWeight fontWeight, FontStretch fontStretch, CultureInfo? culture, out Typeface typeface)
    {
        typeface = default;

        var status = NativeMethods.vello_parley_match_character(
            (uint)codepoint,
            ToFontWeightValue(fontWeight),
            ToStretchRatio(fontStretch),
            ToFontStyleValue(fontStyle),
            familyName: null,
            locale: culture?.Name,
            out var handle,
            out var info);

        if (status != VelloStatus.Success)
        {
            if (handle != IntPtr.Zero)
            {
                NativeMethods.vello_parley_font_handle_destroy(handle);
            }

            return false;
        }

        try
        {
            var family = Marshal.PtrToStringUTF8(info.FamilyName) ?? GetDefaultFontFamilyName();
            typeface = new Typeface(
                family,
                FromFontStyleValue(info.Style),
                FromFontWeightValue(info.Weight),
                FromStretchRatio(info.Stretch));
            return true;
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                NativeMethods.vello_parley_font_handle_destroy(handle);
            }
        }
    }

    public bool TryCreateGlyphTypeface(
        string familyName,
        FontStyle style,
        FontWeight weight,
        FontStretch stretch,
        [NotNullWhen(true)] out IGlyphTypeface? glyphTypeface)
    {
        glyphTypeface = null;

        if (!IsFamilyInstalled(familyName))
        {
            familyName = GetDefaultFontFamilyName();
        }

        var status = NativeMethods.vello_parley_load_typeface(
            familyName,
            ToFontWeightValue(weight),
            ToStretchRatio(stretch),
            ToFontStyleValue(style),
            out var handle,
            out var info);

        if (status != VelloStatus.Success)
        {
            if (handle != IntPtr.Zero)
            {
                NativeMethods.vello_parley_font_handle_destroy(handle);
            }

            return TryCreateEmbeddedGlyphTypeface(familyName, style, weight, stretch, out glyphTypeface);
        }

        try
        {
            var data = CopyFontData(info);
            if (data.Length == 0)
            {
                return TryCreateEmbeddedGlyphTypeface(familyName, style, weight, stretch, out glyphTypeface);
            }

            var resolvedFamily = Marshal.PtrToStringUTF8(info.FamilyName) ?? familyName;
            glyphTypeface = new VelloGlyphTypeface(
                resolvedFamily,
                FromFontStyleValue(info.Style),
                FromFontWeightValue(info.Weight),
                FromStretchRatio(info.Stretch),
                data,
                info.Index);
            return true;
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                NativeMethods.vello_parley_font_handle_destroy(handle);
            }
        }
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

        glyphTypeface = new VelloGlyphTypeface(EmbeddedFamilyName, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, fontData);
        return true;
    }

    private bool TryCreateEmbeddedGlyphTypeface(
        string familyName,
        FontStyle style,
        FontWeight weight,
        FontStretch stretch,
        [NotNullWhen(true)] out IGlyphTypeface? glyphTypeface)
    {
        if (_embeddedFontData.Length == 0)
        {
            glyphTypeface = null;
            return false;
        }

        glyphTypeface = new VelloGlyphTypeface(familyName, style, weight, stretch, _embeddedFontData);
        return true;
    }

    private static byte[] CopyFontData(VelloParleyFontInfoNative info)
    {
        if (info.Length == 0 || info.Data == IntPtr.Zero)
        {
            return Array.Empty<byte>();
        }

        var length = checked((int)info.Length);
        if (length == 0)
        {
            return Array.Empty<byte>();
        }

        var data = new byte[length];
        Marshal.Copy(info.Data, data, 0, length);
        return data;
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

    private static int ToFontStyleValue(FontStyle style) => style switch
    {
        FontStyle.Italic => 1,
        FontStyle.Oblique => 2,
        _ => 0,
    };

    private static FontStyle FromFontStyleValue(int style) => style switch
    {
        1 => FontStyle.Italic,
        2 => FontStyle.Oblique,
        _ => FontStyle.Normal,
    };

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
