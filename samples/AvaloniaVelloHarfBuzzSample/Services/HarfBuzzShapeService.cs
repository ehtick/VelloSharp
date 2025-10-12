extern alias VSHB;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CommunityToolkit.Diagnostics;
using AvaloniaVelloHarfBuzzSample.Rendering;
using VelloSharp;

namespace AvaloniaVelloHarfBuzzSample.Services;

public sealed class HarfBuzzShapeService
{
    internal const float DefaultUnitsPerEmScale = 64f;

    private readonly FontAssetService _fontAssets;

    public HarfBuzzShapeService(FontAssetService fontAssets)
    {
        _fontAssets = fontAssets ?? throw new ArgumentNullException(nameof(fontAssets));
    }

    public VSHB::HarfBuzzSharp.Buffer CreateBuffer(
        VSHB::HarfBuzzSharp.Direction direction,
        VSHB::HarfBuzzSharp.Language language,
        VSHB::HarfBuzzSharp.Script script)
    {
        var buffer = new VSHB::HarfBuzzSharp.Buffer
        {
            Direction = direction,
            Language = language,
            Script = script,
        };
        buffer.ClusterLevel = VSHB::HarfBuzzSharp.ClusterLevel.MonotoneCharacters;
        return buffer;
    }

    public GlyphRunScene ShapeText(
        FontAssetService.FontAssetReference fontReference,
        string text,
        float fontSize,
        ShapeTextOptions? options = null)
    {
        Guard.IsNotNull(fontReference);
        Guard.IsNotNull(text);
        Guard.IsGreaterThan(fontSize, 0f);

        options ??= ShapeTextOptions.Default;

        using var lease = fontReference.CreateFontLease();
        using var buffer = CreateBuffer(options.Direction, options.Language, options.Script);

        if (options.ClearBufferBeforeUse)
        {
            buffer.Reset();
        }

        buffer.AddUtf16(text);

        if (options.GuessSegmentProperties)
        {
            buffer.GuessSegmentProperties();
        }

        var features = PrepareFeatures(options.Features);
        var scale = Math.Max(1, (int)MathF.Round(fontSize * options.UnitsPerEmScale));
        lease.Font.SetScale(scale, scale);
        lease.Font.Shape(buffer, features);

        var glyphInfos = buffer.GlyphInfos;
        var glyphPositions = buffer.GlyphPositions;

        var glyphs = new Glyph[glyphInfos.Length];
        var infoSnapshot = new VSHB::HarfBuzzSharp.GlyphInfo[glyphInfos.Length];
        var positionSnapshot = new VSHB::HarfBuzzSharp.GlyphPosition[glyphPositions.Length];

        var penX = options.Origin.X;
        var penY = options.Origin.Y;
        var divisor = options.UnitsPerEmScale <= 0 ? DefaultUnitsPerEmScale : options.UnitsPerEmScale;

        for (var i = 0; i < glyphInfos.Length; i++)
        {
            var info = glyphInfos[i];
            var position = glyphPositions[i];

            var glyphX = penX + position.XOffset / divisor;
            var glyphY = penY - position.YOffset / divisor;

            glyphs[i] = new Glyph(info.Codepoint, glyphX, glyphY);
            infoSnapshot[i] = info;
            positionSnapshot[i] = position;

            penX += position.XAdvance / divisor;
            penY += position.YAdvance / divisor;
        }

        var advanceX = penX - options.Origin.X;
        var advanceY = penY - options.Origin.Y;

        var extents = lease.Font.GetFontExtentsForDirection(options.Direction);
        var metrics = new GlyphRunMetrics(
            Ascender: extents.Ascender / divisor,
            Descender: extents.Descender / divisor,
            LineGap: extents.LineGap / divisor,
            AdvanceX: advanceX,
            AdvanceY: advanceY);

        var brush = options.Brush ?? new SolidColorBrush(RgbaColor.FromBytes(255, 255, 255));
        var glyphRunOptions = new GlyphRunOptions
        {
            Brush = brush,
            FontSize = fontSize,
            Hint = options.Hint,
            Style = options.Style,
            Stroke = options.Style == GlyphRunStyle.Stroke ? CloneStroke(options.Stroke) : null,
            BrushAlpha = options.BrushAlpha,
            Transform = options.Transform,
            GlyphTransform = options.GlyphTransform,
        };

        var glyphsText = buffer.SerializeGlyphs(lease.Font, VSHB::HarfBuzzSharp.SerializeFormat.Text, options.SerializeFlags);
        var glyphsJson = buffer.SerializeGlyphs(lease.Font, VSHB::HarfBuzzSharp.SerializeFormat.Json, options.SerializeFlags);

        var featureSnapshot = features is null ? null : (VSHB::HarfBuzzSharp.Feature[])features.Clone();

        return new GlyphRunScene(
            fontReference,
            glyphs,
            glyphRunOptions,
            metrics,
            text,
            options.Direction,
            options.Language,
            options.Script,
            featureSnapshot,
            infoSnapshot,
            positionSnapshot,
            glyphsText,
            glyphsJson,
            options.UnitsPerEmScale,
            fontSize,
            DateTimeOffset.UtcNow);
    }

    private static VSHB::HarfBuzzSharp.Feature[]? PrepareFeatures(IReadOnlyList<VSHB::HarfBuzzSharp.Feature>? features)
        => features is null || features.Count == 0 ? null : features.ToArray();

    private static StrokeStyle? CloneStroke(StrokeStyle? source)
    {
        if (source is null)
        {
            return null;
        }

        var clone = new StrokeStyle
        {
            Width = source.Width,
            MiterLimit = source.MiterLimit,
            StartCap = source.StartCap,
            EndCap = source.EndCap,
            LineJoin = source.LineJoin,
            DashPhase = source.DashPhase,
        };

        if (source.DashPattern is { Length: > 0 } pattern)
        {
            clone.DashPattern = (double[])pattern.Clone();
        }

        return clone;
    }
}

public sealed class ShapeTextOptions
{
    private static readonly VSHB::HarfBuzzSharp.Language DefaultLanguage = VSHB::HarfBuzzSharp.Language.FromBcp47("und");

    public static ShapeTextOptions Default { get; } = new();

    public VSHB::HarfBuzzSharp.Direction Direction { get; init; } = VSHB::HarfBuzzSharp.Direction.LeftToRight;

    public VSHB::HarfBuzzSharp.Language Language { get; init; } = DefaultLanguage;

    public VSHB::HarfBuzzSharp.Script Script { get; init; } = VSHB::HarfBuzzSharp.Script.Common;

    public IReadOnlyList<VSHB::HarfBuzzSharp.Feature>? Features { get; init; }

    public bool GuessSegmentProperties { get; init; } = true;

    public bool ClearBufferBeforeUse { get; init; }

    public float UnitsPerEmScale { get; init; } = HarfBuzzShapeService.DefaultUnitsPerEmScale;

    public float BrushAlpha { get; init; } = 1f;

    public bool Hint { get; init; }

    public GlyphRunStyle Style { get; init; } = GlyphRunStyle.Fill;

    public StrokeStyle? Stroke { get; init; }

    public Brush? Brush { get; init; }

    public Matrix3x2 Transform { get; init; } = Matrix3x2.Identity;

    public Matrix3x2? GlyphTransform { get; init; }

    public Vector2 Origin { get; init; } = Vector2.Zero;

    public VSHB::HarfBuzzSharp.SerializeFlag SerializeFlags { get; init; } =
        VSHB::HarfBuzzSharp.SerializeFlag.GlyphFlags | VSHB::HarfBuzzSharp.SerializeFlag.GlyphExtents;
}
