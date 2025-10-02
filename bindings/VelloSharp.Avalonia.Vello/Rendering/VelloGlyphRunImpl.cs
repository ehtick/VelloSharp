using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;
using VelloSharp;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal sealed class VelloGlyphRunImpl : IGlyphRunImpl
{
    private readonly Glyph[] _glyphs;
    private readonly IReadOnlyList<GlyphInfo> _glyphInfos;

    public VelloGlyphRunImpl(IGlyphTypeface glyphTypeface, double fontRenderingEmSize, IReadOnlyList<GlyphInfo> glyphInfos, Point baselineOrigin)
    {
        GlyphTypeface = glyphTypeface ?? throw new ArgumentNullException(nameof(glyphTypeface));
        FontRenderingEmSize = fontRenderingEmSize;
        _glyphInfos = glyphInfos ?? throw new ArgumentNullException(nameof(glyphInfos));
        BaselineOrigin = baselineOrigin;

        _glyphs = BuildGlyphs(glyphInfos, baselineOrigin, out var bounds);
        Bounds = bounds;
    }

    public IGlyphTypeface GlyphTypeface { get; }

    public double FontRenderingEmSize { get; }

    public Point BaselineOrigin { get; }

    public Rect Bounds { get; }

    internal ReadOnlySpan<Glyph> GlyphsSpan => _glyphs;

    internal IReadOnlyList<GlyphInfo> GlyphInfos => _glyphInfos;

    public void Dispose()
    {
        // No unmanaged resources to release yet.
    }

    public IReadOnlyList<float> GetIntersections(float lowerLimit, float upperLimit)
    {
        return Array.Empty<float>();
    }

    private Glyph[] BuildGlyphs(IReadOnlyList<GlyphInfo> glyphInfos, Point baselineOrigin, out Rect bounds)
    {
        var glyphs = new Glyph[glyphInfos.Count];

        if (glyphInfos.Count == 0)
        {
            bounds = new Rect(baselineOrigin, new Size(0, 0));
            return glyphs;
        }

        var font = VelloFontManager.GetFont(GlyphTypeface);

        var currentX = 0.0;

        double minX = double.PositiveInfinity;
        double minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity;
        double maxY = double.NegativeInfinity;

        var simulations = (GlyphTypeface as VelloGlyphTypeface)?.FontSimulations ?? FontSimulations.None;

        for (var i = 0; i < glyphInfos.Count; i++)
        {
            var glyph = glyphInfos[i];
            var offset = glyph.GlyphOffset;

            var x = currentX + offset.X;
            var offsetY = offset.Y;
            var y = -offsetY;

            glyphs[i] = new Glyph(glyph.GlyphIndex, (float)x, (float)y);

            if (VelloFontManager.TryGetGlyphMetrics(font, (ushort)glyph.GlyphIndex, FontRenderingEmSize, out var velloMetrics))
            {
                var left = baselineOrigin.X + x + velloMetrics.XBearing;
                var top = baselineOrigin.Y + offsetY - velloMetrics.YBearing;
                var right = left + velloMetrics.Width;
                var bottom = top + velloMetrics.Height;

                if (left < minX)
                {
                    minX = left;
                }

                if (top < minY)
                {
                    minY = top;
                }

                if (right > maxX)
                {
                    maxX = right;
                }

                if (bottom > maxY)
                {
                    maxY = bottom;
                }
            }
            else if (GlyphTypeface.TryGetGlyphMetrics(glyph.GlyphIndex, out var metrics))
            {
                var scale = GlyphTypeface.Metrics.DesignEmHeight != 0
                    ? FontRenderingEmSize / GlyphTypeface.Metrics.DesignEmHeight
                    : 1.0;

                var left = baselineOrigin.X + x + metrics.XBearing * scale;
                var top = baselineOrigin.Y + offsetY - metrics.YBearing * scale;
                var right = left + metrics.Width * scale;
                var bottom = top + metrics.Height * scale;

                if (left < minX)
                {
                    minX = left;
                }

                if (top < minY)
                {
                    minY = top;
                }

                if (right > maxX)
                {
                    maxX = right;
                }

                if (bottom > maxY)
                {
                    maxY = bottom;
                }
            }

            currentX += glyph.GlyphAdvance;
        }

        if (double.IsPositiveInfinity(minX) || double.IsPositiveInfinity(minY))
        {
            minX = baselineOrigin.X;
            minY = baselineOrigin.Y;
            maxX = baselineOrigin.X + currentX;
            maxY = baselineOrigin.Y + FontRenderingEmSize;
        }

        if (simulations != FontSimulations.None)
        {
            if (simulations.HasFlag(FontSimulations.Oblique))
            {
                var skewExtent = Math.Abs(VelloGlyphTypeface.FauxItalicSkew) * FontRenderingEmSize;
                minX -= skewExtent;
                maxX += skewExtent;
            }

            if (simulations.HasFlag(FontSimulations.Bold))
            {
                var strokeExtent = Math.Max(1.0, FontRenderingEmSize * VelloGlyphTypeface.FauxBoldStrokeScale) * 0.5;
                minX -= strokeExtent;
                maxX += strokeExtent;
                minY -= strokeExtent;
                maxY += strokeExtent;
            }
        }

        bounds = new Rect(new Point(minX, minY), new Point(maxX, maxY));

        return glyphs;
    }
}
