using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace VelloSharp;

public sealed class SparseRenderContext : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    public SparseRenderContext(ushort width, ushort height)
    {
        if (width == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be non-zero.");
        }

        if (height == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be non-zero.");
        }

        _handle = SparseNativeMethods.vello_sparse_render_context_create(width, height);
        if (_handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                NativeHelpers.GetSparseLastErrorMessage() ?? "Failed to create sparse render context.");
        }

        var sizeStatus = SparseNativeMethods.vello_sparse_render_context_get_size(
            _handle,
            out var nativeWidth,
            out var nativeHeight);
        NativeHelpers.ThrowOnError(sizeStatus, "Sparse context size query failed");

        Width = nativeWidth;
        Height = nativeHeight;
    }

    ~SparseRenderContext()
    {
        Dispose(disposing: false);
    }

    public ushort Width { get; }

    public ushort Height { get; }

    public void Reset()
    {
        ThrowIfDisposed();
        var status = SparseNativeMethods.vello_sparse_render_context_reset(_handle);
        NativeHelpers.ThrowOnError(status, "Sparse render context reset failed");
    }

    public void Flush()
    {
        ThrowIfDisposed();
        var status = SparseNativeMethods.vello_sparse_render_context_flush(_handle);
        NativeHelpers.ThrowOnError(status, "Sparse render context flush failed");
    }

    public void SetFillRule(FillRule fillRule)
    {
        ThrowIfDisposed();
        var status = SparseNativeMethods.vello_sparse_render_context_set_fill_rule(
            _handle,
            (VelloFillRule)fillRule);
        NativeHelpers.ThrowOnError(status, "Sparse fill rule update failed");
    }

    public void SetTransform(Matrix3x2 transform)
    {
        ThrowIfDisposed();
        var status = SparseNativeMethods.vello_sparse_render_context_set_transform(
            _handle,
            transform.ToNativeAffine());
        NativeHelpers.ThrowOnError(status, "Sparse transform update failed");
    }

    public void ResetTransform()
    {
        ThrowIfDisposed();
        var status = SparseNativeMethods.vello_sparse_render_context_reset_transform(_handle);
        NativeHelpers.ThrowOnError(status, "Sparse transform reset failed");
    }

    public void SetPaintTransform(Matrix3x2 transform)
    {
        ThrowIfDisposed();
        var status = SparseNativeMethods.vello_sparse_render_context_set_paint_transform(
            _handle,
            transform.ToNativeAffine());
        NativeHelpers.ThrowOnError(status, "Sparse paint transform update failed");
    }

    public void ResetPaintTransform()
    {
        ThrowIfDisposed();
        var status = SparseNativeMethods.vello_sparse_render_context_reset_paint_transform(_handle);
        NativeHelpers.ThrowOnError(status, "Sparse paint transform reset failed");
    }

    public void SetAliasingThreshold(byte? threshold)
    {
        ThrowIfDisposed();
        var status = SparseNativeMethods.vello_sparse_render_context_set_aliasing_threshold(
            _handle,
            threshold.HasValue ? threshold.Value : -1);
        NativeHelpers.ThrowOnError(status, "Sparse aliasing threshold update failed");
    }

    public void SetSolidPaint(RgbaColor color)
    {
        ThrowIfDisposed();
        var status = SparseNativeMethods.vello_sparse_render_context_set_solid_paint(
            _handle,
            color.ToNative());
        NativeHelpers.ThrowOnError(status, "Sparse paint update failed");
    }

    public void SetStroke(StrokeStyle style)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(style);
        var pattern = style.DashPattern;
        if (pattern is { Length: > 0 })
        {
            unsafe
            {
                fixed (double* dashPtr = pattern)
                {
                    var nativeStyle = CreateStrokeStyle(style, (IntPtr)dashPtr, (nuint)pattern.Length);
                    var status = SparseNativeMethods.vello_sparse_render_context_set_stroke(
                        _handle,
                        nativeStyle);
                    NativeHelpers.ThrowOnError(status, "Sparse stroke update failed");
                }
            }
        }
        else
        {
            var nativeStyle = CreateStrokeStyle(style, IntPtr.Zero, 0);
            var status = SparseNativeMethods.vello_sparse_render_context_set_stroke(_handle, nativeStyle);
            NativeHelpers.ThrowOnError(status, "Sparse stroke update failed");
        }
    }

    public void FillPath(PathBuilder path)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(path);
        var span = path.AsSpan();
        unsafe
        {
            fixed (VelloPathElement* ptr = span)
            {
                var status = SparseNativeMethods.vello_sparse_render_context_fill_path(
                    _handle,
                    ptr,
                    (nuint)span.Length);
                NativeHelpers.ThrowOnError(status, "Sparse fill path failed");
            }
        }
    }

    public void StrokePath(PathBuilder path)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(path);
        var span = path.AsSpan();
        unsafe
        {
            fixed (VelloPathElement* ptr = span)
            {
                var status = SparseNativeMethods.vello_sparse_render_context_stroke_path(
                    _handle,
                    ptr,
                    (nuint)span.Length);
                NativeHelpers.ThrowOnError(status, "Sparse stroke path failed");
            }
        }
    }

    public void FillRect(double x, double y, double width, double height)
    {
        ThrowIfDisposed();
        if (width < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be non-negative.");
        }

        if (height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be non-negative.");
        }

        var rect = new VelloRect
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
        };
        var status = SparseNativeMethods.vello_sparse_render_context_fill_rect(_handle, rect);
        NativeHelpers.ThrowOnError(status, "Sparse fill rect failed");
    }

    public void StrokeRect(double x, double y, double width, double height)
    {
        ThrowIfDisposed();
        if (width < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be non-negative.");
        }

        if (height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be non-negative.");
        }

        var rect = new VelloRect
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
        };
        var status = SparseNativeMethods.vello_sparse_render_context_stroke_rect(_handle, rect);
        NativeHelpers.ThrowOnError(status, "Sparse stroke rect failed");
    }

    public void RenderTo(Span<byte> destination, SparseRenderMode mode = SparseRenderMode.OptimizeSpeed)
    {
        ThrowIfDisposed();
        if (destination.IsEmpty)
        {
            throw new ArgumentException("Destination buffer must not be empty.", nameof(destination));
        }

        var expectedBytes = (ulong)Width * Height * 4UL;
        if ((ulong)destination.Length < expectedBytes)
        {
            throw new ArgumentException(
                $"Destination buffer must contain at least {expectedBytes} bytes.",
                nameof(destination));
        }

        unsafe
        {
            fixed (byte* ptr = destination)
            {
                var status = SparseNativeMethods.vello_sparse_render_context_render_to_buffer(
                    _handle,
                    (IntPtr)ptr,
                    (nuint)destination.Length,
                    Width,
                    Height,
                    (VelloSparseRenderMode)mode);
                NativeHelpers.ThrowOnError(status, "Sparse render failed");
            }
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (_handle != IntPtr.Zero)
        {
            SparseNativeMethods.vello_sparse_render_context_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
    }

    private static VelloStrokeStyle CreateStrokeStyle(StrokeStyle style, IntPtr dashPtr, nuint dashLength) => new()
    {
        Width = style.Width,
        MiterLimit = style.MiterLimit,
        StartCap = (VelloLineCap)style.StartCap,
        EndCap = (VelloLineCap)style.EndCap,
        LineJoin = (VelloLineJoin)style.LineJoin,
        DashPhase = style.DashPhase,
        DashPattern = dashPtr,
        DashLength = dashLength,
    };

    private void ThrowIfDisposed()
    {
        if (_disposed || _handle == IntPtr.Zero)
        {
            throw new ObjectDisposedException(nameof(SparseRenderContext));
        }
    }
}
