using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using AvaloniaVelloSkiaSharpSample.Diagnostics;
using AvaloniaVelloSkiaSharpSample.Rendering;
using AvaloniaVelloSkiaSharpSample.Services;
using MiniMvvm;
using SkiaSharp;
using SampleSkiaBackendService = AvaloniaVelloSkiaSharpSample.Services.SkiaBackendService;

namespace AvaloniaVelloSkiaSharpSample.ViewModels.Pages;

public sealed class ImageWorkshopViewModel : SamplePageViewModel
{
    private readonly IReadOnlyList<ImageSourceOption> _sources =
    [
        new ImageSourceOption("Gradient Fields (procedural)", ImageSourceKind.Procedural, ImageMode.Gradient),
        new ImageSourceOption("Checkerboard (procedural)", ImageSourceKind.Procedural, ImageMode.Checkerboard),
        new ImageSourceOption("Plasma Noise (procedural)", ImageSourceKind.Procedural, ImageMode.Plasma),
        new ImageSourceOption("Aurora Gradient (asset)", ImageSourceKind.Asset, null, "images/aurora-gradient.png"),
        new ImageSourceOption("Studio Panel (asset)", ImageSourceKind.Asset, null, "images/studio-panel.jpg"),
        new ImageSourceOption("Palette Orbit (asset)", ImageSourceKind.Asset, null, "images/palette-orbit.gif"),
    ];

    private readonly IReadOnlyList<ImageProcessingOption> _processingModes =
    [
        new ImageProcessingOption("Original pixels", ImageProcessingMode.Original),
        new ImageProcessingOption("Half resolution resize", ImageProcessingMode.HalfResolution),
    ];

    private ImageSourceOption _selectedSource;
    private ImageProcessingOption _selectedProcessing;

    private double _zoom = 1.0;
    private bool _showHistogram = true;
    private bool _animate;

    private string _imageSummary = string.Empty;
    private string _codecSummary = "Select a source to inspect codec metadata.";
    private string _dataSummary = string.Empty;
    private string _processingSummary = string.Empty;
    private string _exportStatus = string.Empty;

    private SKImage? _cachedImage;
    private ImageSourceOption? _cachedSource;
    private ImageProcessingOption? _cachedProcessing;
    private int _cachedFrameSeed;

    private SKImage? _lastRenderedImage;

    public ImageWorkshopViewModel(
        SkiaCaptureRecorder? captureRecorder = null,
        SampleSkiaBackendService? backendService = null,
        SkiaResourceService? resourceService = null)
        : base(
            "Image Workshop",
            "Blend procedural sources, inspect codec metadata via SKCodec, and exercise encode/subset utilities routed through the shim.",
            null,
            captureRecorder,
            backendService,
            resourceService)
    {
        _selectedSource = _sources[0];
        _selectedProcessing = _processingModes[0];

        ExportImageCommand = MiniCommand.Create(ExportImage);
        UpdateMetadataPlaceholders();
    }

    protected override string CaptureLabel => "image-workshop";

    public IReadOnlyList<ImageSourceOption> Sources => _sources;

    public IReadOnlyList<ImageProcessingOption> ProcessingModes => _processingModes;

    public ImageSourceOption SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (SetAndRequestRender(ref _selectedSource, value))
            {
                if (IsAssetSource)
                {
                    Animate = false;
                }

                RaisePropertyChanged(nameof(IsAssetSource));
                RaisePropertyChanged(nameof(IsProceduralSource));
                RaisePropertyChanged(nameof(CanAnimate));

                if (!IsAssetSource)
                {
                    SelectedProcessing = _processingModes[0];
                }

                ResetCachedImage();
                UpdateMetadataPlaceholders();
            }
        }
    }

    public ImageProcessingOption SelectedProcessing
    {
        get => _selectedProcessing;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedProcessing, value))
            {
                if (IsAssetSource)
                {
                    ProcessingSummary = value.Name;
                }

                ResetCachedImage();
                if (IsAssetSource)
                {
                    RequestRender();
                }
            }
        }
    }

    public bool IsAssetSource => SelectedSource.Kind == ImageSourceKind.Asset;

    public bool IsProceduralSource => SelectedSource.Kind == ImageSourceKind.Procedural;

    public bool CanAnimate => IsProceduralSource;

    public double Zoom
    {
        get => _zoom;
        set => SetAndRequestRender(ref _zoom, Math.Clamp(value, 0.2, 3.0));
    }

    public bool ShowHistogram
    {
        get => _showHistogram;
        set => SetAndRequestRender(ref _showHistogram, value);
    }

    public bool Animate
    {
        get => _animate;
        set
        {
            if (SetAndRequestRender(ref _animate, value) && value && !IsProceduralSource)
            {
                _animate = false;
            }
        }
    }

    public string ImageSummary
    {
        get => _imageSummary;
        private set => RaiseAndSetIfChanged(ref _imageSummary, value);
    }

    public string CodecSummary
    {
        get => _codecSummary;
        private set => RaiseAndSetIfChanged(ref _codecSummary, value);
    }

    public string DataSummary
    {
        get => _dataSummary;
        private set => RaiseAndSetIfChanged(ref _dataSummary, value);
    }

    public string ProcessingSummary
    {
        get => _processingSummary;
        private set => RaiseAndSetIfChanged(ref _processingSummary, value);
    }

    public string ExportStatus
    {
        get => _exportStatus;
        private set => RaiseAndSetIfChanged(ref _exportStatus, value);
    }

    public MiniCommand ExportImageCommand { get; }

    public override void Render(in SkiaLeaseRenderContext context)
    {
        var canvas = context.Canvas;
        var info = context.ImageInfo;
        canvas.Clear(new SKColor(12, 18, 28, 255));

        using (var backdrop = new SKPaint
               {
                   Shader = SKShader.CreateLinearGradient(
                       new SKPoint(0, 0),
                       new SKPoint(info.Width, info.Height),
                       new[]
                       {
                           new SKColor(26, 46, 70, 255),
                           new SKColor(18, 28, 42, 255),
                       },
                       null,
                       SKShaderTileMode.Clamp),
                   IsAntialias = true,
               })
        {
            canvas.DrawRect(SKRect.Create(0, 0, info.Width, info.Height), backdrop);
        }

        var frameSeed = IsProceduralSource && Animate ? (int)context.Frame : 0;
        var image = EnsureImage(frameSeed);
        _lastRenderedImage = image;

        var surfaceSize = Math.Min(info.Width, info.Height) * (float)Math.Clamp(Zoom, 0.2, 3.0);
        var destRect = SKRect.Create(
            (info.Width - surfaceSize) * 0.5f,
            (info.Height - surfaceSize) * 0.52f,
            surfaceSize,
            surfaceSize);

        canvas.DrawImage(image, destRect);

        if (ShowHistogram)
        {
            DrawHistogram(canvas, destRect, image);
        }

        DrawMetadata(canvas, info, image, destRect);
        ProcessCapture(context);
    }

    private SKImage EnsureImage(int frameSeed)
    {
        var source = SelectedSource;

        if (_cachedImage is not null &&
            _cachedSource == source &&
            _cachedProcessing == SelectedProcessing &&
            _cachedFrameSeed == frameSeed)
        {
            return _cachedImage;
        }

        _cachedImage?.Dispose();
        _cachedSource = source;
        _cachedProcessing = SelectedProcessing;
        _cachedFrameSeed = frameSeed;

        _cachedImage = source.Kind switch
        {
            ImageSourceKind.Procedural => BuildProceduralImage(source.ProceduralMode!.Value, frameSeed),
            ImageSourceKind.Asset => BuildAssetImage(source, SelectedProcessing.Mode),
            _ => throw new ArgumentOutOfRangeException(nameof(source.Kind), source.Kind, "Unsupported image source."),
        };

        return _cachedImage!;
    }

    private SKImage BuildProceduralImage(ImageMode mode, int frameSeed)
    {
        var info = new SKImageInfo(384, 384, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        switch (mode)
        {
            case ImageMode.Gradient:
                DrawGradientImage(canvas, info, frameSeed);
                ProcessingSummary = "Procedural gradient · dynamic overlay with animated sweep arc.";
                break;
            case ImageMode.Checkerboard:
                DrawCheckerboard(canvas, info, frameSeed);
                ProcessingSummary = "Procedural checkerboard · integer translation offset for animation.";
                break;
            case ImageMode.Plasma:
                DrawPlasma(canvas, info, frameSeed);
                ProcessingSummary = "Procedural plasma · four sine layers blended per pixel.";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported procedural mode.");
        }

        CodecSummary = "Procedural render – generated via SKCanvas primitives.";
        DataSummary = "In-memory surface; no encoded asset.";

        return surface.Snapshot();
    }

    private SKImage BuildAssetImage(ImageSourceOption source, ImageProcessingMode processingMode)
    {
        if (ResourceService is null)
        {
            throw new InvalidOperationException("Resource service is not available.");
        }

        if (string.IsNullOrWhiteSpace(source.AssetPath))
        {
            throw new InvalidOperationException("Asset source missing relative path.");
        }

        using var stream = ResourceService.OpenAsset(source.AssetPath);
        using var managed = new SKManagedStream(stream);
        using var codec = SKCodec.Create(managed)
                       ?? throw new InvalidOperationException($"Unable to create SKCodec for '{source.AssetPath}'.");

        using var decoded = SKBitmap.Decode(codec, codec.Info)
                            ?? throw new InvalidOperationException($"Failed to decode '{source.AssetPath}'.");
        using var processed = ApplyProcessing(decoded, processingMode, out var processingSummary);

        var image = SKImage.FromBitmap(processed)
                   ?? throw new InvalidOperationException($"Failed to construct SKImage for '{source.AssetPath}'.");

        UpdateAssetSummaries(source, codec, processingSummary, image);
        return image;
    }

    private void UpdateAssetSummaries(ImageSourceOption source, SKCodec codec, string processingSummary, SKImage snapshot)
    {
        var info = codec.Info;
        var fileName = Path.GetFileName(source.AssetPath) ?? source.AssetPath;

        CodecSummary = $"{fileName} · {info.Width}×{info.Height}px · {info.ColorType}/{info.AlphaType}";
        DataSummary = BuildDataSummary(source.AssetPath!, snapshot.Width, snapshot.Height);
        ProcessingSummary = processingSummary;
    }

    private string BuildDataSummary(string relativePath, int width, int height)
    {
        if (ResourceService is null)
        {
            return string.Empty;
        }

        using var data = ResourceService.GetData(relativePath);
        var size = data.AsSpan().Length;
        var sizeText = FormatByteSize(size);
        return $"{relativePath} · {sizeText} · Output {width}×{height}px";
    }

    private static string FormatByteSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024d:F1} KB";
        }

        if (bytes < 1024L * 1024 * 1024)
        {
            return $"{bytes / 1024d / 1024d:F1} MB";
        }

        return $"{bytes / 1024d / 1024d / 1024d:F2} GB";
    }

    private static SKBitmap ApplyProcessing(SKBitmap source, ImageProcessingMode mode, out string summary)
    {
        switch (mode)
        {
            case ImageProcessingMode.HalfResolution:
            {
                var destInfo = new SKImageInfo(
                    Math.Max(1, source.Width / 2),
                    Math.Max(1, source.Height / 2),
                    source.ColorType,
                    source.AlphaType);
                var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
                var resized = source.Resize(destInfo, sampling);
                if (resized is null)
                {
                    summary = "Resize failed – falling back to original.";
                    return source.Copy();
                }

                summary = $"{resized.Width}×{resized.Height}px resized (linear sampling).";
                return resized;
            }

            case ImageProcessingMode.Original:
            default:
                summary = $"{source.Width}×{source.Height}px (original pixels)";
                return source.Copy();
        }
    }

    private void DrawHistogram(SKCanvas canvas, SKRect bounds, SKImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var buffer = new byte[info.BytesSize];

        if (!ReadImagePixels(image, info, buffer))
        {
            return;
        }

        const int binCount = 24;
        var bins = new int[binCount];

        var width = info.Width;
        var height = info.Height;
        var rowBytes = info.RowBytes;

        for (var y = 0; y < height; y += 2)
        {
            var rowOffset = y * rowBytes;
            for (var x = 0; x < width; x += 2)
            {
                var offset = rowOffset + x * info.BytesPerPixel;
                var r = buffer[offset];
                var g = buffer[offset + 1];
                var b = buffer[offset + 2];
                var intensity = (r + g + b) / 3;
                var bucket = intensity * binCount / 256;
                bucket = Math.Clamp(bucket, 0, binCount - 1);
                bins[bucket]++;
            }
        }

        var max = 0;
        foreach (var value in bins)
        {
            if (value > max)
            {
                max = value;
            }
        }

        if (max == 0)
        {
            return;
        }

        using var histogramPaint = new SKPaint
        {
            Color = new SKColor(255, 255, 255, 140),
            IsAntialias = true,
        };

        var barWidth = bounds.Width / binCount;
        var bottom = bounds.Bottom - 8;
        var top = bottom - bounds.Height * 0.22f;

        for (var i = 0; i < binCount; i++)
        {
            var ratio = bins[i] / (float)max;
            var barHeight = (top - bottom) * ratio;
            var rect = new SKRect(
                bounds.Left + i * barWidth,
                bottom,
                bounds.Left + (i + 1) * barWidth - 1.5f,
                bottom - barHeight);
            canvas.DrawRect(rect, histogramPaint);
        }
    }

    private void DrawMetadata(SKCanvas canvas, SKImageInfo viewInfo, SKImage image, SKRect imageRect)
    {
        using var textPaint = new SKPaint
        {
            Color = new SKColor(224, 236, 255, 230),
            TextSize = 18f,
            IsAntialias = true,
        };

        var summary = $"{SelectedSource.Name} · Source {image.Width}×{image.Height}px · Zoom {Zoom:F2}×";
        ImageSummary = summary;

        canvas.DrawText(summary, 20f, viewInfo.Height - 32f, textPaint);

        var overlay = $"Dest: {Math.Round(imageRect.Width)}×{Math.Round(imageRect.Height)} px";
        canvas.DrawText(overlay, 20f, viewInfo.Height - 12f, textPaint);
    }

    private void ExportImage()
    {
        var image = _lastRenderedImage ?? _cachedImage;
        if (image is null)
        {
            ExportStatus = "No rendered image available to export.";
            return;
        }

        try
        {
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            if (encoded is null)
            {
                ExportStatus = "Failed to encode snapshot to PNG.";
                return;
            }

            var buffer = encoded.AsSpan().ToArray();
            var root = Path.Combine(AppContext.BaseDirectory, "artifacts", "samples", "skiasharp");
            Directory.CreateDirectory(root);
            var fileName = $"image-workshop-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png";
            var exportPath = Path.Combine(root, fileName);
            File.WriteAllBytes(exportPath, buffer);

            ExportStatus = $"Saved {fileName}";
        }
        catch (NotImplementedException)
        {
            ExportStatus = "Encoding not available in this shim build.";
        }
        catch (Exception ex)
        {
            ExportStatus = $"Export failed: {ex.Message}";
        }
    }

    private void ResetCachedImage()
    {
        _cachedImage?.Dispose();
        _cachedImage = null;
        _cachedSource = null;
        _cachedProcessing = null;
        _cachedFrameSeed = -1;
    }

    private void UpdateMetadataPlaceholders()
    {
        if (IsAssetSource)
        {
            CodecSummary = "Loading asset… (select processing mode to inspect transforms).";
            DataSummary = $"Asset: {SelectedSource.AssetPath}";
            ProcessingSummary = SelectedProcessing.Name;
        }
        else
        {
            CodecSummary = "Procedural render – generated with Skia primitives.";
            DataSummary = "No encoded asset.";
            ProcessingSummary = SelectedSource.ProceduralMode switch
            {
                ImageMode.Gradient => "Gradient shader with animated sweep overlay.",
                ImageMode.Checkerboard => "Checkerboard grid offset each frame seed.",
                ImageMode.Plasma => "Four sine layers with time-based offsets.",
                _ => "Procedural generator.",
            };
        }

        ExportStatus = string.Empty;
    }

    private static bool ReadImagePixels(SKImage image, SKImageInfo info, byte[] buffer)
    {
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            var ptr = handle.AddrOfPinnedObject();
            return image.ReadPixels(info, ptr, info.RowBytes, 0, 0, SKImageCachingHint.Disallow);
        }
        finally
        {
            handle.Free();
        }
    }

    private static void DrawGradientImage(SKCanvas canvas, SKImageInfo info, int frameSeed)
    {
        using var radial = SKShader.CreateRadialGradient(
            new SKPoint(info.Width * 0.5f, info.Height * 0.35f),
            info.Width * 0.6f,
            new[]
            {
                new SKColor(46, 176, 255, 255),
                new SKColor(120, 112, 255, 255),
                new SKColor(255, 146, 196, 255),
            },
            null,
            SKShaderTileMode.Clamp);

        using var paint = new SKPaint
        {
            Shader = radial,
            IsAntialias = true,
        };

        canvas.DrawRect(SKRect.Create(0, 0, info.Width, info.Height), paint);

        var angle = (frameSeed % 360) * (float)Math.PI / 180f;
        var center = new SKPoint(info.Width * 0.5f, info.Height * 0.55f);
        var radius = info.Width * 0.28f;

        using var overlayPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(255, 255, 255, 120),
            StrokeWidth = 18f,
            IsAntialias = true,
            Shader = SKShader.CreateSweepGradient(
                center,
                new[]
                {
                    new SKColor(255, 255, 255, 16),
                    new SKColor(255, 255, 255, 140),
                    new SKColor(255, 255, 255, 16),
                },
                null),
        };

        canvas.Save();
        canvas.Translate(center.X, center.Y);
        canvas.RotateDegrees(angle * 180f / (float)Math.PI);
        canvas.DrawCircle(default, radius, overlayPaint);
        canvas.Restore();
    }

    private static void DrawCheckerboard(SKCanvas canvas, SKImageInfo info, int frameSeed)
    {
        var cell = 48f;
        var offset = (frameSeed * 3) % (int)cell;

        using var paint = new SKPaint
        {
            IsAntialias = false,
        };

        for (var y = -1; y < info.Height / cell + 2; y++)
        {
            for (var x = -1; x < info.Width / cell + 2; x++)
            {
                var parity = (x + y + (offset / (int)cell)) % 2 == 0;
                paint.Color = parity ? new SKColor(30, 45, 70, 255) : new SKColor(140, 210, 255, 220);
                var rect = SKRect.Create(
                    (x * cell) + offset,
                    (y * cell) + offset,
                    cell,
                    cell);
                canvas.DrawRect(rect, paint);
            }
        }

        using var circlePaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(info.Width, info.Height),
                new[]
                {
                    new SKColor(255, 255, 255, 80),
                    new SKColor(255, 255, 255, 0),
                },
                null,
                SKShaderTileMode.Clamp),
            IsAntialias = true,
        };
        canvas.DrawCircle(info.Width * 0.6f, info.Height * 0.38f, 120, circlePaint);
    }

    private static void DrawPlasma(SKCanvas canvas, SKImageInfo info, int frameSeed)
    {
        using var paint = new SKPaint { IsAntialias = true };
        var width = info.Width;
        var height = info.Height;

        for (var y = 0; y < height; y++)
        {
            var rowY = y / 18.0;

            for (var x = 0; x < width; x++)
            {
                var v = (float)(
                    Math.Sin(x / 24.0 + frameSeed * 0.05) +
                    Math.Sin(rowY + frameSeed * 0.03) +
                    Math.Sin((x + y) / 32.0 + frameSeed * 0.02) +
                    Math.Sin(Math.Sqrt(x * x + y * y) / 18.0 + frameSeed * 0.04));

                var normalized = (float)((v + 4) / 8);
                var color = new SKColor(
                    (byte)(normalized * 255),
                    (byte)(Math.Pow(normalized, 0.8) * 255),
                    (byte)(Math.Pow(1 - normalized, 0.6) * 255));

                paint.Color = color;
                canvas.DrawRect(new SKRect(x, y, x + 1, y + 1), paint);
            }
        }
    }

    public sealed record ImageSourceOption(string Name, ImageSourceKind Kind, ImageMode? ProceduralMode = null, string? AssetPath = null);

    public sealed record ImageProcessingOption(string Name, ImageProcessingMode Mode);

    public enum ImageSourceKind
    {
        Procedural,
        Asset,
    }

    public enum ImageProcessingMode
    {
        Original,
        HalfResolution,
    }

    public enum ImageMode
    {
        Gradient,
        Checkerboard,
        Plasma,
    }
}
