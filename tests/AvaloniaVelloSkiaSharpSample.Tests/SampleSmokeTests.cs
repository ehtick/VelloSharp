extern alias ShimSkia;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using AvaloniaVelloSkiaSharpSample.ViewModels;
using AvaloniaVelloSkiaSharpSample.ViewModels.Pages;
using Xunit;
using AvaloniaVelloSkiaSharpSample.Services;

using SKImageInfo = ShimSkia::SkiaSharp.SKImageInfo;
using SKSurface = ShimSkia::SkiaSharp.SKSurface;
using SKColorType = ShimSkia::SkiaSharp.SKColorType;
using SKAlphaType = ShimSkia::SkiaSharp.SKAlphaType;

namespace AvaloniaVelloSkiaSharpSample.Tests;

public sealed class SampleSmokeTests
{
    [Fact]
    public async Task Pages_Render_And_CaptureSnapshots()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "avalonia-vello-skiasharp-sample", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var captureRecorder = new AvaloniaVelloSkiaSharpSample.Diagnostics.SkiaCaptureRecorder(tempRoot);
            using var viewModel = new MainWindowViewModel(captureRecorder: captureRecorder);

            var info = new SKImageInfo(640, 480, SKColorType.Bgra8888, SKAlphaType.Premul);
            var viewBounds = new Rect(0, 0, info.Width, info.Height);
            var elapsed = TimeSpan.FromMilliseconds(16);
            ulong frame = 0;

            foreach (var page in viewModel.Pages)
            {
                using var surface = SKSurface.Create(info);
                var backend = page.BackendDescriptor;
                var context = new AvaloniaVelloSkiaSharpSample.Rendering.SkiaLeaseRenderContext(
                    surface,
                    surface.Canvas,
                    info,
                    viewBounds,
                    1.0,
                    elapsed,
                    frame++,
                    backend);

                page.Render(context);

                page.CaptureSnapshotCommand.Execute(null!);

                var captureContext = new AvaloniaVelloSkiaSharpSample.Rendering.SkiaLeaseRenderContext(
                    surface,
                    surface.Canvas,
                    info,
                    viewBounds,
                    1.0,
                    elapsed + TimeSpan.FromMilliseconds(16),
                    frame++,
                    backend);
                page.Render(captureContext);

                await WaitForCaptureAsync(page);
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static async Task WaitForCaptureAsync(SamplePageViewModel page)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(5))
        {
            var status = page.CaptureStatus ?? string.Empty;
            if (string.IsNullOrEmpty(status) ||
                status.StartsWith("Saved", StringComparison.OrdinalIgnoreCase) ||
                status.StartsWith("Capture failed", StringComparison.OrdinalIgnoreCase) ||
                status.StartsWith("Runtime effects are not available", StringComparison.OrdinalIgnoreCase) ||
                status.StartsWith("Capture unavailable", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"Capture did not complete for page '{page.Title}'. Status: '{page.CaptureStatus}'.");
    }
}
