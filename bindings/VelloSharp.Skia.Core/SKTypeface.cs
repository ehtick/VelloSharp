using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using SkiaSharp.IO;
using VelloSharp;

namespace SkiaSharp;

public sealed class SKTypeface : IDisposable
{
    private readonly Font _font;
    private readonly byte[] _fontData;
    private readonly Dictionary<uint, (int Offset, int Length)> _tables;
    private readonly bool _ownsFont;
    private readonly string _familyName;
    private readonly SKFontStyle _fontStyle;
    private readonly int _unitsPerEm;

    private SKTypeface(Font font, byte[] fontData, bool ownsFont, string? familyName = null, SKFontStyle? fontStyle = null)
    {
        _font = font ?? throw new ArgumentNullException(nameof(font));
        _fontData = fontData ?? throw new ArgumentNullException(nameof(fontData));
        _ownsFont = ownsFont;
        _tables = ParseTableDirectory(_fontData);
        _familyName = familyName ?? ParseFamilyName(_fontData, _tables) ?? "Unknown";
        _fontStyle = fontStyle ?? SKFontStyle.Normal;
        _unitsPerEm = ParseUnitsPerEm(_fontData, _tables);
    }

    public static SKTypeface Default => s_default.Value;

    private static readonly Lazy<SKTypeface> s_default = new(CreateDefault);

    public string FamilyName => _familyName;
    public SKFontStyle FontStyle => _fontStyle;
    public bool IsBold => _fontStyle.Weight >= SKFontStyleWeight.Bold;
    public bool IsItalic => _fontStyle.Slant != SKFontStyleSlant.Upright;
    public int UnitsPerEm => _unitsPerEm;

    internal Font Font => _font;

    public void Dispose()
    {
        if (_ownsFont)
        {
            _font.Dispose();
        }
    }

    public static SKTypeface FromStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var data = ms.ToArray();
        var font = Font.Load(data);
        return new SKTypeface(font, data, ownsFont: true);
    }

    internal static SKTypeface FromFontData(byte[] fontData, uint faceIndex, string? familyName, SKFontStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(fontData);
        var font = Font.Load(fontData, faceIndex);
        return new SKTypeface(font, fontData, ownsFont: true, familyName, style);
    }

    internal static SKTypeface FromFont(Font font, byte[] fontData, bool ownsFont = false, string? familyName = null)
        => new(font, fontData, ownsFont, familyName);

    public int GetTableSize(uint tag)
    {
        return _tables.TryGetValue(tag, out var entry) ? entry.Length : 0;
    }

    public bool TryGetTableData(uint tag, int offset, int length, IntPtr destination)
    {
        if (destination == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        if (!_tables.TryGetValue(tag, out var entry))
        {
            return false;
        }

        if (offset < 0 || length < 0 || offset + length > entry.Length)
        {
            return false;
        }

        Marshal.Copy(_fontData, entry.Offset + offset, destination, length);
        return true;
    }

    public bool TryGetTableData(uint tag, out byte[] table)
    {
        if (!_tables.TryGetValue(tag, out var entry))
        {
            table = Array.Empty<byte>();
            return false;
        }

        table = new byte[entry.Length];
        Array.Copy(_fontData, entry.Offset, table, 0, entry.Length);
        return true;
    }

    public SKStreamAsset OpenStream() => new SKStreamAsset(_fontData);

    private static SKTypeface CreateDefault()
    {
        var assembly = typeof(SKTypeface).GetTypeInfo().Assembly;
        using var stream = assembly.GetManifestResourceStream("VelloSharp.Skia.Core.Fonts.Roboto-Regular.ttf")
            ?? throw new InvalidOperationException("Embedded default font 'Roboto-Regular.ttf' was not found.");

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var data = ms.ToArray();
        var font = Font.Load(data);
        return new SKTypeface(font, data, ownsFont: false, familyName: "Roboto");
    }

    private static Dictionary<uint, (int Offset, int Length)> ParseTableDirectory(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12)
        {
            return new Dictionary<uint, (int, int)>();
        }

        var numTables = ReadUInt16BE(data.Slice(4, 2));
        var tables = new Dictionary<uint, (int, int)>(numTables);
        var recordOffset = 12;
        const int recordSize = 16;

        for (var i = 0; i < numTables; i++)
        {
            var entryStart = recordOffset + i * recordSize;
            if (entryStart + recordSize > data.Length)
            {
                break;
            }

            var tag = ReadUInt32BE(data.Slice(entryStart, 4));
            var offset = ReadUInt32BE(data.Slice(entryStart + 8, 4));
            var length = ReadUInt32BE(data.Slice(entryStart + 12, 4));

            if (offset + length > data.Length)
            {
                continue;
            }

            tables[tag] = ((int)offset, (int)length);
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
            var encodingId = ReadUInt16BE(record.Slice(2, 2));
            _ = encodingId;
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

    private static int ParseUnitsPerEm(ReadOnlySpan<byte> data, IReadOnlyDictionary<uint, (int Offset, int Length)> tables)
    {
        if (!tables.TryGetValue(MakeTag("head"), out var entry))
        {
            return 2048;
        }

        if (entry.Length < 20)
        {
            return 2048;
        }

        var span = data.Slice(entry.Offset, entry.Length);
        return ReadUInt16BE(span.Slice(18, 2));
    }

    private static ushort ReadUInt16BE(ReadOnlySpan<byte> span)
        => (ushort)((span[0] << 8) | span[1]);

    private static uint ReadUInt32BE(ReadOnlySpan<byte> span)
        => ((uint)span[0] << 24) | ((uint)span[1] << 16) | ((uint)span[2] << 8) | span[3];

    private static uint MakeTag(string value)
    {
        if (value is null || value.Length != 4)
        {
            throw new ArgumentException("Tag value must be four characters long.", nameof(value));
        }

        return ((uint)value[0] << 24) | ((uint)value[1] << 16) | ((uint)value[2] << 8) | value[3];
    }
}
