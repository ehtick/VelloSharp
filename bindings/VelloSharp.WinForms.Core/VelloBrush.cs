using System;
using System.Numerics;
using VelloSharp;

namespace VelloSharp.WinForms;

public abstract class VelloBrush : IDisposable
{
    private bool _disposed;

    public Matrix3x2 Transform { get; set; } = Matrix3x2.Identity;

    internal Brush CreateCoreBrush(out Matrix3x2? transform)
    {
        ThrowIfDisposed();
        transform = Transform == Matrix3x2.Identity ? null : Transform;
        return CreateCoreBrushCore();
    }

    internal Brush CreateCoreBrush()
    {
        ThrowIfDisposed();
        return CreateCoreBrushCore();
    }

    internal Matrix3x2? GetTransformOrDefault()
    {
        var transform = Transform;
        return transform == Matrix3x2.Identity ? null : transform;
    }

    protected abstract Brush CreateCoreBrushCore();

    protected void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }

    protected virtual void DisposeCore()
    {
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DisposeCore();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
