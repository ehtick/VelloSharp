using System;
using System.Collections.Generic;
using VelloSharp;

namespace SkiaSharp;

public sealed class SKPictureRecorder : IDisposable
{
    private List<ICanvasCommand>? _commands;
    private Scene? _scene;
    private SKCanvas? _canvas;
    private SKRect _cullRect;
    private bool _disposed;

    public SKCanvas BeginRecording(SKRect bounds)
    {
        ThrowIfDisposed();
        if (_canvas is not null)
        {
            throw new InvalidOperationException("Recording already in progress.");
        }

        _commands = new List<ICanvasCommand>();
        _scene = new Scene();
        _cullRect = bounds;
        _canvas = new SKCanvas(_scene, bounds.Width, bounds.Height, _commands);
        return _canvas;
    }

    public SKCanvas BeginRecording(SKRectI bounds)
    {
        var rect = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        return BeginRecording(rect);
    }

    public SKPicture EndRecording()
    {
        ThrowIfDisposed();
        if (_canvas is null || _commands is null)
        {
            throw new InvalidOperationException("No recording in progress.");
        }

        var commands = _commands;
        var cull = _cullRect;

        _canvas = null;
        _commands = null;

        _scene?.Dispose();
        _scene = null;

        return new SKPicture(cull, commands);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _scene?.Dispose();
        _commands?.Clear();
        _commands = null;
        _canvas = null;
        _scene = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SKPictureRecorder));
        }
    }
}
