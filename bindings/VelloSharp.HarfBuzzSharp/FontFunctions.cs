using System;

namespace HarfBuzzSharp;

public delegate bool FontExtentsDelegate(Font font, object? fontData, out FontExtents extents);
public delegate bool NominalGlyphDelegate(Font font, object? fontData, uint unicode, out uint glyph);
public delegate uint NominalGlyphsDelegate(Font font, object? fontData, uint count, ReadOnlySpan<uint> codepoints, Span<uint> glyphs);
public delegate bool VariationGlyphDelegate(Font font, object? fontData, uint unicode, uint variationSelector, out uint glyph);
public delegate int GlyphAdvanceDelegate(Font font, object? fontData, uint glyph);
public delegate void GlyphAdvancesDelegate(Font font, object? fontData, uint count, ReadOnlySpan<uint> glyphs, Span<int> advances);
public delegate bool GlyphOriginDelegate(Font font, object? fontData, uint glyph, out int x, out int y);
public delegate int GlyphKerningDelegate(Font font, object? fontData, uint firstGlyph, uint secondGlyph);
public delegate bool GlyphExtentsDelegate(Font font, object? fontData, uint glyph, out GlyphExtents extents);
public delegate bool GlyphContourPointDelegate(Font font, object? fontData, uint glyph, uint pointIndex, out int x, out int y);
public delegate bool GlyphNameDelegate(Font font, object? fontData, uint glyph, out string name);
public delegate bool GlyphFromNameDelegate(Font font, object? fontData, string name, out uint glyph);

public sealed class FontFunctions : IDisposable
{
    private static readonly Lazy<FontFunctions> LazyEmpty = new(() => new FontFunctions(isImmutable: true));

    private bool _isImmutable;

    private FontExtentsDelegate? _horizontalExtents;
    private FontExtentsDelegate? _verticalExtents;
    private NominalGlyphDelegate? _nominalGlyph;
    private NominalGlyphsDelegate? _nominalGlyphs;
    private VariationGlyphDelegate? _variationGlyph;
    private GlyphAdvanceDelegate? _horizontalAdvance;
    private GlyphAdvanceDelegate? _verticalAdvance;
    private GlyphAdvancesDelegate? _horizontalAdvances;
    private GlyphAdvancesDelegate? _verticalAdvances;
    private GlyphOriginDelegate? _horizontalOrigin;
    private GlyphOriginDelegate? _verticalOrigin;
    private GlyphKerningDelegate? _horizontalKerning;
    private GlyphExtentsDelegate? _glyphExtents;
    private GlyphContourPointDelegate? _glyphContourPoint;
    private GlyphNameDelegate? _glyphName;
    private GlyphFromNameDelegate? _glyphFromName;

    private ReleaseDelegate? _horizontalExtentsDestroy;
    private ReleaseDelegate? _verticalExtentsDestroy;
    private ReleaseDelegate? _nominalGlyphDestroy;
    private ReleaseDelegate? _nominalGlyphsDestroy;
    private ReleaseDelegate? _variationGlyphDestroy;
    private ReleaseDelegate? _horizontalAdvanceDestroy;
    private ReleaseDelegate? _verticalAdvanceDestroy;
    private ReleaseDelegate? _horizontalAdvancesDestroy;
    private ReleaseDelegate? _verticalAdvancesDestroy;
    private ReleaseDelegate? _horizontalOriginDestroy;
    private ReleaseDelegate? _verticalOriginDestroy;
    private ReleaseDelegate? _horizontalKerningDestroy;
    private ReleaseDelegate? _glyphExtentsDestroy;
    private ReleaseDelegate? _glyphContourPointDestroy;
    private ReleaseDelegate? _glyphNameDestroy;
    private ReleaseDelegate? _glyphFromNameDestroy;

    public FontFunctions()
    {
        _isImmutable = false;
    }

    private FontFunctions(bool isImmutable)
    {
        _isImmutable = isImmutable;
    }

    public static FontFunctions Empty => LazyEmpty.Value;

    public bool IsImmutable => _isImmutable;

    public FontExtentsDelegate? HorizontalExtents => _horizontalExtents;
    public FontExtentsDelegate? VerticalExtents => _verticalExtents;
    public NominalGlyphDelegate? NominalGlyph => _nominalGlyph;
    public NominalGlyphsDelegate? NominalGlyphs => _nominalGlyphs;
    public VariationGlyphDelegate? VariationGlyph => _variationGlyph;
    public GlyphAdvanceDelegate? HorizontalAdvance => _horizontalAdvance;
    public GlyphAdvanceDelegate? VerticalAdvance => _verticalAdvance;
    public GlyphAdvancesDelegate? HorizontalAdvances => _horizontalAdvances;
    public GlyphAdvancesDelegate? VerticalAdvances => _verticalAdvances;
    public GlyphOriginDelegate? HorizontalOrigin => _horizontalOrigin;
    public GlyphOriginDelegate? VerticalOrigin => _verticalOrigin;
    public GlyphKerningDelegate? HorizontalKerning => _horizontalKerning;
    public GlyphExtentsDelegate? GlyphExtents => _glyphExtents;
    public GlyphContourPointDelegate? GlyphContourPoint => _glyphContourPoint;
    public GlyphNameDelegate? GlyphName => _glyphName;
    public GlyphFromNameDelegate? GlyphFromName => _glyphFromName;

    public void MakeImmutable()
    {
        _isImmutable = true;
    }

    public void SetHorizontalFontExtentsDelegate(FontExtentsDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _horizontalExtents, ref _horizontalExtentsDestroy, del, destroy);

    public void SetVerticalFontExtentsDelegate(FontExtentsDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _verticalExtents, ref _verticalExtentsDestroy, del, destroy);

    public void SetNominalGlyphDelegate(NominalGlyphDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _nominalGlyph, ref _nominalGlyphDestroy, del, destroy);

    public void SetNominalGlyphsDelegate(NominalGlyphsDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _nominalGlyphs, ref _nominalGlyphsDestroy, del, destroy);

    public void SetVariationGlyphDelegate(VariationGlyphDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _variationGlyph, ref _variationGlyphDestroy, del, destroy);

    public void SetHorizontalGlyphAdvanceDelegate(GlyphAdvanceDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _horizontalAdvance, ref _horizontalAdvanceDestroy, del, destroy);

    public void SetVerticalGlyphAdvanceDelegate(GlyphAdvanceDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _verticalAdvance, ref _verticalAdvanceDestroy, del, destroy);

    public void SetHorizontalGlyphAdvancesDelegate(GlyphAdvancesDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _horizontalAdvances, ref _horizontalAdvancesDestroy, del, destroy);

    public void SetVerticalGlyphAdvancesDelegate(GlyphAdvancesDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _verticalAdvances, ref _verticalAdvancesDestroy, del, destroy);

    public void SetHorizontalGlyphOriginDelegate(GlyphOriginDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _horizontalOrigin, ref _horizontalOriginDestroy, del, destroy);

    public void SetVerticalGlyphOriginDelegate(GlyphOriginDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _verticalOrigin, ref _verticalOriginDestroy, del, destroy);

    public void SetHorizontalGlyphKerningDelegate(GlyphKerningDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _horizontalKerning, ref _horizontalKerningDestroy, del, destroy);

    public void SetGlyphExtentsDelegate(GlyphExtentsDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _glyphExtents, ref _glyphExtentsDestroy, del, destroy);

    public void SetGlyphContourPointDelegate(GlyphContourPointDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _glyphContourPoint, ref _glyphContourPointDestroy, del, destroy);

    public void SetGlyphNameDelegate(GlyphNameDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _glyphName, ref _glyphNameDestroy, del, destroy);

    public void SetGlyphFromNameDelegate(GlyphFromNameDelegate del, ReleaseDelegate? destroy = null)
        => SetDelegate(ref _glyphFromName, ref _glyphFromNameDestroy, del, destroy);

    public void Dispose()
    {
        DisposeDelegate(ref _horizontalExtentsDestroy);
        DisposeDelegate(ref _verticalExtentsDestroy);
        DisposeDelegate(ref _nominalGlyphDestroy);
        DisposeDelegate(ref _nominalGlyphsDestroy);
        DisposeDelegate(ref _variationGlyphDestroy);
        DisposeDelegate(ref _horizontalAdvanceDestroy);
        DisposeDelegate(ref _verticalAdvanceDestroy);
        DisposeDelegate(ref _horizontalAdvancesDestroy);
        DisposeDelegate(ref _verticalAdvancesDestroy);
        DisposeDelegate(ref _horizontalOriginDestroy);
        DisposeDelegate(ref _verticalOriginDestroy);
        DisposeDelegate(ref _horizontalKerningDestroy);
        DisposeDelegate(ref _glyphExtentsDestroy);
        DisposeDelegate(ref _glyphContourPointDestroy);
        DisposeDelegate(ref _glyphNameDestroy);
        DisposeDelegate(ref _glyphFromNameDestroy);

        _horizontalExtents = null;
        _verticalExtents = null;
        _nominalGlyph = null;
        _nominalGlyphs = null;
        _variationGlyph = null;
        _horizontalAdvance = null;
        _verticalAdvance = null;
        _horizontalAdvances = null;
        _verticalAdvances = null;
        _horizontalOrigin = null;
        _verticalOrigin = null;
        _horizontalKerning = null;
        _glyphExtents = null;
        _glyphContourPoint = null;
        _glyphName = null;
        _glyphFromName = null;
    }

    private void SetDelegate<TDelegate>(
        ref TDelegate? target,
        ref ReleaseDelegate? destroyField,
        TDelegate del,
        ReleaseDelegate? destroy)
        where TDelegate : class
    {
        if (del is null)
        {
            throw new ArgumentNullException(nameof(del));
        }

        if (_isImmutable)
        {
            throw new InvalidOperationException("FontFunctions has been marked immutable.");
        }

        DisposeDelegate(ref destroyField);
        target = del;
        destroyField = destroy;
    }

    private static void DisposeDelegate(ref ReleaseDelegate? destroy)
    {
        try
        {
            destroy?.Invoke();
        }
        finally
        {
            destroy = null;
        }
    }
}

