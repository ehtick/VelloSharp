using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace VelloSharp;

public sealed class VelloSvg : IDisposable
{
    private IntPtr _handle;

    private VelloSvg(IntPtr handle)
    {
        _handle = handle;
    }

    public static VelloSvg LoadFromFile(string path, float scale = 1f)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must not be null or empty.", nameof(path));
        }

        var handle = NativeMethods.vello_svg_load_from_file(path, scale);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to load SVG file.");
        }

        return new VelloSvg(handle);
    }

    public static VelloSvg LoadFromString(string svg, float scale = 1f)
    {
        ArgumentNullException.ThrowIfNull(svg);
        return LoadFromUtf8(Encoding.UTF8.GetBytes(svg), scale);
    }

    public static VelloSvg LoadFromUtf8(ReadOnlySpan<byte> utf8, float scale = 1f)
    {
        if (utf8.IsEmpty)
        {
            throw new ArgumentException("SVG data must not be empty.", nameof(utf8));
        }

        unsafe
        {
            fixed (byte* data = utf8)
            {
                var handle = NativeMethods.vello_svg_load_from_memory(data, (nuint)utf8.Length, scale);
                if (handle == IntPtr.Zero)
                {
                    throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to load SVG data.");
                }

                return new VelloSvg(handle);
            }
        }
    }

    public Vector2 Size
    {
        get
        {
            EnsureNotDisposed();
            var status = NativeMethods.vello_svg_get_size(_handle, out var point);
            NativeHelpers.ThrowOnError(status, "Failed to retrieve SVG size.");
            return new Vector2((float)point.X, (float)point.Y);
        }
    }

    internal IntPtr Handle
    {
        get
        {
            EnsureNotDisposed();
            return _handle;
        }
    }

    public void Render(Scene scene, Matrix3x2? transform = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        EnsureNotDisposed();

        unsafe
        {
            if (transform.HasValue)
            {
                var affine = transform.Value.ToNativeAffine();
                var status = NativeMethods.vello_svg_render(_handle, scene.Handle, &affine);
                NativeHelpers.ThrowOnError(status, "Failed to render SVG.");
            }
            else
            {
                var status = NativeMethods.vello_svg_render(_handle, scene.Handle, null);
                NativeHelpers.ThrowOnError(status, "Failed to render SVG.");
            }
        }
    }

    private void EnsureNotDisposed()
    {
        if (_handle == IntPtr.Zero)
        {
            throw new ObjectDisposedException(nameof(VelloSvg));
        }
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_svg_destroy(_handle);
            _handle = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }
    }

    ~VelloSvg()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_svg_destroy(_handle);
        }
    }
}
