using System;
using System.Numerics;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal readonly record struct SkiaLeaseRequest(int Width, int Height, Matrix3x2 LocalTransform, bool UseHostScene);

internal static class SkiaLeaseRequestScope
{
    [ThreadStatic]
    private static SkiaLeaseRequest? s_current;

    public static SkiaLeaseRequest? Current => s_current;

    public static IDisposable Activate(SkiaLeaseRequest request)
    {
        var previous = s_current;
        s_current = request;
        return new Scope(previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly SkiaLeaseRequest? _previous;
        private bool _disposed;

        public Scope(SkiaLeaseRequest? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            s_current = _previous;
            _disposed = true;
        }
    }
}
