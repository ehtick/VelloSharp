using System;
using System.Collections.Generic;
using Avalonia.Media;
using AvaloniaVelloSkiaSharpSample.Diagnostics;
using AvaloniaVelloSkiaSharpSample.Rendering;
using AvaloniaVelloSkiaSharpSample.Services;
using SkiaSharp;
using SampleSkiaBackendService = AvaloniaVelloSkiaSharpSample.Services.SkiaBackendService;

namespace AvaloniaVelloSkiaSharpSample.ViewModels.Pages;

public sealed class TypographyPlaygroundViewModel : SamplePageViewModel
{
    private readonly IReadOnlyList<TextSampleOption> _samples =
    [
        new("Intro", "VelloSharp · Skia Shaped Text\nDesign once, render everywhere."),
        new("Ligatures", "Office ➝ Efficient\nAffine · Offline · Coffeehouse"),
        new("Emoji", "Vector ✨ Raster 🖼️ Glyphs 🔤 🎨"),
    ];

    private TextSampleOption _selectedSample;
    private double _fontSize = 46;
    private bool _useGradientFill = true;
    private bool _useSubpixel = true;
    private bool _showMetrics = true;
    private string _typographySummary = string.Empty;

    public TypographyPlaygroundViewModel(
        SkiaCaptureRecorder? captureRecorder = null,
        SampleSkiaBackendService? backendService = null,
        SkiaResourceService? resourceService = null)
        : base(
            "Typography Playground",
            "Adjust glyph rendering options, inspect baseline metrics, and animate typographic compositions.",
            null,
            captureRecorder,
            backendService,
            resourceService)
    {
        _selectedSample = _samples[0];
    }

    protected override string CaptureLabel => "typography-playground";

    public IReadOnlyList<TextSampleOption> Samples => _samples;

    public TextSampleOption SelectedSample
    {
        get => _selectedSample;
        set => SetAndRequestRender(ref _selectedSample, value);
    }

    public double FontSize
    {
        get => _fontSize;
        set => SetAndRequestRender(ref _fontSize, Math.Clamp(value, 18, 96));
    }

    public bool UseGradientFill
    {
        get => _useGradientFill;
        set => SetAndRequestRender(ref _useGradientFill, value);
    }

    public bool UseSubpixel
    {
        get => _useSubpixel;
        set => SetAndRequestRender(ref _useSubpixel, value);
    }

    public bool ShowMetrics
    {
        get => _showMetrics;
        set => SetAndRequestRender(ref _showMetrics, value);
    }

    public string TypographySummary
    {
        get => _typographySummary;
        private set => RaiseAndSetIfChanged(ref _typographySummary, value);
    }

    public override void Render(in SkiaLeaseRenderContext context)
    {
        var canvas = context.Canvas;
        var info = context.ImageInfo;

        canvas.Clear(new SKColor(14, 18, 32, 255));

        using var backgroundPaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, info.Height),
                new SKPoint(info.Width, 0),
                new[]
                {
                    new SKColor(30, 42, 72, 255),
                    new SKColor(16, 24, 42, 255),
                },
                null,
                SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(new SKRect(0, 0, info.Width, info.Height), backgroundPaint);

        using var textPaint = new SKPaint
        {
            Color = new SKColor(235, 240, 255, 255),
            TextSize = (float)_fontSize,
            IsAntialias = true,
            Typeface = SKTypeface.Default,
        };

        if (UseGradientFill)
        {
            textPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(info.Width, 0),
                new[]
                {
                    new SKColor(108, 220, 255, 255),
                    new SKColor(255, 180, 228, 255),
                },
                null,
                SKShaderTileMode.Clamp);
        }
        else
        {
            textPaint.Shader = null;
        }

        textPaint.IsStroke = false;

        var lines = SelectedSample.Text.Split('\n');
        var lineHeight = textPaint.TextSize * 1.35f;
        var originX = info.Width * 0.1f;
        var originY = info.Height * 0.35f;

        if (!UseSubpixel)
        {
            textPaint.IsAntialias = false;
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var y = originY + i * lineHeight;
            DrawLine(canvas, textPaint, lines[i], originX, y);
        }

        TypographySummary = $"Sample: {SelectedSample.Name} · Size {FontSize:F0}px · Gradient {(UseGradientFill ? "On" : "Off")} · Subpixel {(UseSubpixel ? "On" : "Off")}";

        ProcessCapture(context);
    }

    private void DrawLine(SKCanvas canvas, SKPaint textPaint, string text, float originX, float baselineY)
    {
        canvas.DrawText(text, originX, baselineY, textPaint);

        if (!ShowMetrics)
        {
            return;
        }

        var width = EstimateWidth(text, textPaint.TextSize);
        using var metricsPaint = new SKPaint
        {
            Color = new SKColor(255, 255, 255, 64),
            IsAntialias = true,
            StrokeWidth = 1.5f,
            Style = SKPaintStyle.Stroke,
        };

        DrawLineSegment(canvas, new SKPoint(originX, baselineY), new SKPoint(originX + width, baselineY), metricsPaint);

        metricsPaint.Color = new SKColor(120, 200, 255, 96);

        var ascent = -(float)(textPaint.TextSize * 0.78f);
        var descent = (float)(textPaint.TextSize * 0.26f);
        var top = baselineY + ascent;
        var bottom = baselineY + descent;
        var rect = new SKRect(originX, top, originX + width, bottom);
        canvas.DrawRect(rect, metricsPaint);
    }

    private static float EstimateWidth(string text, float size)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        const float average = 0.58f;
        return text.Length * size * average;
    }

    private static void DrawLineSegment(SKCanvas canvas, SKPoint start, SKPoint end, SKPaint paint)
    {
        using var path = new SKPath();
        path.MoveTo(start);
        path.LineTo(end);
        canvas.DrawPath(path, paint);
    }

    public sealed record TextSampleOption(string Name, string Text);
}
