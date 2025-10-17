using System;
using System.Collections.Generic;

namespace SkiaSharp;

public sealed class SKPicture : IDisposable
{
    private readonly List<ICanvasCommand> _commands;
    private readonly SKRect _cullRect;
    private bool _disposed;

    internal SKPicture(SKRect cullRect, List<ICanvasCommand> commands)
    {
        _cullRect = cullRect;
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public SKRect CullRect => _cullRect;

    internal IReadOnlyList<ICanvasCommand> Commands
    {
        get
        {
            ThrowIfDisposed();
            return _commands;
        }
    }

    public void Playback(SKCanvas canvas)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(canvas);
        foreach (var command in _commands)
        {
            command.Replay(canvas);
        }
    }

    public SKShader ToShader(
        SKShaderTileMode tileModeX,
        SKShaderTileMode tileModeY,
        SKMatrix localMatrix,
        SKRect tileRect)
    {
        ThrowIfDisposed();
        var width = Math.Max(1, (int)Math.Ceiling(tileRect.Width));
        var height = Math.Max(1, (int)Math.Ceiling(tileRect.Height));
        var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul);
        var surface = SKSurface.Create(info);
        try
        {
            var canvas = surface.Canvas;
            if (tileRect.Left != 0 || tileRect.Top != 0)
            {
                canvas.Translate(-tileRect.Left, -tileRect.Top);
            }

            Playback(canvas);
            var image = surface.Snapshot();
            return SKShader.CreateImageShader(image, tileModeX, tileModeY, localMatrix, tileRect, SKSamplingOptions.Default, takeOwnership: true);
        }
        finally
        {
            surface.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _commands.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    internal SKImage Rasterize(SKSizeI dimensions, SKMatrix? localMatrix)
    {
        ThrowIfDisposed();
        if (dimensions.Width <= 0 || dimensions.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions));
        }

        var info = new SKImageInfo(dimensions.Width, dimensions.Height, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul);
        var surface = SKSurface.Create(info);
        try
        {
            var canvas = surface.Canvas;
            if (localMatrix.HasValue)
            {
                canvas.SetMatrix(localMatrix.Value);
            }

            Playback(canvas);
            return surface.Snapshot();
        }
        finally
        {
            surface.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SKPicture));
        }
    }
}
