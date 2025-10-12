extern alias VSHB;

using System;
using System.Collections.Generic;
using System.Numerics;
using AvaloniaVelloHarfBuzzSample.Services;
using VelloSharp;

namespace AvaloniaVelloHarfBuzzSample.Rendering;

public sealed class GlyphRunScene
{
    private readonly Glyph[] _glyphs;
    private readonly GlyphRunOptions _options;

    public GlyphRunScene(
        FontAssetService.FontAssetReference fontReference,
        Glyph[] glyphs,
        GlyphRunOptions options,
        GlyphRunMetrics metrics,
        string text,
        VSHB::HarfBuzzSharp.Direction direction,
        VSHB::HarfBuzzSharp.Language language,
        VSHB::HarfBuzzSharp.Script script,
        VSHB::HarfBuzzSharp.Feature[]? features,
        VSHB::HarfBuzzSharp.GlyphInfo[] glyphInfos,
        VSHB::HarfBuzzSharp.GlyphPosition[] glyphPositions,
        string serializedGlyphsText,
        string serializedGlyphsJson,
        float unitsPerEmScale,
        float fontSize,
        DateTimeOffset timestamp)
    {
        FontReference = fontReference ?? throw new ArgumentNullException(nameof(fontReference));
        _glyphs = glyphs ?? throw new ArgumentNullException(nameof(glyphs));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        Metrics = metrics;
        Text = text ?? string.Empty;
        Direction = direction;
        Language = language;
        Script = script;
        Features = features ?? Array.Empty<VSHB::HarfBuzzSharp.Feature>();
        GlyphInfos = glyphInfos ?? Array.Empty<VSHB::HarfBuzzSharp.GlyphInfo>();
        GlyphPositions = glyphPositions ?? Array.Empty<VSHB::HarfBuzzSharp.GlyphPosition>();
        SerializedGlyphsText = serializedGlyphsText ?? string.Empty;
        SerializedGlyphsJson = serializedGlyphsJson ?? string.Empty;
        UnitsPerEmScale = unitsPerEmScale <= 0 ? 64f : unitsPerEmScale;
        FontSize = fontSize;
        Timestamp = timestamp;
    }

    public FontAssetService.FontAssetReference FontReference { get; }

    public GlyphRunMetrics Metrics { get; }

    public string Text { get; }

    public VSHB::HarfBuzzSharp.Direction Direction { get; }

    public VSHB::HarfBuzzSharp.Language Language { get; }

    public VSHB::HarfBuzzSharp.Script Script { get; }

    public IReadOnlyList<VSHB::HarfBuzzSharp.Feature> Features { get; }

    public IReadOnlyList<VSHB::HarfBuzzSharp.GlyphInfo> GlyphInfos { get; }

    public IReadOnlyList<VSHB::HarfBuzzSharp.GlyphPosition> GlyphPositions { get; }

    public string SerializedGlyphsText { get; }

    public string SerializedGlyphsJson { get; }

    public float UnitsPerEmScale { get; }

    public float FontSize { get; }

    public DateTimeOffset Timestamp { get; }

    public void Render(Scene scene, Matrix3x2 globalTransform)
    {
        if (scene is null)
        {
            throw new ArgumentNullException(nameof(scene));
        }

        var font = FontReference.GetSceneFont();
        var options = CloneOptions();
        options.Transform = Matrix3x2.Multiply(_options.Transform, globalTransform);
        scene.DrawGlyphRun(font, _glyphs, options);
    }

    public void Render(Scene scene)
        => Render(scene, Matrix3x2.Identity);

    private GlyphRunOptions CloneOptions()
    {
        var clone = new GlyphRunOptions
        {
            Brush = _options.Brush,
            FontSize = _options.FontSize,
            Hint = _options.Hint,
            Style = _options.Style,
            Stroke = CloneStroke(_options.Stroke),
            BrushAlpha = _options.BrushAlpha,
            Transform = _options.Transform,
            GlyphTransform = _options.GlyphTransform,
        };
        return clone;
    }

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

public readonly record struct GlyphRunMetrics(
    float Ascender,
    float Descender,
    float LineGap,
    float AdvanceX,
    float AdvanceY)
{
    public float LineHeight => Ascender - Descender + LineGap;
}
