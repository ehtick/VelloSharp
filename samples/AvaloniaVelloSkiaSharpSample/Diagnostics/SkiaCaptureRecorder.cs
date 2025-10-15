using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

namespace AvaloniaVelloSkiaSharpSample.Diagnostics;

public sealed class SkiaCaptureRecorder
{
    public SkiaCaptureRecorder(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        RootPath = rootPath;
    }

    public string RootPath { get; }

    public Task<string> SaveSnapshotAsync(SKSurface surface, string label, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        using var image = surface.Snapshot();
        return SaveImageCoreAsync(image, label, cancellationToken);
    }

    public Task<string> SaveImageAsync(SKImage image, string label, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        return SaveImageCoreAsync(image, label, cancellationToken);
    }

    private static string Sanitise(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '-');
        }

        return value;
    }

    private async Task<string> SaveImageCoreAsync(SKImage sourceImage, string label, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(RootPath);

        var info = new SKImageInfo(sourceImage.Width, sourceImage.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var buffer = new byte[info.BytesSize];

        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            var ptr = handle.AddrOfPinnedObject();
            if (!sourceImage.ReadPixels(info, ptr, info.RowBytes, 0, 0, SKImageCachingHint.Disallow))
            {
                throw new InvalidOperationException("Failed to read pixels from the captured surface.");
            }
        }
        finally
        {
            handle.Free();
        }

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmssfff");
        var baseName = $"{timestamp}_{Sanitise(label)}".Trim('_');
        var ppmPath = Path.Combine(RootPath, baseName + ".ppm");
        var metadataPath = Path.Combine(RootPath, baseName + ".json");

        await WritePortablePixmapAsync(ppmPath, info, buffer, cancellationToken).ConfigureAwait(false);
        await WriteMetadataAsync(metadataPath, label, info, Path.GetFileName(ppmPath), cancellationToken).ConfigureAwait(false);

        return ppmPath;
    }

    private static async Task WritePortablePixmapAsync(string path, SKImageInfo info, byte[] buffer, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024, useAsync: true);

        var header = Encoding.ASCII.GetBytes($"P6\n{info.Width} {info.Height}\n255\n");
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);

        var rgbRow = new byte[info.Width * 3];
        var sourceRowStride = info.RowBytes;

        for (var y = 0; y < info.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceOffset = y * sourceRowStride;
            var targetIndex = 0;

            for (var x = 0; x < info.Width; x++)
            {
                var pixelOffset = sourceOffset + x * info.BytesPerPixel;
                rgbRow[targetIndex++] = buffer[pixelOffset];     // R
                rgbRow[targetIndex++] = buffer[pixelOffset + 1]; // G
                rgbRow[targetIndex++] = buffer[pixelOffset + 2]; // B
            }

            await stream.WriteAsync(rgbRow.AsMemory(0, targetIndex), cancellationToken).ConfigureAwait(false);
        }

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteMetadataAsync(string path, string label, SKImageInfo info, string imageName, CancellationToken cancellationToken)
    {
        var metadata = new SnapshotMetadata(
            label,
            info.Width,
            info.Height,
            imageName,
            "PPM (P6 RGB)",
            DateTimeOffset.UtcNow);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4 * 1024, useAsync: true);
        await JsonSerializer.SerializeAsync(stream, metadata, options, cancellationToken).ConfigureAwait(false);
    }

    private sealed record SnapshotMetadata(
        string Label,
        int Width,
        int Height,
        string Image,
        string Format,
        DateTimeOffset CapturedUtc);
}
