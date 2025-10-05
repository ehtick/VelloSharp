using System;
using System.Drawing;
using System.Globalization;
using VelloSharp;
using VelloSharp.Text;

namespace VelloSharp.WinForms;

public sealed class VelloFont : IDisposable
{
    private bool _disposed;

    public VelloFont(string familyName, float size, FontStyle style = FontStyle.Regular, CultureInfo? culture = null)
    {
        if (string.IsNullOrWhiteSpace(familyName))
        {
            throw new ArgumentException("Family name cannot be null or whitespace.", nameof(familyName));
        }

        if (size <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        FamilyName = familyName;
        Size = size;
        Style = style;
        Culture = culture ?? CultureInfo.CurrentCulture;

        var result = LoadFontFace(familyName, style, Culture);
        CoreFont = result.Font;
        FaceIndex = result.FaceIndex;
        Weight = result.Weight;
        ResolvedFamilyName = result.ResolvedFamilyName;
    }

    private VelloFont(Font font, string familyName, float size, FontStyle style, CultureInfo culture, float weight, uint faceIndex, string resolvedFamily)
    {
        CoreFont = font ?? throw new ArgumentNullException(nameof(font));
        FamilyName = familyName;
        Size = size;
        Style = style;
        Culture = culture;
        Weight = weight;
        FaceIndex = faceIndex;
        ResolvedFamilyName = resolvedFamily;
    }

    public Font CoreFont { get; }

    public string FamilyName { get; }

    public string ResolvedFamilyName { get; }

    public float Size { get; }

    public FontStyle Style { get; }

    public CultureInfo Culture { get; }

    public float Weight { get; }

    public uint FaceIndex { get; }

    public static VelloFont FromSystemFont(System.Drawing.Font font)
    {
        ArgumentNullException.ThrowIfNull(font);
        var size = (float)font.SizeInPoints;
        if (!float.IsFinite(size) || size <= 0f)
        {
            size = font.Size;
        }

        var culture = CultureInfo.CurrentCulture;
        var result = LoadFontFace(font.FontFamily.Name, font.Style, culture);
        return new VelloFont(result.Font, font.FontFamily.Name, size, font.Style, culture, result.Weight, result.FaceIndex, result.ResolvedFamilyName);
    }

    public static VelloFont FromFamily(string familyName, float size, bool bold = false, bool italic = false, CultureInfo? culture = null)
    {
        var style = FontStyle.Regular;
        if (bold)
        {
            style |= FontStyle.Bold;
        }
        if (italic)
        {
            style |= FontStyle.Italic;
        }

        return new VelloFont(familyName, size, style, culture);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CoreFont.Dispose();
        _disposed = true;
    }

    internal bool TryGetGlyphMetrics(uint codePoint, out GlyphMetrics metrics)
    {
        ThrowIfDisposed();
        metrics = default;
        if (!CoreFont.TryGetGlyphIndex(codePoint, out var glyphId))
        {
            return false;
        }

        return CoreFont.TryGetGlyphMetrics(glyphId, Size, out metrics);
    }

    private static FontLoadResult LoadFontFace(string familyName, FontStyle style, CultureInfo culture)
    {
        var service = ParleyFontService.Instance;
        var query = new ParleyFontQuery(
            familyName,
            ToFontWeight(style),
            Stretch: 1f,
            ToFontStyle(style),
            culture);

        if (!service.TryLoadTypeface(query, out var info))
        {
            var fallback = service.GetDefaultFamilyName();
            query = query with { FamilyName = fallback };
            if (!service.TryLoadTypeface(query, out info))
            {
                throw new InvalidOperationException($"Unable to load font '{familyName}'.");
            }
        }

        var resolvedFamily = string.IsNullOrWhiteSpace(info.FamilyName) ? familyName : info.FamilyName;
        var font = Font.Load(info.FontData, info.FaceIndex);
        return new FontLoadResult(font, info.FaceIndex, query.Weight, resolvedFamily);
    }

    private static float ToFontWeight(FontStyle style)
        => (style & FontStyle.Bold) != 0 ? 700f : 400f;

    private static VelloFontStyle ToFontStyle(FontStyle style)
        => (style & FontStyle.Italic) != 0 ? VelloFontStyle.Italic : VelloFontStyle.Normal;

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VelloFont));
        }
    }

    private readonly record struct FontLoadResult(Font Font, uint FaceIndex, float Weight, string ResolvedFamilyName);
}

