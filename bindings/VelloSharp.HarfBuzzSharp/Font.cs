using System;
using System.Collections.Generic;
using System.Globalization;

namespace HarfBuzzSharp;

public sealed class Font : NativeObject
{
    private readonly Face _face;
    private readonly Font? _parent;
    private readonly global::VelloSharp.Font? _ownedFont;
    private FontFunctions? _fontFunctions;
    private object? _fontFunctionsData;
    private ReleaseDelegate? _fontFunctionsDestroy;
    private int _scaleX;
    private int _scaleY;
    private FontVariation[] _variationSettings = Array.Empty<FontVariation>();
    private List<global::VelloSharp.Text.VelloVariationAxisValue>? _cachedVariationOptions;

    public Font(Face face, IntPtr fontHandle, int unitsPerEm)
        : base(fontHandle)
    {
        _face = face ?? throw new ArgumentNullException(nameof(face));
        _scaleX = unitsPerEm == 0 ? 2048 : unitsPerEm;
        _scaleY = _scaleX;
        OpenTypeMetrics = new OpenTypeMetrics(this);
    }

    public Font(Face face)
        : base(IntPtr.Zero)
    {
        _face = face ?? throw new ArgumentNullException(nameof(face));
        _ownedFont = TryCreateVelloFont(face);
        Handle = _ownedFont?.Handle ?? IntPtr.Zero;
        var unitsPerEm = face.UnitsPerEm == 0 ? 2048 : face.UnitsPerEm;
        _scaleX = unitsPerEm;
        _scaleY = unitsPerEm;
        OpenTypeMetrics = new OpenTypeMetrics(this);
    }

    public Font(Font parent)
        : base(parent?.Handle ?? IntPtr.Zero)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _face = parent._face;
        _scaleX = parent._scaleX;
        _scaleY = parent._scaleY;
        _variationSettings = parent._variationSettings.Length == 0
            ? Array.Empty<FontVariation>()
            : (FontVariation[])parent._variationSettings.Clone();
        if (parent._cachedVariationOptions is { Count: > 0 })
        {
            _cachedVariationOptions = new List<global::VelloSharp.Text.VelloVariationAxisValue>(parent._cachedVariationOptions);
        }
        OpenTypeMetrics = new OpenTypeMetrics(this);
    }

    public Font? Parent => _parent;

    public OpenTypeMetrics OpenTypeMetrics { get; }
    public ReadOnlySpan<FontVariation> Variations => _variationSettings;

    public void SetFontFunctions(FontFunctions fontFunctions) =>
        SetFontFunctions(fontFunctions, null, null);

    public void SetFontFunctions(FontFunctions fontFunctions, object? fontData) =>
        SetFontFunctions(fontFunctions, fontData, null);

    public void SetFontFunctions(FontFunctions fontFunctions, object? fontData, ReleaseDelegate? destroy)
    {
        _fontFunctionsDestroy?.Invoke();
        _fontFunctionsDestroy = destroy;
        _fontFunctions = fontFunctions ?? throw new ArgumentNullException(nameof(fontFunctions));
        if (!_fontFunctions.IsImmutable)
        {
            _fontFunctions.MakeImmutable();
        }
        _fontFunctionsData = fontData;
    }

    public void SetScale(int xScale, int yScale)
    {
        _scaleX = xScale;
        _scaleY = yScale;
    }

    public void GetScale(out int xScale, out int yScale)
    {
        xScale = _scaleX;
        yScale = _scaleY;
    }

    public void SetVariations(params FontVariation[] variations)
        => SetVariations(variations.AsSpan());

    public void SetVariations(ReadOnlySpan<FontVariation> variations)
    {
        if (variations.IsEmpty)
        {
            _variationSettings = Array.Empty<FontVariation>();
            _cachedVariationOptions = null;
            return;
        }

        var axes = _face.VariationAxes;
        var result = new List<FontVariation>(variations.Length);
        for (var i = 0; i < variations.Length; i++)
        {
            var variation = variations[i];
            if (variation.Tag.Equals(Tag.None))
            {
                continue;
            }

            var value = variation.Value;
            for (var axisIndex = 0; axisIndex < axes.Length; axisIndex++)
            {
                var axis = axes[axisIndex];
                if (!axis.Tag.Equals(variation.Tag))
                {
                    continue;
                }

                value = Math.Clamp(value, axis.MinValue, axis.MaxValue);
                break;
            }

            var updated = new FontVariation(variation.Tag, value);
            var existingIndex = result.FindIndex(v => v.Tag.Equals(updated.Tag));
            if (existingIndex >= 0)
            {
                result[existingIndex] = updated;
            }
            else
            {
                result.Add(updated);
            }
        }

        _variationSettings = result.Count == 0
            ? Array.Empty<FontVariation>()
            : result.ToArray();
        _cachedVariationOptions = null;
    }

    public void SetFunctionsOpenType()
    {
        // Metrics queries are routed through Vello; no managed setup required.
    }

    public bool TryGetGlyphExtents(ushort glyph, out GlyphExtents extents)
    {
        if (_fontFunctions?.GlyphExtents is { } custom && custom(this, _fontFunctionsData, glyph, out extents))
        {
            return true;
        }

        if (Handle == IntPtr.Zero)
        {
            extents = default;
            return false;
        }

        if (global::VelloSharp.NativeMethods.vello_font_get_glyph_metrics(Handle, glyph, _scaleX, out var metrics) != global::VelloSharp.VelloStatus.Success)
        {
            extents = default;
            return false;
        }

        extents = new GlyphExtents
        {
            XBearing = metrics.XBearing,
            YBearing = metrics.YBearing,
            Width = metrics.Width,
            Height = metrics.Height,
        };
        return true;
    }

    public bool TryGetGlyph(uint codepoint, out uint glyph)
        => TryGetGlyph(codepoint, 0, out glyph);

    public bool TryGetGlyph(uint codepoint, uint variationSelector, out uint glyph)
    {
        if (_fontFunctions?.VariationGlyph is { } variation)
        {
            if (variation(this, _fontFunctionsData, codepoint, variationSelector, out glyph))
            {
                return glyph != 0;
            }
        }

        if (_fontFunctions?.NominalGlyph is { } single && variationSelector == 0)
        {
            if (single(this, _fontFunctionsData, codepoint, out glyph))
            {
                return glyph != 0;
            }
        }

        if (_fontFunctions?.NominalGlyphs is { } multiple && variationSelector == 0)
        {
            Span<uint> glyphBuffer = stackalloc uint[1];
            Span<uint> codeBuffer = stackalloc uint[1];
            codeBuffer[0] = codepoint;
            var produced = multiple(this, _fontFunctionsData, 1, codeBuffer, glyphBuffer);
            if (produced > 0)
            {
                glyph = glyphBuffer[0];
                return glyph != 0;
            }
        }

        glyph = 0;
        if (Handle == IntPtr.Zero)
        {
            return false;
        }

        if (global::VelloSharp.NativeMethods.vello_font_get_glyph_index(Handle, codepoint, out var mapped) != global::VelloSharp.VelloStatus.Success)
        {
            return false;
        }

        glyph = mapped;
        return glyph != 0;
    }

    public bool TryGetGlyph(int unicode, out uint glyph)
        => TryGetGlyph((uint)unicode, out glyph);

    public bool TryGetGlyph(int unicode, uint variationSelector, out uint glyph)
        => TryGetGlyph((uint)unicode, variationSelector, out glyph);

    public bool TryGetVariationGlyph(uint unicode, out uint glyph)
        => TryGetGlyph(unicode, 0, out glyph);

    public bool TryGetVariationGlyph(int unicode, out uint glyph)
        => TryGetGlyph((uint)unicode, 0, out glyph);

    public bool TryGetVariationGlyph(uint unicode, uint variationSelector, out uint glyph)
        => TryGetGlyph(unicode, variationSelector, out glyph);

    public bool TryGetVariationGlyph(int unicode, uint variationSelector, out uint glyph)
        => TryGetGlyph((uint)unicode, variationSelector, out glyph);

    public int GetHorizontalGlyphAdvance(ushort glyph)
    {
        if (_fontFunctions?.HorizontalAdvance is { } custom)
        {
            return custom(this, _fontFunctionsData, glyph);
        }

        if (Handle == IntPtr.Zero)
        {
            return 0;
        }

        if (global::VelloSharp.NativeMethods.vello_font_get_glyph_metrics(Handle, glyph, _scaleX, out var metrics) != global::VelloSharp.VelloStatus.Success)
        {
            return 0;
        }

        return (int)MathF.Round(metrics.Advance);
    }

    public int[] GetHorizontalGlyphAdvances(ReadOnlySpan<uint> glyphs)
    {
        if (_fontFunctions?.HorizontalAdvances is { } custom)
        {
            Span<int> advances = glyphs.Length <= 16 ? stackalloc int[glyphs.Length] : new int[glyphs.Length];
            custom(this, _fontFunctionsData, (uint)glyphs.Length, glyphs, advances);
            return advances.ToArray();
        }

        var result = new int[glyphs.Length];
        for (var i = 0; i < glyphs.Length; i++)
        {
            result[i] = GetHorizontalGlyphAdvance((ushort)glyphs[i]);
        }

        return result;
    }

    public FontExtents GetFontExtentsForDirection(Direction direction)
    {
        if (direction == Direction.LeftToRight || direction == Direction.RightToLeft)
        {
            if (_fontFunctions?.HorizontalExtents is { } horizontal && horizontal(this, _fontFunctionsData, out var extents))
            {
                return extents;
            }
        }
        else if (direction == Direction.TopToBottom || direction == Direction.BottomToTop)
        {
            if (_fontFunctions?.VerticalExtents is { } vertical && vertical(this, _fontFunctionsData, out var extents))
            {
                return extents;
            }
        }

        if (Handle == IntPtr.Zero)
        {
            return default;
        }

        if (global::VelloSharp.NativeMethods.vello_font_get_metrics(Handle, _scaleX, out var metrics) != global::VelloSharp.VelloStatus.Success)
        {
            return default;
        }

        return new FontExtents
        {
            Ascender = -metrics.Ascent,
            Descender = -metrics.Descent,
            LineGap = metrics.Leading,
        };
    }

    public void Shape(Buffer buffer, Feature[]? features = null)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (Handle == IntPtr.Zero)
        {
            buffer.PopulateFallback(Handle, _scaleX);
            return;
        }

        buffer.ApplyUnicodeProcessing();

        if (buffer.Length == 0 || buffer.TextLength == 0)
        {
            buffer.ClearContents();
            return;
        }

        var velloFeatures = ConvertFeatures(features);
        var variations = GetVariationOptions();
        var options = new global::VelloSharp.Text.VelloTextShaperOptions(
            FontSize: _scaleX,
            IsRightToLeft: buffer.Direction == Direction.RightToLeft,
            LetterSpacing: 0f,
            Features: velloFeatures,
            VariationAxes: variations,
            EnableScriptSegmentation: true,
            Culture: null);

        var glyphs = global::VelloSharp.Text.VelloTextShaperCore.ShapeUtf16(
            Handle,
            buffer.TextSpan,
            options);

        if (glyphs.Count == 0)
        {
            buffer.PopulateFallback(Handle, _scaleX);
            return;
        }

        buffer.SetLength(glyphs.Count);
        buffer.ContentType = ContentType.Glyphs;

        for (var i = 0; i < glyphs.Count; i++)
        {
            var glyph = glyphs[i];
            var cluster = buffer.ClusterOffset + (int)glyph.Cluster;
            buffer.SetGlyph(i, glyph.GlyphId, (uint)cluster);
            buffer.SetPosition(i, glyph.XAdvance, glyph.YAdvance, glyph.XOffset, glyph.YOffset);
        }
    }

    public int GetKerning(uint leftGlyph, uint rightGlyph)
    {
        if (_fontFunctions?.HorizontalKerning is { } custom)
        {
            return custom(this, _fontFunctionsData, leftGlyph, rightGlyph);
        }

        return 0;
    }

    public bool TryGetGlyphHorizontalOrigin(uint glyph, out int x, out int y)
    {
        if (_fontFunctions?.HorizontalOrigin is { } custom)
        {
            return custom(this, _fontFunctionsData, glyph, out x, out y);
        }

        x = 0;
        y = 0;
        return false;
    }

    public bool TryGetGlyphVerticalOrigin(uint glyph, out int x, out int y)
    {
        if (_fontFunctions?.VerticalOrigin is { } custom)
        {
            return custom(this, _fontFunctionsData, glyph, out x, out y);
        }

        x = 0;
        y = 0;
        return false;
    }

    public bool TryGetGlyphContourPoint(uint glyph, uint pointIndex, out int x, out int y)
    {
        if (_fontFunctions?.GlyphContourPoint is { } custom)
        {
            return custom(this, _fontFunctionsData, glyph, pointIndex, out x, out y);
        }

        x = 0;
        y = 0;
        return false;
    }

    public bool TryGetGlyphName(uint glyph, out string name)
    {
        if (_fontFunctions?.GlyphName is { } custom && custom(this, _fontFunctionsData, glyph, out name))
        {
            return !string.IsNullOrEmpty(name);
        }

        name = string.Empty;
        return false;
    }

    public bool TryGetGlyphFromName(string name, out uint glyph)
    {
        if (string.IsNullOrEmpty(name))
        {
            glyph = 0;
            return false;
        }

        if (_fontFunctions?.GlyphFromName is { } custom)
        {
            return custom(this, _fontFunctionsData, name, out glyph);
        }

        glyph = 0;
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _fontFunctionsDestroy?.Invoke();
            _fontFunctionsDestroy = null;
            _fontFunctions = null;
            _fontFunctionsData = null;
            _variationSettings = Array.Empty<FontVariation>();
            _cachedVariationOptions?.Clear();
            _cachedVariationOptions = null;
        }
    }

    protected override void DisposeHandler()
    {
        _ownedFont?.Dispose();
    }

    private static global::VelloSharp.Font? TryCreateVelloFont(Face face)
    {
        if (face.Blob is { } blob)
        {
            var data = blob.AsSpan();
            if (!data.IsEmpty)
            {
                return global::VelloSharp.Font.Load(data.ToArray(), (uint)Math.Max(0, face.Index));
            }
        }

        return null;
    }

    private static IReadOnlyList<global::VelloSharp.Text.VelloOpenTypeFeature>? ConvertFeatures(IReadOnlyList<Feature>? features)
    {
        if (features is null || features.Count == 0)
        {
            return null;
        }

        var list = new List<global::VelloSharp.Text.VelloOpenTypeFeature>(features.Count);
        for (var i = 0; i < features.Count; i++)
        {
            var feature = features[i];
            list.Add(new global::VelloSharp.Text.VelloOpenTypeFeature(
                feature.Tag.ToString().PadRight(4).Substring(0, 4),
                (int)feature.Value,
                feature.Start,
                feature.End));
        }

        return list;
    }

    internal IReadOnlyList<global::VelloSharp.Text.VelloVariationAxisValue>? GetVariationOptions()
    {
        if (_variationSettings.Length == 0)
        {
            _cachedVariationOptions = null;
            return null;
        }

        if (_cachedVariationOptions is null)
        {
            _cachedVariationOptions = new List<global::VelloSharp.Text.VelloVariationAxisValue>(_variationSettings.Length);
            for (var i = 0; i < _variationSettings.Length; i++)
            {
                var variation = _variationSettings[i];
                var tagString = variation.Tag.ToString();
                if (string.Equals(tagString, nameof(Tag.None), StringComparison.Ordinal))
                {
                    continue;
                }

                if (tagString.Length != 4)
                {
                    tagString = tagString.PadRight(4).Substring(0, 4);
                }

                _cachedVariationOptions.Add(new global::VelloSharp.Text.VelloVariationAxisValue(tagString, variation.Value));
            }

            if (_cachedVariationOptions.Count == 0)
            {
                _cachedVariationOptions = null;
                return null;
            }
        }

        return _cachedVariationOptions;
    }
}


