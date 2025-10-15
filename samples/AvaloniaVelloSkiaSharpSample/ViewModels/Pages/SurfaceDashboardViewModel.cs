using System;
using System.Collections.Generic;
using AvaloniaVelloSkiaSharpSample.Diagnostics;
using AvaloniaVelloSkiaSharpSample.Rendering;
using AvaloniaVelloSkiaSharpSample.Services;
using SkiaSharp;
using SampleSkiaBackendService = AvaloniaVelloSkiaSharpSample.Services.SkiaBackendService;

namespace AvaloniaVelloSkiaSharpSample.ViewModels.Pages;

public sealed class SurfaceDashboardViewModel : SamplePageViewModel
{
    private const double FrameSmoothing = 0.85;
    private const float CircleControlPoint = 0.55228475f;
    private static readonly TimeSpan StatsUpdateInterval = TimeSpan.FromMilliseconds(200);

    private readonly SampleSkiaBackendService _backendService;
    private readonly List<string> _matrixStackBuilder = new();

    private bool _useSaveLayer = true;
    private bool _useClipRect = true;
    private bool _useClipPath;
    private bool _resetMatrix;

    private SurfaceDashboardStats _stats = SurfaceDashboardStats.Empty;
    private string _matrixStackSummary = "Identity";

    private TimeSpan _lastElapsed;
    private double _smoothedFrameTimeMs = 16.0;
    private TimeSpan _lastStatsUpdate;

    public SurfaceDashboardViewModel(
        SampleSkiaBackendService backendService,
        SkiaCaptureRecorder? captureRecorder = null,
        SkiaResourceService? resourceService = null)
        : base(
            "Surface Dashboard",
            "Inspect surface state, canvas transforms, and draw stack while exercising lease rendering.",
            null,
            captureRecorder,
            backendService,
            resourceService)
    {
        _backendService = backendService;
    }

    protected override string CaptureLabel => "surface-dashboard";

    public bool UseSaveLayer
    {
        get => _useSaveLayer;
        set => SetAndRequestRender(ref _useSaveLayer, value);
    }

    public bool UseClipRect
    {
        get => _useClipRect;
        set => SetAndRequestRender(ref _useClipRect, value);
    }

    public bool UseClipPath
    {
        get => _useClipPath;
        set => SetAndRequestRender(ref _useClipPath, value);
    }

    public bool ResetMatrix
    {
        get => _resetMatrix;
        set => SetAndRequestRender(ref _resetMatrix, value);
    }

    public SurfaceDashboardStats Stats
    {
        get => _stats;
        private set => RaiseAndSetIfChanged(ref _stats, value);
    }

    public string MatrixStackSummary
    {
        get => _matrixStackSummary;
        private set => RaiseAndSetIfChanged(ref _matrixStackSummary, value);
    }

    public override void Render(in SkiaLeaseRenderContext context)
    {
        var canvas = context.Canvas;
        var info = context.ImageInfo;
        canvas.Clear(new SKColor(18, 22, 34, 255));

        var width = info.Width;
        var height = info.Height;
        var commandCount = 0;

        DrawGrid(canvas, width, height, ref commandCount);
        DrawContent(canvas, context, ref commandCount);

        UpdateStats(context, width, height, commandCount);
        ProcessCapture(context);
    }

    private void DrawGrid(SKCanvas canvas, int width, int height, ref int commandCount)
    {
        using var gridPaint = new SKPaint
        {
            Color = new SKColor(48, 60, 82, 255),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntialias = false,
        };

        using var path = new SKPath();
        var lines = 0;
        var spacing = Math.Max(32, Math.Min(width, height) / 12f);

        for (var x = 0f; x <= width; x += spacing)
        {
            path.MoveTo(x, 0);
            path.LineTo(x, height);
            lines++;
        }

        for (var y = 0f; y <= height; y += spacing)
        {
            path.MoveTo(0, y);
            path.LineTo(width, y);
            lines++;
        }

        if (lines > 0)
        {
            canvas.DrawPath(path, gridPaint);
            commandCount += lines;
        }
    }

    private void DrawContent(SKCanvas canvas, in SkiaLeaseRenderContext context, ref int commandCount)
    {
        var info = context.ImageInfo;
        var baseRect = new SKRect(-220, -140, 220, 140);

        _matrixStackBuilder.Clear();
        _matrixStackBuilder.Add("Identity");

        canvas.Save();
        _matrixStackBuilder.Add("Save()");

        var translate = new SKPoint(info.Width * 0.5f, info.Height * 0.55f);
        canvas.Translate(translate.X, translate.Y);
        _matrixStackBuilder.Add($"Translate({translate.X:F1}, {translate.Y:F1})");

        if (ResetMatrix)
        {
            canvas.ResetMatrix();
            _matrixStackBuilder.Add("ResetMatrix()");
        }
        else
        {
            var scale = MathF.Min(info.Width, info.Height) / 520f;
            canvas.Scale(scale);
            _matrixStackBuilder.Add($"Scale({scale:F2})");
        }

        if (UseClipRect)
        {
            canvas.Save();
            _matrixStackBuilder.Add("Save()");
            canvas.ClipRect(baseRect);
            _matrixStackBuilder.Add(
                $"ClipRect({baseRect.Left:F0}, {baseRect.Top:F0}, {baseRect.Right:F0}, {baseRect.Bottom:F0})");
        }

        if (UseClipPath)
        {
            using var clipPath = CreateRoundedRectPath(baseRect, 48);
            canvas.ClipPath(clipPath, SKClipOperation.Intersect, true);
            _matrixStackBuilder.Add("ClipPath(RoundedRect r=48)");
        }

        if (UseSaveLayer)
        {
            using var saveLayerPaint = new SKPaint
            {
                Color = new SKColor(255, 255, 255, 24),
            };

            canvas.SaveLayer(saveLayerPaint);
            _matrixStackBuilder.Add("SaveLayer(alpha=0.09)");
        }

        using (var backdropPaint = new SKPaint
               {
                   Style = SKPaintStyle.Fill,
                   Shader = SKShader.CreateLinearGradient(
                       new SKPoint(baseRect.Left, baseRect.Top),
                       new SKPoint(baseRect.Right, baseRect.Bottom),
                       new[]
                       {
                           new SKColor(50, 92, 204, 230),
                           new SKColor(98, 226, 255, 230),
                       },
                       null,
                       SKShaderTileMode.Clamp),
                   IsAntialias = true,
               })
        {
            canvas.DrawRoundRect(baseRect, 32, 32, backdropPaint);
            commandCount++;
        }

        using (var framePaint = new SKPaint
               {
                   Style = SKPaintStyle.Stroke,
                   StrokeWidth = 4f,
                   Color = new SKColor(255, 255, 255, 80),
                   IsAntialias = true,
               })
        {
            canvas.DrawRoundRect(baseRect, 32, 32, framePaint);
            commandCount++;
        }

        using (var axisPaint = new SKPaint
               {
                   Style = SKPaintStyle.Stroke,
                   Color = new SKColor(255, 255, 255, 90),
                   StrokeWidth = 3f,
                   IsAntialias = true,
               })
        {
            using var axisPath = new SKPath();
            axisPath.MoveTo(baseRect.Left, 0);
            axisPath.LineTo(baseRect.Right, 0);
            axisPath.MoveTo(0, baseRect.Top);
            axisPath.LineTo(0, baseRect.Bottom);
            canvas.DrawPath(axisPath, axisPaint);
            commandCount += 2;
        }

        using (var ellipsePaint = new SKPaint
               {
                   Style = SKPaintStyle.StrokeAndFill,
                   StrokeWidth = 2f,
                   Color = new SKColor(255, 189, 87, 220),
                   IsAntialias = true,
               })
        {
            canvas.DrawOval(new SKRect(-160, -90, 160, 90), ellipsePaint);
            commandCount++;
        }

        using (var ringPaint = new SKPaint
               {
                   Style = SKPaintStyle.Stroke,
                   StrokeWidth = 10f,
                   Color = new SKColor(185, 105, 255, 200),
                   StrokeCap = SKStrokeCap.Round,
                   IsAntialias = true,
               })
        {
            canvas.DrawCircle(0, 0, 120, ringPaint);
            commandCount++;
        }

        using (var textPaint = new SKPaint
               {
                   Color = new SKColor(240, 248, 255, 255),
                   IsAntialias = true,
                   TextSize = 42f,
               })
        {
            var headline = "Lease Active";
            var halfWidth = ApproximateTextWidth(headline, textPaint.TextSize) * 0.5f;
            canvas.DrawText(headline, -halfWidth, 16, textPaint);
            commandCount++;
        }

        SKPathEffect? dashEffect = null;
        try
        {
            dashEffect = SKPathEffect.CreateDash(new[] { 12f, 8f }, (float)(context.Frame % 32));
        }
        catch (NotImplementedException)
        {
            dashEffect = null;
        }

        using (var orbitPaint = new SKPaint
               {
                   Style = SKPaintStyle.Stroke,
                   StrokeWidth = 2f,
                   Color = new SKColor(126, 237, 255, 190),
                   PathEffect = dashEffect,
                   IsAntialias = true,
               })
        {
            canvas.DrawCircle(0, 0, 132, orbitPaint);
            commandCount++;
        }

        dashEffect?.Dispose();

        if (UseSaveLayer)
        {
            canvas.Restore();
            _matrixStackBuilder.Add("Restore()");
        }

        if (UseClipRect)
        {
            canvas.Restore();
            _matrixStackBuilder.Add("Restore()");
        }

        canvas.Restore();
        _matrixStackBuilder.Add("Restore()");

        MatrixStackSummary = string.Join(Environment.NewLine, _matrixStackBuilder);
    }

    private void UpdateStats(in SkiaLeaseRenderContext context, int width, int height, int commandCount)
    {
        var elapsed = context.Elapsed;
        if (_lastElapsed == TimeSpan.Zero)
        {
            _lastElapsed = elapsed;
        }

        var frameDeltaMs = Math.Max(0.01, (elapsed - _lastElapsed).TotalMilliseconds);
        _lastElapsed = elapsed;
        _smoothedFrameTimeMs = (_smoothedFrameTimeMs * FrameSmoothing) + (frameDeltaMs * (1.0 - FrameSmoothing));

        var descriptor = _backendService.CurrentDescriptor;
        var frameRate = _smoothedFrameTimeMs > 0.0 ? 1000.0 / _smoothedFrameTimeMs : 0.0;

        if (elapsed - _lastStatsUpdate >= StatsUpdateInterval || Stats == SurfaceDashboardStats.Empty)
        {
            _lastStatsUpdate = elapsed;
            Stats = new SurfaceDashboardStats(
                width,
                height,
                _smoothedFrameTimeMs,
                frameRate,
                context.Frame,
                commandCount,
                descriptor.Title,
                descriptor.Subtitle);
        }
    }

    private static SKPath CreateRoundedRectPath(SKRect rect, float radius)
    {
        radius = MathF.Min(radius, MathF.Min(rect.Width, rect.Height) * 0.5f);
        var k = CircleControlPoint * radius;

        var left = rect.Left;
        var top = rect.Top;
        var right = rect.Right;
        var bottom = rect.Bottom;

        var path = new SKPath();
        path.MoveTo(left + radius, top);
        path.LineTo(right - radius, top);
        path.CubicTo(
            new SKPoint(right - k, top),
            new SKPoint(right, top + k),
            new SKPoint(right, top + radius));
        path.LineTo(right, bottom - radius);
        path.CubicTo(
            new SKPoint(right, bottom - k),
            new SKPoint(right - k, bottom),
            new SKPoint(right - radius, bottom));
        path.LineTo(left + radius, bottom);
        path.CubicTo(
            new SKPoint(left + k, bottom),
            new SKPoint(left, bottom - k),
            new SKPoint(left, bottom - radius));
        path.LineTo(left, top + radius);
        path.CubicTo(
            new SKPoint(left, top + k),
            new SKPoint(left + k, top),
            new SKPoint(left + radius, top));
        path.Close();
        return path;
    }

    private static float ApproximateTextWidth(string text, float size)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        const float AverageGlyphWidthFactor = 0.55f;
        return text.Length * size * AverageGlyphWidthFactor;
    }
}

public readonly record struct SurfaceDashboardStats(
    int SurfaceWidth,
    int SurfaceHeight,
    double FrameTimeMs,
    double FrameRate,
    ulong Frame,
    int CommandCount,
    string BackendTitle,
    string BackendSubtitle)
{
    public static SurfaceDashboardStats Empty { get; } = new(
        0,
        0,
        0,
        0,
        0,
        0,
        string.Empty,
        string.Empty);
}
