using System;
using SkiaSharp;

namespace AvaloniaVelloSkiaSharpSample.Services;

public sealed class SkiaSceneRecorder : IDisposable
{
    private SKPictureRecorder? _recorder;
    private bool _disposed;

    public bool IsRecording => _recorder is not null;

    public SKCanvas BeginRecording(SKRect bounds)
    {
        ThrowIfDisposed();

        if (_recorder is not null)
        {
            throw new InvalidOperationException("A recording is already in progress.");
        }

        _recorder = new SKPictureRecorder();
        return _recorder.BeginRecording(bounds);
    }

    public SKPicture? EndRecording()
    {
        ThrowIfDisposed();

        if (_recorder is null)
        {
            return null;
        }

        var picture = _recorder.EndRecording();
        _recorder.Dispose();
        _recorder = null;
        return picture;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SkiaSceneRecorder));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _recorder?.Dispose();
        _recorder = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
