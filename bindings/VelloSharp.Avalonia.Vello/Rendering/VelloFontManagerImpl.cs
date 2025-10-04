using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;
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

        var metadata = ParseFontMetadata(fontData);
        var familyName = string.IsNullOrWhiteSpace(metadata.FamilyName) ? EmbeddedFamilyName : metadata.FamilyName;
        var style = metadata.Style;
        var weight = metadata.Weight;
        var stretch = metadata.Stretch;

        if (fontSimulations.HasFlag(FontSimulations.Oblique) && style == FontStyle.Normal)
        {
            style = FontStyle.Italic;
        }

        if (fontSimulations.HasFlag(FontSimulations.Bold) && (int)weight < (int)FontWeight.Bold)
        {
            weight = FontWeight.Bold;
        }

        glyphTypeface = new VelloGlyphTypeface(
            familyName,
            style,
            weight,
            stretch,
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

    private static FontMetadata ParseFontMetadata(byte[] fontData)
    {
        if (fontData is not { Length: >= 12 })
        {
            return new FontMetadata(EmbeddedFamilyName, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal);
        }

        var tables = ParseTableDirectory(fontData);
        var familyName = ParseFamilyName(fontData, tables) ?? EmbeddedFamilyName;
        var (style, weight, stretch) = ParseOs2(fontData, tables);
        return new FontMetadata(familyName, style, weight, stretch);
    }

    private static Dictionary<uint, (int Offset, int Length)> ParseTableDirectory(ReadOnlySpan<byte> data)
    {
        var tables = new Dictionary<uint, (int Offset, int Length)>();

        if (data.Length < 12)
        {
            return tables;
        }

        var numTables = ReadUInt16BE(data.Slice(4, 2));
        const int recordSize = 16;
        var recordsOffset = 12;

        for (var i = 0; i < numTables; i++)
        {
            var entryStart = recordsOffset + i * recordSize;
            if (entryStart + recordSize > data.Length)
            {
                break;
            }

            var tag = ReadUInt32BE(data.Slice(entryStart, 4));
            var offset = ReadUInt32BE(data.Slice(entryStart + 8, 4));
            var length = ReadUInt32BE(data.Slice(entryStart + 12, 4));

            if (offset > int.MaxValue || length > int.MaxValue)
            {
                continue;
            }

            var intOffset = (int)offset;
            var intLength = (int)length;

            if (intOffset < 0 || intLength <= 0 || intOffset + intLength > data.Length)
            {
                continue;
            }

            tables[tag] = (intOffset, intLength);
        }

        return tables;
    }

    private static string? ParseFamilyName(ReadOnlySpan<byte> data, IReadOnlyDictionary<uint, (int Offset, int Length)> tables)
    {
        if (!tables.TryGetValue(MakeTag("name"), out var entry))
        {
            return null;
        }

        var span = data.Slice(entry.Offset, entry.Length);
        if (span.Length < 6)
        {
            return null;
        }

        var count = ReadUInt16BE(span.Slice(2, 2));
        var stringOffset = ReadUInt16BE(span.Slice(4, 2));
        var recordsStart = 6;
        string? fallback = null;

        for (var i = 0; i < count; i++)
        {
            var recordPos = recordsStart + i * 12;
            if (recordPos + 12 > span.Length)
            {
                break;
            }

            var record = span.Slice(recordPos, 12);
            var platformId = ReadUInt16BE(record.Slice(0, 2));
            var languageId = ReadUInt16BE(record.Slice(4, 2));
            var nameId = ReadUInt16BE(record.Slice(6, 2));
            var length = ReadUInt16BE(record.Slice(8, 2));
            var offset = ReadUInt16BE(record.Slice(10, 2));

            if (nameId != 1)
            {
                continue;
            }

            var stringPos = stringOffset + offset;
            if (stringPos + length > span.Length)
            {
                continue;
            }

            var nameSpan = span.Slice(stringPos, length);
            string name;

            if (platformId == 0 || platformId == 3)
            {
                name = Encoding.BigEndianUnicode.GetString(nameSpan);
            }
            else
            {
                name = Encoding.ASCII.GetString(nameSpan);
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if ((platformId == 3 && languageId == 0x0409) || platformId == 0)
            {
                return name;
            }

            fallback ??= name;
        }

        return fallback;
    }

    private static (FontStyle Style, FontWeight Weight, FontStretch Stretch) ParseOs2(ReadOnlySpan<byte> data, IReadOnlyDictionary<uint, (int Offset, int Length)> tables)
    {
        var style = FontStyle.Normal;
        var weight = FontWeight.Normal;
        var stretch = FontStretch.Normal;

        if (!tables.TryGetValue(MakeTag("OS/2"), out var entry))
        {
            return (style, weight, stretch);
        }

        if (entry.Offset < 0 || entry.Length <= 0 || entry.Offset + entry.Length > data.Length)
        {
            return (style, weight, stretch);
        }

        var span = data.Slice(entry.Offset, entry.Length);

        if (span.Length >= 8)
        {
            var weightClass = ReadUInt16BE(span.Slice(4, 2));
            var clampedWeight = Math.Clamp((int)weightClass, 1, 1000);
            weight = (FontWeight)clampedWeight;

            var widthClass = ReadUInt16BE(span.Slice(6, 2));
            stretch = WidthClassToStretch(Math.Clamp((int)widthClass, 1, 9));
        }

        if (span.Length >= 64)
        {
            var selection = ReadUInt16BE(span.Slice(62, 2));

            if ((selection & 0x01) != 0)
            {
                style = FontStyle.Italic;
            }
            else if ((selection & 0x200) != 0)
            {
                style = FontStyle.Oblique;
            }

            if ((selection & 0x20) != 0 && (int)weight < (int)FontWeight.Bold)
            {
                weight = FontWeight.Bold;
            }
        }

        return (style, weight, stretch);
    }

    private static FontStretch WidthClassToStretch(int widthClass) => widthClass switch
    {
        1 => FontStretch.UltraCondensed,
        2 => FontStretch.ExtraCondensed,
        3 => FontStretch.Condensed,
        4 => FontStretch.SemiCondensed,
        5 => FontStretch.Normal,
        6 => FontStretch.SemiExpanded,
        7 => FontStretch.Expanded,
        8 => FontStretch.ExtraExpanded,
        9 => FontStretch.UltraExpanded,
        _ => FontStretch.Normal,
    };

    private static ushort ReadUInt16BE(ReadOnlySpan<byte> span)
    {
        if (span.Length < 2)
        {
            return 0;
        }

        return (ushort)((span[0] << 8) | span[1]);
    }

    private static uint ReadUInt32BE(ReadOnlySpan<byte> span)
    {
        if (span.Length < 4)
        {
            return 0;
        }

        return ((uint)span[0] << 24) | ((uint)span[1] << 16) | ((uint)span[2] << 8) | span[3];
    }

    private static uint MakeTag(string value)
    {
        if (value is null || value.Length != 4)
        {
            throw new ArgumentException("Tag value must be four characters long.", nameof(value));
        }

        return ((uint)value[0] << 24) | ((uint)value[1] << 16) | ((uint)value[2] << 8) | value[3];
    }

    private readonly record struct FontMetadata(string FamilyName, FontStyle Style, FontWeight Weight, FontStretch Stretch);
}
