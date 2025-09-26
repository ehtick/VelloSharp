using System;
using System.Collections.Concurrent;
using System.IO;
using SixLabors.ImageSharp.PixelFormats;
using VelloSharp;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using VelloImage = VelloSharp.Image;

namespace VelloSharp.Scenes;

public sealed class ImageCache : IDisposable
{
    private readonly string? _assetRoot;
    private readonly ConcurrentDictionary<string, VelloImage> _imagesByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<int, VelloImage> _imagesByKey = new();
    private bool _disposed;

    public ImageCache(string? assetRoot)
    {
        _assetRoot = assetRoot;
    }

    public VelloImage GetFromPath(string relativePath)
    {
        EnsureNotDisposed();
        ArgumentException.ThrowIfNullOrEmpty(relativePath);

        return _imagesByPath.GetOrAdd(relativePath, LoadFromPath);
    }

    public VelloImage GetFromBytes(int key, ReadOnlySpan<byte> bytes)
    {
        EnsureNotDisposed();

        var buffer = bytes.ToArray();
        return _imagesByKey.GetOrAdd(key, _ => LoadFromBytes(buffer));
    }

    private VelloImage LoadFromPath(string relativePath)
    {
        var fullPath = _assetRoot is null ? relativePath : Path.Combine(_assetRoot, relativePath);
        using var stream = File.OpenRead(fullPath);
        return LoadFromStream(stream);
    }

    private static VelloImage LoadFromBytes(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        return LoadFromStream(stream);
    }

    private static VelloImage LoadFromStream(Stream stream)
    {
        using var picture = ImageSharpImage.Load<Rgba32>(stream);
        var width = picture.Width;
        var height = picture.Height;
        var pixels = new byte[checked(width * height * 4)];
        picture.CopyPixelDataTo(pixels);
        return VelloImage.FromPixels(pixels, width, height);
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ImageCache));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var entry in _imagesByPath.Values)
        {
            entry.Dispose();
        }
        _imagesByPath.Clear();

        foreach (var entry in _imagesByKey.Values)
        {
            entry.Dispose();
        }
        _imagesByKey.Clear();

        _disposed = true;
    }
}
