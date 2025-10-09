using System;
using System.Runtime.InteropServices;
using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Images;

internal sealed class BitmapInstallPixelsScene : ISkiaGalleryScene
{
    public string Title => "Bitmap: Install Pixels";

    public string Description => "Installs custom pixel data into an SKBitmap, peeks pixels, and snapshots the bitmap.";

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(253, 252, 248));

        var installInfo = new SKImageInfo(64, 64, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var stride = installInfo.RowBytes;
        var buffer = new byte[stride * installInfo.Height];

        for (var y = 0; y < installInfo.Height; y++)
        {
            for (var x = 0; x < installInfo.Width; x++)
            {
                var offset = y * stride + x * 4;
                buffer[offset + 0] = (byte)(x * 4);
                buffer[offset + 1] = (byte)(y * 4);
                buffer[offset + 2] = 180;
                buffer[offset + 3] = 255;
            }
        }

        var releaseFlag = new ReleaseTracker();
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            using var bitmap = new SKBitmap();
            SKBitmapReleaseDelegate release = (_, ctx) =>
            {
                if (ctx is ReleaseTracker tracker)
                {
                    tracker.Released = true;
                }
            };
            bitmap.InstallPixels(installInfo, handle.AddrOfPinnedObject(), stride, release, releaseFlag);
            bitmap.Erase(new SKColor(255, 255, 255, 40));
            _ = bitmap.GetPixels();
            bitmap.SetImmutable();

            var pixmap = bitmap.PeekPixels();
            using var image = SKImage.FromBitmap(bitmap)!;

            canvas.DrawImage(image, SKRect.Create(40, 40, 160, 160));

            using var labelPaint = new SKPaint
            {
                Color = new SKColor(120, 120, 120),
                TextSize = 16,
                IsAntialias = true,
            };
            canvas.DrawText($"Release invoked: {releaseFlag.Released}", 40, 220, labelPaint);

            using var stroke = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3,
                Color = new SKColor(40, 60, 90),
                IsAntialias = true,
            };
            canvas.DrawRect(SKRect.Create(40, 40, 160, 160), stroke);
        }
        finally
        {
            handle.Free();
        }
    }

    private sealed class ReleaseTracker
    {
        public bool Released { get; set; }
    }
}
