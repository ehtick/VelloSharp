using System;
using AvaloniaVelloHarfBuzzSample.Diagnostics;

namespace AvaloniaVelloHarfBuzzSample.Services;

public sealed class HarfBuzzSampleServices
{
    public HarfBuzzSampleServices(
        FontAssetService fontAssets,
        HarfBuzzShapeService shapeService,
        ShapeCaptureRecorder captureRecorder)
    {
        FontAssets = fontAssets ?? throw new ArgumentNullException(nameof(fontAssets));
        ShapeService = shapeService ?? throw new ArgumentNullException(nameof(shapeService));
        CaptureRecorder = captureRecorder ?? throw new ArgumentNullException(nameof(captureRecorder));
    }

    public FontAssetService FontAssets { get; }

    public HarfBuzzShapeService ShapeService { get; }

    public ShapeCaptureRecorder CaptureRecorder { get; }
}
