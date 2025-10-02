using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;

namespace VelloSharp;

public readonly struct VelatoCompositionInfo
{
    public VelatoCompositionInfo(double startFrame, double endFrame, double frameRate, uint width, uint height)
    {
        StartFrame = startFrame;
        EndFrame = endFrame;
        FrameRate = frameRate;
        Width = width;
        Height = height;
    }

    public double StartFrame { get; }
    public double EndFrame { get; }
    public double FrameRate { get; }
    public uint Width { get; }
    public uint Height { get; }

    public Vector2 Size => new((float)Width, (float)Height);
}

public sealed class VelatoComposition : IDisposable
{
    private IntPtr _handle;

    private VelatoComposition(IntPtr handle)
    {
        _handle = handle;
    }

    public static VelatoComposition LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must not be null or empty.", nameof(path));
        }

        var handle = NativeMethods.vello_velato_composition_load_from_file(path);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to load Lottie composition from file.");
        }

        return new VelatoComposition(handle);
    }

    public static VelatoComposition LoadFromString(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        Span<byte> scratch = stackalloc byte[512];
        byte[]? rented = null;
        try
        {
            var utf8 = NativeHelpers.EncodeUtf8(json, scratch, ref rented);
            return LoadFromUtf8(utf8);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    public static VelatoComposition LoadFromUtf8(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty)
        {
            throw new ArgumentException("Lottie data must not be empty.", nameof(utf8));
        }

        unsafe
        {
            fixed (byte* data = utf8)
            {
                var handle = NativeMethods.vello_velato_composition_load_from_memory(data, (nuint)utf8.Length);
                if (handle == IntPtr.Zero)
                {
                    throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to load Lottie composition from memory.");
                }

                return new VelatoComposition(handle);
            }
        }
    }

    public VelatoCompositionInfo Info
    {
        get
        {
            EnsureNotDisposed();
            var status = NativeMethods.vello_velato_composition_get_info(_handle, out var info);
            NativeHelpers.ThrowOnError(status, "Failed to retrieve Velato composition info.");
            return new VelatoCompositionInfo(info.StartFrame, info.EndFrame, info.FrameRate, info.Width, info.Height);
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

    private void EnsureNotDisposed()
    {
        if (_handle == IntPtr.Zero)
        {
            throw new ObjectDisposedException(nameof(VelatoComposition));
        }
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_velato_composition_destroy(_handle);
            _handle = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }
    }

    ~VelatoComposition()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_velato_composition_destroy(_handle);
        }
    }
}

public sealed class VelatoRenderer : IDisposable
{
    private IntPtr _handle;

    public VelatoRenderer()
    {
        _handle = NativeMethods.vello_velato_renderer_create();
        if (_handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create Velato renderer.");
        }
    }

    public void Append(Scene scene, VelatoComposition composition, double frame, Matrix3x2? transform = null, double alpha = 1.0)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(composition);
        EnsureNotDisposed();

        unsafe
        {
            if (transform.HasValue)
            {
                var native = transform.Value.ToNativeAffine();
                var status = NativeMethods.vello_velato_renderer_render(
                    _handle,
                    composition.Handle,
                    scene.Handle,
                    frame,
                    alpha,
                    &native);
                NativeHelpers.ThrowOnError(status, "Failed to render Velato composition.");
            }
            else
            {
                var status = NativeMethods.vello_velato_renderer_render(
                    _handle,
                    composition.Handle,
                    scene.Handle,
                    frame,
                    alpha,
                    null);
                NativeHelpers.ThrowOnError(status, "Failed to render Velato composition.");
            }
        }
    }

    public Scene Render(VelatoComposition composition, double frame, Matrix3x2? transform = null, double alpha = 1.0)
    {
        ArgumentNullException.ThrowIfNull(composition);
        EnsureNotDisposed();
        var scene = new Scene();
        Append(scene, composition, frame, transform, alpha);
        return scene;
    }

    private void EnsureNotDisposed()
    {
        if (_handle == IntPtr.Zero)
        {
            throw new ObjectDisposedException(nameof(VelatoRenderer));
        }
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_velato_renderer_destroy(_handle);
            _handle = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }
    }

    ~VelatoRenderer()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_velato_renderer_destroy(_handle);
        }
    }
}
