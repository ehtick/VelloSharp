using System;
using AvaloniaVelloSkiaSharpSample.Diagnostics;
using AvaloniaVelloSkiaSharpSample.Navigation;
using AvaloniaVelloSkiaSharpSample.Rendering;
using AvaloniaVelloSkiaSharpSample.Services;
using MiniMvvm;
using SkiaSharp;
using SampleSkiaBackendService = AvaloniaVelloSkiaSharpSample.Services.SkiaBackendService;

namespace AvaloniaVelloSkiaSharpSample.ViewModels.Pages;

public sealed class AdvancedUtilitiesViewModel : SamplePageViewModel
{
    private readonly SkiaResourceService _resourceService;
    private SKImage? _panelImage;
    private float _rotation = 18f;
    private float _scale = 1.05f;
    private float _shear = 0.18f;
    private string _matrixSummary = string.Empty;

    public AdvancedUtilitiesViewModel(
        SkiaResourceService resourceService,
        SkiaCaptureRecorder? captureRecorder = null,
        SampleSkiaBackendService? backendService = null)
        : base(
            "Advanced Utilities",
            "Combine matrices, bitmap transforms, and path bounds to stress advanced shim utilities.",
            null,
            captureRecorder,
            backendService,
            resourceService)
    {
        _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
        ResetCommand = MiniCommand.Create(ResetParameters);
        UpdateMatrixSummary();

        SetDocumentationLinks(
            new DocumentationLink("SKMatrix", new Uri("https://learn.microsoft.com/xamarin/graphics-games/skiasharp/matrices")),
            new DocumentationLink("SKBitmap", new Uri("https://learn.microsoft.com/xamarin/graphics-games/skiasharp/bitmaps")));
    }

    public MiniCommand ResetCommand { get; }

    public float Rotation
    {
        get => _rotation;
        set
        {
            var clamped = Math.Clamp(value, -360f, 360f);
            if (Math.Abs(clamped - _rotation) > 0.01f)
            {
                _rotation = clamped;
                UpdateMatrixSummary();
                RequestRender();
            }
        }
    }

    public float Scale
    {
        get => _scale;
        set
        {
            var clamped = Math.Clamp(value, 0.5f, 1.6f);
            if (Math.Abs(clamped - _scale) > 0.01f)
            {
                _scale = clamped;
                UpdateMatrixSummary();
                RequestRender();
            }
        }
    }

    public float Shear
    {
        get => _shear;
        set
        {
            var clamped = Math.Clamp(value, -0.4f, 0.4f);
            if (Math.Abs(clamped - _shear) > 0.01f)
            {
                _shear = clamped;
                UpdateMatrixSummary();
                RequestRender();
            }
        }
    }

    public string MatrixSummary
    {
        get => _matrixSummary;
        private set => RaiseAndSetIfChanged(ref _matrixSummary, value);
    }

    public override void Render(in SkiaLeaseRenderContext context)
    {
        var canvas = context.Canvas;
        var info = context.ImageInfo;
        canvas.Clear(new SKColor(12, 16, 28, 255));

        canvas.Save();
        canvas.Translate(info.Width * 0.5f, info.Height * 0.42f);
        var baseScale = MathF.Min(info.Width, info.Height) / 620f;
        canvas.Scale(baseScale);

        var basePath = CreateBasePath();
        DrawPath(canvas, basePath, new SKColor(80, 120, 220, 110));

        var transform = ComposeTransform();
        var transformed = basePath.Clone();
        transformed.Transform(transform);

        DrawPath(canvas, transformed, new SKColor(255, 170, 96, 220));
        DrawBounds(canvas, basePath, new SKColor(120, 190, 255, 80));
        DrawBounds(canvas, transformed, new SKColor(255, 200, 120, 120));

        canvas.Restore();

        using var textPaint = new SKPaint
        {
            Color = new SKColor(236, 240, 255, 255),
            TextSize = info.Width * 0.034f,
            IsAntialias = true,
        };
        canvas.DrawText("Matrix Stack", info.Width * 0.08f, info.Height * 0.78f, textPaint);

        using var summaryPaint = new SKPaint
        {
            Color = new SKColor(156, 210, 255, 180),
            TextSize = info.Width * 0.022f,
            IsAntialias = true,
        };
        canvas.DrawText(MatrixSummary, info.Width * 0.08f, info.Height * 0.86f, summaryPaint);

        var panelImage = EnsurePanelImage();
        var panelWidth = Math.Min(info.Width * 0.32f, panelImage.Width);
        var panelHeight = panelWidth * panelImage.Height / panelImage.Width;
        var panelRect = new SKRect(info.Width - panelWidth - 48, info.Height - panelHeight - 48, info.Width - 48, info.Height - 48);
        canvas.DrawImage(panelImage, panelRect);

        using var panelLabel = new SKPaint
        {
            Color = new SKColor(255, 255, 255, 180),
            TextSize = info.Width * 0.02f,
            IsAntialias = true,
        };
        canvas.DrawText("Resampled panel", panelRect.Left, panelRect.Top - 12, panelLabel);

        ProcessCapture(context);
    }

    private void ResetParameters()
    {
        _rotation = 18f;
        _scale = 1.05f;
        _shear = 0.18f;
        UpdateMatrixSummary();
        RequestRender();
    }

    private void UpdateMatrixSummary()
    {
        MatrixSummary = $"Rotate {_rotation:F1}° · Scale {_scale:F2} · Shear {_shear:F2}";
    }

    private SKMatrix ComposeTransform()
    {
        var center = new SKPoint(0, 0);
        var rotation = SKMatrix.CreateRotationDegrees(_rotation, center.X, center.Y);
        var scale = SKMatrix.CreateScale(_scale, _scale);
        var shear = SKMatrix.CreateIdentity();
        shear.SkewX = _shear;
        shear.SkewY = _shear * 0.35f;

        var combined = SKMatrix.Concat(rotation, SKMatrix.Concat(shear, scale));
        return combined;
    }

    private static SKPath CreateBasePath()
    {
        var path = new SKPath { FillType = SKPathFillType.Winding };
        path.MoveTo(-220, -40);
        path.CubicTo(new SKPoint(-160, -180), new SKPoint(180, -140), new SKPoint(200, -12));
        path.CubicTo(new SKPoint(180, 24), new SKPoint(60, 96), new SKPoint(-40, 160));
        path.CubicTo(new SKPoint(-120, 96), new SKPoint(-220, 96), new SKPoint(-220, -40));
        path.Close();
        return path;
    }

    private static void DrawPath(SKCanvas canvas, SKPath path, SKColor fill)
    {
        using var fillPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = fill,
            IsAntialias = true,
        };
        canvas.DrawPath(path, fillPaint);

        using var stroke = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 8f,
            Color = new SKColor(255, 255, 255, 90),
            IsAntialias = true,
        };
        canvas.DrawPath(path, stroke);
    }

    private static void DrawBounds(SKCanvas canvas, SKPath path, SKColor color)
    {
        var bounds = path.TightBounds;
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4f,
            Color = color,
            IsAntialias = true,
        };
        canvas.DrawRect(bounds, paint);
    }

    private SKImage EnsurePanelImage()
    {
        if (_panelImage is not null)
        {
            return _panelImage;
        }

        _panelImage = _resourceService.GetImage("images/aurora-gradient.png");
        return _panelImage;
    }
}
