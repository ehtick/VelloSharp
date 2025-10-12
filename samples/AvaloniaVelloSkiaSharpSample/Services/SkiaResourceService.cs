using System;
using System.Collections.Concurrent;
using System.IO;
using SkiaSharp;

namespace AvaloniaVelloSkiaSharpSample.Services;

public sealed class SkiaResourceService : IDisposable
{
    private readonly ConcurrentDictionary<string, SKTypeface> _typefaces = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SKData> _dataCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SKImage> _imageCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public SkiaResourceService(string? assetRoot = null)
    {
        AssetRoot = assetRoot ?? AppContext.BaseDirectory;
    }

    public string AssetRoot { get; }

    public SKTypeface GetTypeface(string relativePath)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var fullPath = ResolvePath(relativePath);
        return _typefaces.GetOrAdd(fullPath, path =>
        {
            using var stream = File.OpenRead(path);
            return SKTypeface.FromStream(stream);
        });
    }

    public SKData GetData(string relativePath)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var fullPath = ResolvePath(relativePath);
        return _dataCache.GetOrAdd(fullPath, path =>
        {
            using var fs = File.OpenRead(path);
            return SKData.Create(fs) ?? throw new InvalidOperationException($"Failed to create SKData from '{path}'.");
        });
    }

    public SKImage GetImage(string relativePath)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var fullPath = ResolvePath(relativePath);
        return _imageCache.GetOrAdd(fullPath, path =>
        {
            var data = GetData(relativePath);
            var image = SKImage.FromEncodedData(data);
            if (image is null)
            {
                throw new InvalidOperationException($"Failed to create SKImage from '{path}'.");
            }
            return image;
        });
    }

    public Stream OpenAsset(string relativePath)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var fullPath = ResolvePath(relativePath);
        return File.OpenRead(fullPath);
    }

    private string ResolvePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            return relativePath;
        }

        return Path.GetFullPath(Path.Combine(AssetRoot, relativePath));
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SkiaResourceService));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var entry in _imageCache.Values)
        {
            entry.Dispose();
        }

        foreach (var entry in _dataCache.Values)
        {
            entry.Dispose();
        }

        foreach (var entry in _typefaces.Values)
        {
            entry.Dispose();
        }

        _imageCache.Clear();
        _dataCache.Clear();
        _typefaces.Clear();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
