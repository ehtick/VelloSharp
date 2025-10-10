using System;

namespace SkiaSharp;

public sealed class SKPathMeasure : IDisposable
{
    private readonly SKPath _path;
    private bool _disposed;

    public SKPathMeasure(SKPath path, bool forceClosed = false, float resScale = 1f)
    {
        ArgumentNullException.ThrowIfNull(path);
        _path = path;
        ForceClosed = forceClosed;
        ResScale = resScale;
        ShimNotImplemented.Throw($"{nameof(SKPathMeasure)}.ctor", "path measurement");
    }

    public SKPathMeasure(SKPath path)
        : this(path, false, 1f)
    {
    }

    public bool ForceClosed { get; }

    public float ResScale { get; }

    public float Length
    {
        get
        {
            EnsureNotDisposed();
            ShimNotImplemented.Throw($"{nameof(SKPathMeasure)}.{nameof(Length)}");
            return 0f;
        }
    }

    public bool GetPosition(float distance, out SKPoint position)
    {
        EnsureNotDisposed();
        ShimNotImplemented.Throw($"{nameof(SKPathMeasure)}.{nameof(GetPosition)}");
        position = default;
        _ = distance;
        return false;
    }

    public bool GetPositionAndTangent(float distance, out SKPoint position, out SKPoint tangent)
    {
        EnsureNotDisposed();
        ShimNotImplemented.Throw($"{nameof(SKPathMeasure)}.{nameof(GetPositionAndTangent)}");
        position = default;
        tangent = default;
        _ = distance;
        return false;
    }

    public bool GetSegment(float startD, float stopD, SKPath destination, bool startWithMoveTo)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(destination);
        ShimNotImplemented.Throw($"{nameof(SKPathMeasure)}.{nameof(GetSegment)}");
        _ = startD;
        _ = stopD;
        _ = startWithMoveTo;
        destination.Reset();
        return false;
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SKPathMeasure));
        }
    }
}
