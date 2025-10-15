using System;
using System.IO;
using System.Text;
using AvaloniaVelloSkiaSharpSample.Diagnostics;
using AvaloniaVelloSkiaSharpSample.Navigation;
using AvaloniaVelloSkiaSharpSample.Rendering;
using AvaloniaVelloSkiaSharpSample.Services;
using MiniMvvm;
using SkiaSharp;
using SampleSkiaBackendDescriptor = AvaloniaVelloSkiaSharpSample.Services.SkiaBackendDescriptor;
using SampleSkiaBackendService = AvaloniaVelloSkiaSharpSample.Services.SkiaBackendService;

namespace AvaloniaVelloSkiaSharpSample.ViewModels.Pages;

public sealed class IoDiagnosticsViewModel : SamplePageViewModel
{
    private readonly SkiaResourceService _resourceService;
    private SKImage? _cachedImage;
    private string _imageSummary = string.Empty;
    private string _dataSummary = string.Empty;
    private string _streamSummary = string.Empty;
    private bool _showColorSpace = true;

    public IoDiagnosticsViewModel(
        SkiaResourceService resourceService,
        SkiaCaptureRecorder? captureRecorder = null,
        SampleSkiaBackendService? backendService = null)
        : base(
            "IO & Diagnostics Workbench",
            "Inspect encoded metadata, color spaces, and shim diagnostics powered by Skia stream APIs.",
            null,
            captureRecorder,
            backendService,
            resourceService)
    {
        _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
        RefreshCommand = MiniCommand.Create(RefreshMetadata);
        RefreshMetadata();

        SetDocumentationLinks(
            new DocumentationLink("SkiaSharp Streams", new Uri("https://learn.microsoft.com/xamarin/graphics-games/skiasharp/streams")),
            new DocumentationLink("SKEncodedInfo", new Uri("https://api.skia.org/classSkEncodedInfo.html")));
    }

    public MiniCommand RefreshCommand { get; }

    public string ImageSummary
    {
        get => _imageSummary;
        private set => RaiseAndSetIfChanged(ref _imageSummary, value);
    }

    public string DataSummary
    {
        get => _dataSummary;
        private set => RaiseAndSetIfChanged(ref _dataSummary, value);
    }

    public string StreamSummary
    {
        get => _streamSummary;
        private set => RaiseAndSetIfChanged(ref _streamSummary, value);
    }

    public bool ShowColorSpace
    {
        get => _showColorSpace;
        set => SetAndRequestRender(ref _showColorSpace, value);
    }

    public override void Render(in SkiaLeaseRenderContext context)
    {
        var canvas = context.Canvas;
        var info = context.ImageInfo;
        canvas.Clear(new SKColor(8, 14, 24, 255));

        var image = EnsureImage();
        var targetSize = Math.Min(info.Width, info.Height) * 0.6f;
        var destRect = SKRect.Create(
            (info.Width - targetSize) * 0.5f,
            (info.Height - targetSize) * 0.32f,
            targetSize,
            targetSize);
        canvas.DrawImage(image, destRect);

        if (ShowColorSpace)
        {
            using var colors = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
            for (var i = 0; i < 6; i++)
            {
                var t = i / 6f;
                var hue = (byte)(MathF.Sin((float)(context.Elapsed.TotalSeconds * 0.6 + t * Math.PI)) * 120 + 120);
                colors.Color = new SKColor((byte)(80 + t * 140), hue, (byte)(200 - t * 110), 180);
                var bar = new SKRect(destRect.Left + i * destRect.Width / 6f, destRect.Bottom + 20, destRect.Left + (i + 1) * destRect.Width / 6f - 6, destRect.Bottom + 68);
                canvas.DrawRect(bar, colors);
            }
        }

        using var caption = new SKPaint
        {
            Color = new SKColor(235, 240, 255, 255),
            IsAntialias = true,
            TextSize = info.Width * 0.035f,
        };
        canvas.DrawText("Resource Diagnostics", info.Width * 0.08f, info.Height * 0.82f, caption);

        using var backendPaint = new SKPaint
        {
            Color = new SKColor(150, 200, 255, 180),
            TextSize = info.Width * 0.024f,
            IsAntialias = true,
        };
        canvas.DrawText($"Backend: {context.Backend.Title}", info.Width * 0.08f, info.Height * 0.88f, backendPaint);

        ProcessCapture(context);
    }

    protected override void OnBackendChanged(SampleSkiaBackendDescriptor descriptor)
    {
        base.OnBackendChanged(descriptor);
        RefreshMetadata();
    }

    private SKImage EnsureImage()
    {
        _cachedImage ??= _resourceService.GetImage("images/aurora-gradient.png");
        return _cachedImage;
    }

    private void RefreshMetadata()
    {
        var image = EnsureImage();
        ImageSummary = $"{image.Width}×{image.Height}px · RGBA surface";

        using var data = _resourceService.GetData("images/aurora-gradient.png");
        DataSummary = $"PNG bytes: {data.AsSpan().Length}";

        using var stream = _resourceService.OpenAsset("shaders/aurora.sksl");
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: false);
        var preview = reader.ReadLine() ?? string.Empty;
        if (preview.Length > 48)
        {
            preview = preview[..48] + "…";
        }
        StreamSummary = $"Shader snippet: {preview}";
    }
}
