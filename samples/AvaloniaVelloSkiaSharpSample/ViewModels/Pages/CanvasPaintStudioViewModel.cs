using System;
using System.Collections.Generic;
using Avalonia.Media;
using AvaloniaVelloSkiaSharpSample.Diagnostics;
using AvaloniaVelloSkiaSharpSample.Rendering;
using AvaloniaVelloSkiaSharpSample.Services;
using SkiaSharp;
using SampleSkiaBackendService = AvaloniaVelloSkiaSharpSample.Services.SkiaBackendService;

namespace AvaloniaVelloSkiaSharpSample.ViewModels.Pages;

public sealed class CanvasPaintStudioViewModel : SamplePageViewModel
{
    private readonly IReadOnlyList<PaintStyleOption> _paintStyles =
    [
        new("Fill", SKPaintStyle.Fill),
        new("Stroke", SKPaintStyle.Stroke),
        new("Stroke & Fill", SKPaintStyle.StrokeAndFill),
    ];

    private readonly IReadOnlyList<StrokeCapOption> _strokeCaps =
    [
        new("Butt", SKStrokeCap.Butt),
        new("Round", SKStrokeCap.Round),
        new("Square", SKStrokeCap.Square),
    ];

    private readonly IReadOnlyList<StrokeJoinOption> _strokeJoins =
    [
        new("Miter", SKStrokeJoin.Miter),
        new("Round", SKStrokeJoin.Round),
        new("Bevel", SKStrokeJoin.Bevel),
    ];

    private readonly IReadOnlyList<BlendModeOption> _blendModes =
    [
        new("Source Over", SKBlendMode.SrcOver),
        new("Screen", SKBlendMode.Screen),
        new("Multiply", SKBlendMode.Multiply),
        new("Overlay", SKBlendMode.Overlay),
        new("Plus", SKBlendMode.Plus),
    ];

    private readonly IReadOnlyList<ColorOption> _palette =
    [
        new("Sky", new SKColor(102, 204, 255, 255)),
        new("Aurora", new SKColor(96, 255, 172, 255)),
        new("Sunset", new SKColor(255, 184, 108, 255)),
        new("Violet", new SKColor(178, 137, 255, 255)),
        new("Crimson", new SKColor(255, 99, 132, 255)),
    ];

    private PaintStyleOption _selectedPaintStyle;
    private StrokeCapOption _selectedStrokeCap;
    private StrokeJoinOption _selectedStrokeJoin;
    private BlendModeOption _selectedBlendMode;
    private ColorOption _selectedFillColor;
    private ColorOption _selectedStrokeColor;

    private float _strokeWidth = 12f;
    private bool _isAntialias = true;
    private bool _useGradientFill = true;
    private bool _showGuides = true;

    private string _paintSummary = string.Empty;

    public CanvasPaintStudioViewModel(
        SkiaCaptureRecorder? captureRecorder = null,
        SampleSkiaBackendService? backendService = null,
        SkiaResourceService? resourceService = null)
        : base(
            "Canvas & Paint Studio",
            "Explore paint configuration, blend modes, and stroke geometry constructed through the shimmed Skia APIs.",
            null,
            captureRecorder,
            backendService,
            resourceService)
    {
        _selectedPaintStyle = _paintStyles[2];
        _selectedStrokeCap = _strokeCaps[1];
        _selectedStrokeJoin = _strokeJoins[1];
        _selectedBlendMode = _blendModes[0];
        _selectedFillColor = _palette[0];
        _selectedStrokeColor = _palette[3];

        UpdatePaintSummary();
    }

    protected override string CaptureLabel => "canvas-paint";

    public IReadOnlyList<PaintStyleOption> PaintStyles => _paintStyles;

    public IReadOnlyList<StrokeCapOption> StrokeCaps => _strokeCaps;

    public IReadOnlyList<StrokeJoinOption> StrokeJoins => _strokeJoins;

    public IReadOnlyList<BlendModeOption> BlendModes => _blendModes;

    public IReadOnlyList<ColorOption> Palette => _palette;

    public PaintStyleOption SelectedPaintStyle
    {
        get => _selectedPaintStyle;
        set
        {
            if (SetAndRequestRender(ref _selectedPaintStyle, value))
            {
                UpdatePaintSummary();
            }
        }
    }

    public StrokeCapOption SelectedStrokeCap
    {
        get => _selectedStrokeCap;
        set
        {
            if (SetAndRequestRender(ref _selectedStrokeCap, value))
            {
                UpdatePaintSummary();
            }
        }
    }

    public StrokeJoinOption SelectedStrokeJoin
    {
        get => _selectedStrokeJoin;
        set
        {
            if (SetAndRequestRender(ref _selectedStrokeJoin, value))
            {
                UpdatePaintSummary();
            }
        }
    }

    public BlendModeOption SelectedBlendMode
    {
        get => _selectedBlendMode;
        set
        {
            if (SetAndRequestRender(ref _selectedBlendMode, value))
            {
                UpdatePaintSummary();
            }
        }
    }

    public ColorOption SelectedFillColor
    {
        get => _selectedFillColor;
        set
        {
            if (SetAndRequestRender(ref _selectedFillColor, value))
            {
                UpdatePaintSummary();
            }
        }
    }

    public ColorOption SelectedStrokeColor
    {
        get => _selectedStrokeColor;
        set
        {
            if (SetAndRequestRender(ref _selectedStrokeColor, value))
            {
                UpdatePaintSummary();
            }
        }
    }

    public double StrokeWidth
    {
        get => _strokeWidth;
        set
        {
            var width = (float)Math.Clamp(value, 0.5, 96);
            if (Math.Abs(width - _strokeWidth) > 0.01f)
            {
                _strokeWidth = width;
                RaisePropertyChanged();
                UpdatePaintSummary();
                RequestRender();
            }
        }
    }

    public bool IsAntialias
    {
        get => _isAntialias;
        set
        {
            if (SetAndRequestRender(ref _isAntialias, value))
            {
                UpdatePaintSummary();
            }
        }
    }

    public bool UseGradientFill
    {
        get => _useGradientFill;
        set
        {
            if (SetAndRequestRender(ref _useGradientFill, value))
            {
                UpdatePaintSummary();
            }
        }
    }

    public bool ShowGuides
    {
        get => _showGuides;
        set => SetAndRequestRender(ref _showGuides, value);
    }

    public string PaintSummary
    {
        get => _paintSummary;
        private set => RaiseAndSetIfChanged(ref _paintSummary, value);
    }

    public override void Render(in SkiaLeaseRenderContext context)
    {
        var canvas = context.Canvas;
        var info = context.ImageInfo;
        canvas.Clear(new SKColor(14, 20, 32, 255));

        var center = new SKPoint(info.Width * 0.5f, info.Height * 0.52f);
        var scale = MathF.Min(info.Width, info.Height) / 520f;

        canvas.Save();
        canvas.Translate(center.X, center.Y);
        canvas.Scale(scale);

        DrawBackdrop(canvas);
        DrawRibbon(canvas);
        DrawStrokeDemo(canvas);

        canvas.Restore();

        if (ShowGuides)
        {
            DrawGuides(canvas, info);
        }

        ProcessCapture(context);
    }

    private void DrawBackdrop(SKCanvas canvas)
    {
        using var backdropPaint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(0, -40),
                320,
                new[]
                {
                    new SKColor(30, 60, 92, 255),
                    new SKColor(12, 20, 32, 255),
                },
                null,
                SKShaderTileMode.Clamp),
            IsAntialias = true,
        };

        canvas.DrawOval(new SKRect(-320, -220, 320, 220), backdropPaint);
    }

    private void DrawRibbon(SKCanvas canvas)
    {
        using var ribbonPath = CreateRibbonPath();

        using var fillPaint = CreateConfiguredPaint();
        if (fillPaint.Style is SKPaintStyle.Fill or SKPaintStyle.StrokeAndFill)
        {
            canvas.DrawPath(ribbonPath, fillPaint);
        }

        using var strokePaint = CreateStrokePaint();
        if (fillPaint.Style is SKPaintStyle.Stroke or SKPaintStyle.StrokeAndFill)
        {
            canvas.DrawPath(ribbonPath, strokePaint);
        }
    }

    private void DrawStrokeDemo(SKCanvas canvas)
    {
        using var guidePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(255, 255, 255, 28),
            StrokeWidth = 1f,
            IsAntialias = false,
        };

        using var strokePaint = CreateStrokePaint();
        strokePaint.StrokeWidth = MathF.Max(2f, _strokeWidth);

        canvas.Save();
        canvas.Translate(-220, 140);

        if (ShowGuides)
        {
            canvas.DrawRect(new SKRect(0, -80, 440, 80), guidePaint);
        }

        using var strokePath = new SKPath();
        strokePath.MoveTo(20, -60);
        strokePath.CubicTo(new SKPoint(160, -120), new SKPoint(260, 0), new SKPoint(420, -40));
        strokePath.LineTo(420, 20);
        strokePath.CubicTo(new SKPoint(260, 60), new SKPoint(160, -40), new SKPoint(20, 40));

        canvas.DrawPath(strokePath, strokePaint);

        if (ShowGuides)
        {
            using var pointPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = new SKColor(255, 255, 255, 90),
                IsAntialias = true,
            };

            foreach (var point in new[]
                     {
                         new SKPoint(20, -60),
                         new SKPoint(420, -40),
                         new SKPoint(420, 20),
                         new SKPoint(20, 40),
                     })
            {
                canvas.DrawCircle(point, 4, pointPaint);
            }
        }

        canvas.Restore();
    }

    private SKPaint CreateConfiguredPaint()
    {
        var paint = new SKPaint
        {
            Style = SelectedPaintStyle.Style,
            IsAntialias = IsAntialias,
            StrokeCap = SelectedStrokeCap.Cap,
            StrokeJoin = SelectedStrokeJoin.Join,
            StrokeWidth = MathF.Max(0.5f, _strokeWidth),
            BlendMode = SelectedBlendMode.Mode,
            Color = SelectedFillColor.Color,
        };

        if (UseGradientFill)
        {
            paint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(-160, -140),
                new SKPoint(160, 160),
                new[]
                {
                    SelectedFillColor.Color,
                    new SKColor(
                        (byte)Math.Clamp(SelectedFillColor.Color.Red + 40, 0, 255),
                        (byte)Math.Clamp(SelectedFillColor.Color.Green + 18, 0, 255),
                        (byte)Math.Clamp(SelectedFillColor.Color.Blue + 12, 0, 255),
                        SelectedFillColor.Color.Alpha),
                },
                null,
                SKShaderTileMode.Clamp);
        }
        else
        {
            paint.Shader = null;
        }

        return paint;
    }

    private SKPaint CreateStrokePaint()
    {
        return new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            IsAntialias = IsAntialias,
            StrokeCap = SelectedStrokeCap.Cap,
            StrokeJoin = SelectedStrokeJoin.Join,
            StrokeWidth = MathF.Max(1f, _strokeWidth),
            Color = SelectedStrokeColor.Color,
            BlendMode = SKBlendMode.SrcOver,
        };
    }

    private static SKPath CreateRibbonPath()
    {
        var path = new SKPath();
        path.MoveTo(-220, -20);
        path.CubicTo(new SKPoint(-140, -140), new SKPoint(140, -140), new SKPoint(220, -20));
        path.CubicTo(new SKPoint(120, 20), new SKPoint(-40, 40), new SKPoint(-20, 140));
        path.CubicTo(new SKPoint(-120, 60), new SKPoint(-220, 60), new SKPoint(-220, -20));
        path.Close();
        return path;
    }

    private void DrawGuides(SKCanvas canvas, SKImageInfo info)
    {
        using var guidePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(255, 255, 255, 32),
            StrokeWidth = 1f,
            IsAntialias = false,
        };

        var rect = new SKRect(24, 24, info.Width - 24, info.Height - 24);
        canvas.DrawRect(rect, guidePaint);
    }

    private void UpdatePaintSummary()
    {
        PaintSummary =
            $"{SelectedPaintStyle.Name} · Stroke {StrokeWidth:F1}px · Cap {SelectedStrokeCap.Name} · Join {SelectedStrokeJoin.Name} · Blend {SelectedBlendMode.Name}";
    }

    public sealed record PaintStyleOption(string Name, SKPaintStyle Style);

    public sealed record StrokeCapOption(string Name, SKStrokeCap Cap);

    public sealed record StrokeJoinOption(string Name, SKStrokeJoin Join);

    public sealed record BlendModeOption(string Name, SKBlendMode Mode);

    public sealed record ColorOption(string Name, SKColor Swatch)
    {
        private static Color ToMedia(SKColor value) => Avalonia.Media.Color.FromArgb(value.Alpha, value.Red, value.Green, value.Blue);

        public SKColor Color => Swatch;

        public Color MediaColor { get; } = ToMedia(Swatch);

        public IBrush Brush { get; } = new SolidColorBrush(ToMedia(Swatch));
    }
}
