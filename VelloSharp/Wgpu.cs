using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace VelloSharp;

[Flags]
public enum WgpuBackend : uint
{
    Vulkan = 1u << 0,
    Gl = 1u << 1,
    Metal = 1u << 2,
    Dx12 = 1u << 3,
    BrowserWebGpu = 1u << 4,
}

[Flags]
public enum WgpuFeature : ulong
{
    None = 0,
    TextureAdapterSpecificFormatFeatures = 1ul << 0,
    TimestampQuery = 1ul << 1,
    PipelineStatisticsQuery = 1ul << 2,
    PushConstants = 1ul << 3,
    TextureCompressionBc = 1ul << 4,
    TextureCompressionEtc2 = 1ul << 5,
    TextureCompressionAstc = 1ul << 6,
    IndirectFirstInstance = 1ul << 7,
    MappablePrimaryBuffers = 1ul << 8,
}

[Flags]
public enum WgpuTextureUsage : uint
{
    None = 0,
    CopySrc = 1u << 0,
    CopyDst = 1u << 1,
    TextureBinding = 1u << 2,
    StorageBinding = 1u << 3,
    RenderAttachment = 1u << 4,
    VideoDecode = 1u << 5,
    VideoEncode = 1u << 6,
}

public enum WgpuPowerPreference
{
    None,
    LowPower,
    HighPerformance,
}

public enum WgpuDx12Compiler
{
    Default,
    Fxc,
    Dxc,
}

public enum WgpuLimitsPreset
{
    Default,
    DownlevelWebGl2,
    DownlevelDefault,
    AdapterDefault,
}

public enum WgpuCompositeAlphaMode
{
    Auto,
    Opaque,
    Premultiplied,
    PostMultiplied,
    Inherit,
}

public enum WgpuTextureFormat
{
    Rgba8Unorm,
    Rgba8UnormSrgb,
    Bgra8Unorm,
    Bgra8UnormSrgb,
    Rgba16Float,
}

public enum WgpuTextureViewDimension
{
    Default,
    D1,
    D2,
    D2Array,
    Cube,
    CubeArray,
    D3,
}

public enum WgpuTextureAspect
{
    All,
    StencilOnly,
    DepthOnly,
    Plane0,
    Plane1,
}

public readonly struct WgpuInstanceOptions
{
    public WgpuBackend Backends { get; init; }
    public uint Flags { get; init; }
    public WgpuDx12Compiler Dx12ShaderCompiler { get; init; }
}

public readonly struct WgpuRequestAdapterOptions
{
    public WgpuPowerPreference PowerPreference { get; init; }
    public bool ForceFallbackAdapter { get; init; }
    public WgpuSurface? CompatibleSurface { get; init; }
}

public readonly struct WgpuDeviceDescriptor
{
    public string? Label { get; init; }
    public WgpuFeature RequiredFeatures { get; init; }
    public WgpuLimitsPreset Limits { get; init; }
}

public readonly struct WgpuSurfaceConfiguration
{
    public WgpuTextureUsage Usage { get; init; }
    public WgpuTextureFormat Format { get; init; }
    public uint Width { get; init; }
    public uint Height { get; init; }
    public PresentMode PresentMode { get; init; }
    public WgpuCompositeAlphaMode AlphaMode { get; init; }
    public IReadOnlyList<WgpuTextureFormat>? ViewFormats { get; init; }
}

public readonly struct WgpuTextureViewDescriptor
{
    public string? Label { get; init; }
    public WgpuTextureFormat Format { get; init; }
    public WgpuTextureViewDimension Dimension { get; init; }
    public WgpuTextureAspect Aspect { get; init; }
    public uint BaseMipLevel { get; init; }
    public uint? MipLevelCount { get; init; }
    public uint BaseArrayLayer { get; init; }
    public uint? ArrayLayerCount { get; init; }
}

public sealed class WgpuInstance : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    public WgpuInstance(WgpuInstanceOptions? options = null)
    {
        unsafe
        {
            IntPtr handle;
            if (options.HasValue)
            {
                var native = new WgpuInstanceDescriptorNative
                {
                    Backends = (uint)options.Value.Backends,
                    Flags = options.Value.Flags,
                    Dx12ShaderCompiler = (WgpuDx12CompilerNative)options.Value.Dx12ShaderCompiler,
                };
                var buffer = stackalloc WgpuInstanceDescriptorNative[1];
                buffer[0] = native;
                handle = NativeMethods.vello_wgpu_instance_create(buffer);
            }
            else
            {
                handle = NativeMethods.vello_wgpu_instance_create(null);
            }

            if (handle == IntPtr.Zero)
            {
                throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create wgpu instance.");
            }

            _handle = handle;
        }
    }

    internal IntPtr Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    public WgpuAdapter RequestAdapter(WgpuRequestAdapterOptions? options = null)
    {
        ThrowIfDisposed();
        unsafe
        {
            IntPtr adapter;
            if (options.HasValue)
            {
                var native = new WgpuRequestAdapterOptionsNative
                {
                    PowerPreference = (WgpuPowerPreferenceNative)options.Value.PowerPreference,
                    ForceFallbackAdapter = options.Value.ForceFallbackAdapter,
                    CompatibleSurface = options.Value.CompatibleSurface?.Handle ?? IntPtr.Zero,
                };
                var buffer = stackalloc WgpuRequestAdapterOptionsNative[1];
                buffer[0] = native;
                adapter = NativeMethods.vello_wgpu_instance_request_adapter(_handle, buffer);
            }
            else
            {
                adapter = NativeMethods.vello_wgpu_instance_request_adapter(_handle, null);
            }

            if (adapter == IntPtr.Zero)
            {
                throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to request adapter.");
            }

            return new WgpuAdapter(adapter);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_instance_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgpuInstance()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_instance_destroy(_handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WgpuInstance));
        }
    }
}

public sealed class WgpuAdapter : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    internal WgpuAdapter(IntPtr handle)
    {
        _handle = handle;
    }

    internal IntPtr Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    public WgpuDevice RequestDevice(WgpuDeviceDescriptor? descriptor = null)
    {
        ThrowIfDisposed();
        unsafe
        {
            IntPtr device;
            if (descriptor.HasValue)
            {
                IntPtr labelPtr = IntPtr.Zero;
                try
                {
                    var native = new WgpuDeviceDescriptorNative
                    {
                        RequiredFeatures = (ulong)descriptor.Value.RequiredFeatures,
                        Limits = (WgpuLimitsPresetNative)descriptor.Value.Limits,
                    };
                    if (!string.IsNullOrEmpty(descriptor.Value.Label))
                    {
                        labelPtr = Marshal.StringToCoTaskMemUTF8(descriptor.Value.Label);
                        native.Label = labelPtr;
                    }
                    var buffer = stackalloc WgpuDeviceDescriptorNative[1];
                    buffer[0] = native;
                    device = NativeMethods.vello_wgpu_adapter_request_device(_handle, buffer);
                }
                finally
                {
                    if (labelPtr != IntPtr.Zero)
                    {
                        Marshal.FreeCoTaskMem(labelPtr);
                    }
                }
            }
            else
            {
                device = NativeMethods.vello_wgpu_adapter_request_device(_handle, null);
            }

            if (device == IntPtr.Zero)
            {
                throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to request device.");
            }

            return new WgpuDevice(device);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_adapter_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgpuAdapter()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_adapter_destroy(_handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WgpuAdapter));
        }
    }
}

public sealed class WgpuDevice : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    internal WgpuDevice(IntPtr handle)
    {
        _handle = handle;
    }

    internal IntPtr Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    public WgpuQueue GetQueue()
    {
        ThrowIfDisposed();
        var handle = NativeMethods.vello_wgpu_device_get_queue(_handle);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to get queue.");
        }

        return new WgpuQueue(handle);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_device_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgpuDevice()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_device_destroy(_handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WgpuDevice));
        }
    }
}

public sealed class WgpuQueue : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    internal WgpuQueue(IntPtr handle)
    {
        _handle = handle;
    }

    internal IntPtr Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_queue_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgpuQueue()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_queue_destroy(_handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WgpuQueue));
        }
    }
}

public sealed class WgpuSurface : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    private WgpuSurface(IntPtr handle)
    {
        _handle = handle;
    }

    public static WgpuSurface Create(WgpuInstance instance, SurfaceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var nativeDescriptor = descriptor.ToNative();
        var handle = NativeMethods.vello_wgpu_surface_create(instance.Handle, nativeDescriptor);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create wgpu surface.");
        }

        return new WgpuSurface(handle);
    }

    internal IntPtr Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    public WgpuTextureFormat GetPreferredFormat(WgpuAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ThrowIfDisposed();
        unsafe
        {
            WgpuTextureFormatNative format;
            var status = NativeMethods.vello_wgpu_surface_get_preferred_format(_handle, adapter.Handle, &format);
            NativeHelpers.ThrowOnError(status, "Failed to get preferred surface format.");
            return (WgpuTextureFormat)format;
        }
    }

    public void Configure(WgpuDevice device, WgpuSurfaceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(device);
        ThrowIfDisposed();
        unsafe
        {
            var viewFormats = configuration.ViewFormats;
            int count = viewFormats?.Count ?? 0;
            var native = new WgpuSurfaceConfigurationNative
            {
                Usage = (uint)configuration.Usage,
                Format = (WgpuTextureFormatNative)configuration.Format,
                Width = configuration.Width,
                Height = configuration.Height,
                PresentMode = (VelloPresentMode)configuration.PresentMode,
                AlphaMode = (WgpuCompositeAlphaModeNative)configuration.AlphaMode,
                ViewFormatCount = (nuint)count,
                ViewFormats = IntPtr.Zero,
            };

            if (count == 0)
            {
                var status = NativeMethods.vello_wgpu_surface_configure(_handle, device.Handle, &native);
                NativeHelpers.ThrowOnError(status, "Failed to configure surface.");
                return;
            }

            var formats = new WgpuTextureFormatNative[count];
            if (viewFormats is not null)
            {
                for (int i = 0; i < count; i++)
                {
                    formats[i] = (WgpuTextureFormatNative)viewFormats[i];
                }
            }

            fixed (WgpuTextureFormatNative* ptr = formats)
            {
                native.ViewFormats = (IntPtr)ptr;
                var status = NativeMethods.vello_wgpu_surface_configure(_handle, device.Handle, &native);
                NativeHelpers.ThrowOnError(status, "Failed to configure surface.");
            }
        }
    }

    public WgpuSurfaceTexture AcquireNextTexture()
    {
        ThrowIfDisposed();
        var handle = NativeMethods.vello_wgpu_surface_acquire_next_texture(_handle);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to acquire surface texture.");
        }

        return new WgpuSurfaceTexture(handle);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_surface_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgpuSurface()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_surface_destroy(_handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WgpuSurface));
        }
    }
}

public sealed class WgpuSurfaceTexture : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    internal WgpuSurfaceTexture(IntPtr handle)
    {
        _handle = handle;
    }

    public WgpuTextureView CreateView(WgpuTextureViewDescriptor? descriptor = null)
    {
        ThrowIfDisposed();
        unsafe
        {
            IntPtr viewHandle;
            if (descriptor.HasValue)
            {
                IntPtr labelPtr = IntPtr.Zero;
                try
                {
                    var native = new WgpuTextureViewDescriptorNative
                    {
                        Format = (WgpuTextureFormatNative)descriptor.Value.Format,
                        Dimension = (uint)descriptor.Value.Dimension,
                        Aspect = (uint)descriptor.Value.Aspect,
                        BaseMipLevel = descriptor.Value.BaseMipLevel,
                        MipLevelCount = descriptor.Value.MipLevelCount ?? 0,
                        BaseArrayLayer = descriptor.Value.BaseArrayLayer,
                        ArrayLayerCount = descriptor.Value.ArrayLayerCount ?? 0,
                    };

                    if (!string.IsNullOrEmpty(descriptor.Value.Label))
                    {
                        labelPtr = Marshal.StringToCoTaskMemUTF8(descriptor.Value.Label);
                        native.Label = labelPtr;
                    }

                    var buffer = stackalloc WgpuTextureViewDescriptorNative[1];
                    buffer[0] = native;
                    viewHandle = NativeMethods.vello_wgpu_surface_texture_create_view(_handle, buffer);
                }
                finally
                {
                    if (labelPtr != IntPtr.Zero)
                    {
                        Marshal.FreeCoTaskMem(labelPtr);
                    }
                }
            }
            else
            {
                viewHandle = NativeMethods.vello_wgpu_surface_texture_create_view(_handle, null);
            }

            if (viewHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create texture view.");
            }

            return new WgpuTextureView(viewHandle);
        }
    }

    public void Present()
    {
        ThrowIfDisposed();
        NativeMethods.vello_wgpu_surface_texture_present(_handle);
        _handle = IntPtr.Zero;
        _disposed = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_surface_texture_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgpuSurfaceTexture()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_surface_texture_destroy(_handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WgpuSurfaceTexture));
        }
    }
}

public sealed class WgpuTextureView : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    internal WgpuTextureView(IntPtr handle)
    {
        _handle = handle;
    }

    internal IntPtr Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_texture_view_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgpuTextureView()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_texture_view_destroy(_handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WgpuTextureView));
        }
    }
}

public sealed class WgpuRenderer : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    public WgpuRenderer(WgpuDevice device, RendererOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        var nativeOptions = (options ?? new RendererOptions()).ToNative();
        _handle = NativeMethods.vello_wgpu_renderer_create(device.Handle, nativeOptions);
        if (_handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create wgpu renderer.");
        }
    }

    internal IntPtr Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    public void Render(Scene scene, WgpuTextureView textureView, RenderParams parameters)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(textureView);
        ThrowIfDisposed();

        var nativeParams = new VelloRenderParams
        {
            Width = parameters.Width,
            Height = parameters.Height,
            BaseColor = parameters.BaseColor.ToNative(),
            Antialiasing = (VelloAaMode)parameters.Antialiasing,
            Format = (VelloRenderFormat)parameters.Format,
        };

        var status = NativeMethods.vello_wgpu_renderer_render(
            _handle,
            scene.Handle,
            textureView.Handle,
            nativeParams);

        NativeHelpers.ThrowOnError(status, "Failed to render to wgpu texture.");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WgpuRenderer));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_renderer_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgpuRenderer()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_renderer_destroy(_handle);
        }
    }
}
