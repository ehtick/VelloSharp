using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VelloSharp;

[SupportedOSPlatform("browser")]
public static class WebGpuRuntime
{
    private static bool s_logCallbackRegistered;
    private static readonly object s_capabilitiesLock = new();
    private static WebGpuCapabilities? s_latestCapabilities;

    public static event Action<WebGpuLogLevel, string>? LogMessage;
    public static event EventHandler<WebGpuCapabilitiesChangedEventArgs>? DeviceCapabilitiesChanged;

    public static void EnsureInitialized()
    {
        var status = WebGpuRuntimeTestHooks.InitializeOverride?.Invoke()
                     ?? WebGpuNativeMethods.Initialize();
        if (status != WebGpuNativeMethods.VelloWebGpuStatus.Success &&
            status != WebGpuNativeMethods.VelloWebGpuStatus.AlreadyInitialized)
        {
            Throw(status);
        }

        EnsureLogCallbackRegistered();
    }

    public static void Shutdown()
    {
        WebGpuNativeMethods.Shutdown();
    }

    public static async Task<uint?> RequestAdapterAsync(
        WebGpuRequestAdapterOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var nativeOptions = options?.ToNative() ?? WebGpuRequestAdapterOptions.Default.ToNative();

        WebGpuNativeMethods.VelloWebGpuStatus status;
        uint futureId;
        if (WebGpuRuntimeTestHooks.RequestAdapterAsyncOverride is not null)
        {
            var overrideResult = WebGpuRuntimeTestHooks.RequestAdapterAsyncOverride(nativeOptions);
            status = overrideResult.Status;
            futureId = overrideResult.FutureId;
        }
        else
        {
            status = WebGpuNativeMethods.RequestAdapterAsync(ref nativeOptions, out futureId);
        }
        Throw(status);

        var result = await WaitForFutureAsync(futureId, cancellationToken).ConfigureAwait(false);
        return result.AdapterHandle != 0 ? result.AdapterHandle : null;
    }

    public static async Task<WebGpuDeviceHandles?> RequestDeviceAsync(
        uint adapterHandle,
        WebGpuRequestDeviceOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var nativeOptions = options?.ToNative() ?? WebGpuRequestDeviceOptions.Default.ToNative();

        WebGpuNativeMethods.VelloWebGpuStatus status;
        uint futureId;
        if (WebGpuRuntimeTestHooks.RequestDeviceAsyncOverride is not null)
        {
            var overrideResult = WebGpuRuntimeTestHooks.RequestDeviceAsyncOverride(adapterHandle, nativeOptions);
            status = overrideResult.Status;
            futureId = overrideResult.FutureId;
        }
        else
        {
            status = WebGpuNativeMethods.RequestDeviceAsync(adapterHandle, ref nativeOptions, out futureId);
        }
        Throw(status);

        var result = await WaitForFutureAsync(futureId, cancellationToken).ConfigureAwait(false);
        if (result.DeviceHandle == 0)
        {
            return null;
        }

        return new WebGpuDeviceHandles(result.DeviceHandle, result.QueueHandle);
    }

    public static void ConfigureSurface(
        uint surfaceHandle,
        uint adapterHandle,
        uint deviceHandle,
        WebGpuSurfaceConfiguration configuration)
    {
        EnsureInitialized();

        var nativeConfig = configuration.ToNative();
        var status = WebGpuNativeMethods.SurfaceConfigure(surfaceHandle, adapterHandle, deviceHandle, ref nativeConfig);
        Throw(status);

        try
        {
            CaptureCapabilities(deviceHandle, surfaceHandle);
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(WebGpuLogLevel.Warn, $"Failed to capture WebGPU capabilities: {ex.Message}");
        }
    }

    public static uint AcquireSurfaceTexture(uint surfaceHandle)
    {
        EnsureInitialized();

        var status = WebGpuNativeMethods.SurfaceAcquireNextTexture(surfaceHandle, out var textureHandle);
        Throw(status);
        return textureHandle;
    }

    public static uint CreateSurfaceTextureView(uint textureHandle)
    {
        EnsureInitialized();

        var status = WebGpuNativeMethods.SurfaceTextureCreateView(textureHandle, out var textureViewHandle);
        Throw(status);
        return textureViewHandle;
    }

    public static void PresentSurfaceTexture(uint surfaceHandle, uint textureHandle)
    {
        EnsureInitialized();

        var status = WebGpuNativeMethods.SurfacePresent(surfaceHandle, textureHandle);
        Throw(status);
    }

    public static void DestroySurfaceTexture(uint textureHandle)
    {
        EnsureInitialized();

        var status = WebGpuNativeMethods.SurfaceTextureDestroy(textureHandle);
        Throw(status);
    }

    public static void DestroyTextureView(uint textureViewHandle)
    {
        EnsureInitialized();

        var status = WebGpuNativeMethods.TextureViewDestroy(textureViewHandle);
        Throw(status);
    }

    public static void DestroyDevice(uint deviceHandle)
    {
        EnsureInitialized();

        var status = WebGpuNativeMethods.DeviceDestroy(deviceHandle);
        Throw(status);
    }

    public static void DestroyQueue(uint queueHandle)
    {
        EnsureInitialized();

        var status = WebGpuNativeMethods.QueueDestroy(queueHandle);
        Throw(status);
    }

    public static uint CreateRenderer(uint deviceHandle, uint queueHandle, RendererOptions options)
    {
        EnsureInitialized();

        var nativeOptions = options.ToNative();
        var status = WebGpuNativeMethods.RendererCreate(deviceHandle, queueHandle, ref nativeOptions, out var rendererHandle);
        Throw(status);
        return rendererHandle;
    }

    public static void DestroyRenderer(uint rendererHandle)
    {
        EnsureInitialized();

        var status = WebGpuNativeMethods.RendererDestroy(rendererHandle);
        Throw(status);
    }

    public static void RenderSurface(
        uint rendererHandle,
        IntPtr sceneHandle,
        uint textureViewHandle,
        RenderParams renderParams,
        WebGpuTextureFormat surfaceFormat)
    {
        EnsureInitialized();

        if (sceneHandle == IntPtr.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(sceneHandle), "Scene handle must be a non-zero pointer.");
        }

        var nativeParams = CreateNativeRenderParams(renderParams);
        var status = WebGpuNativeMethods.RendererRenderSurface(
            rendererHandle,
            sceneHandle,
            textureViewHandle,
            ref nativeParams,
            (WebGpuNativeMethods.VelloWebGpuTextureFormat)surfaceFormat);

        Throw(status);
    }

    public static WebGpuDeviceLimits GetDeviceLimits(uint deviceHandle)
    {
        EnsureInitialized();

        var status = WebGpuNativeMethods.DeviceGetLimits(deviceHandle, out var nativeLimits);
        Throw(status);
        return new WebGpuDeviceLimits(nativeLimits);
    }

    public static WebGpuTextureFormat GetSurfaceTextureFormat(uint surfaceHandle)
    {
        EnsureInitialized();

        var status = WebGpuNativeMethods.SurfaceGetCurrentTextureFormat(surfaceHandle, out var nativeFormat);
        Throw(status);
        return (WebGpuTextureFormat)nativeFormat;
    }

    public static bool TryGetLatestCapabilities(out WebGpuCapabilities? capabilities)
    {
        lock (s_capabilitiesLock)
        {
            if (s_latestCapabilities is not null)
            {
                capabilities = s_latestCapabilities;
                return true;
            }
        }

        capabilities = null;
        return false;
    }

    public static unsafe uint CreateSurface(WebGpuSurfaceDescriptor descriptor)
    {
        EnsureInitialized();

        return descriptor.BindingKind switch
        {
            WebGpuSurfaceBindingKind.CssSelector => CreateSurfaceFromBinding(
                descriptor.Binding,
                WebGpuNativeMethods.SurfaceFromCanvasSelector),
            WebGpuSurfaceBindingKind.CanvasId => CreateSurfaceFromBinding(
                descriptor.Binding,
                WebGpuNativeMethods.SurfaceFromCanvasId),
            _ => throw new ArgumentOutOfRangeException(nameof(descriptor.BindingKind), descriptor.BindingKind, "Unsupported WebGPU surface binding type."),
        };
    }

    public static uint CreateSurfaceFromSelector(string selector) =>
        CreateSurface(WebGpuSurfaceDescriptor.FromCssSelector(selector));

    public static uint CreateSurfaceFromCanvasId(string canvasId) =>
        CreateSurface(WebGpuSurfaceDescriptor.FromCanvasId(canvasId));

    public static void ResizeSurfaceCanvas(
        uint surfaceHandle,
        float logicalWidth,
        float logicalHeight,
        float devicePixelRatio)
    {
        EnsureInitialized();

        if (float.IsNaN(logicalWidth) || float.IsInfinity(logicalWidth) || logicalWidth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalWidth), "Logical width must be finite and non-negative.");
        }

        if (float.IsNaN(logicalHeight) || float.IsInfinity(logicalHeight) || logicalHeight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalHeight), "Logical height must be finite and non-negative.");
        }

        if (float.IsNaN(devicePixelRatio) || float.IsInfinity(devicePixelRatio) || devicePixelRatio <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(devicePixelRatio), "Device pixel ratio must be finite and greater than zero.");
        }

        var status = WebGpuNativeMethods.SurfaceResizeCanvas(
            surfaceHandle,
            logicalWidth,
            logicalHeight,
            devicePixelRatio);
        Throw(status);
    }

    public static void DestroySurface(uint surfaceHandle)
    {
        EnsureInitialized();

        var status = WebGpuNativeMethods.SurfaceDestroy(surfaceHandle);
        Throw(status);
        ClearCapabilitiesForSurface(surfaceHandle);
    }

    private static VelloRenderParams CreateNativeRenderParams(RenderParams renderParams)
    {
        return new VelloRenderParams
        {
            Width = renderParams.Width,
            Height = renderParams.Height,
            BaseColor = renderParams.BaseColor.ToNative(),
            Antialiasing = (VelloAaMode)renderParams.Antialiasing,
            Format = (VelloRenderFormat)renderParams.Format,
        };
    }

    private static unsafe uint CreateSurfaceFromBinding(
        string binding,
        CreateSurfaceDelegate invoker)
    {
        if (string.IsNullOrWhiteSpace(binding))
        {
            throw new ArgumentException("Binding value cannot be null or empty.", nameof(binding));
        }

        var byteCount = Encoding.UTF8.GetByteCount(binding);
        var buffer = ArrayPool<byte>.Shared.Rent(byteCount + 1);
        try
        {
            var written = Encoding.UTF8.GetBytes(binding, buffer);
            buffer[written] = 0;

            fixed (byte* bindingPtr = buffer)
            {
                var status = invoker(bindingPtr, out var surfaceHandle);
                Throw(status);
                return surfaceHandle;
            }
        }
        finally
        {
            Array.Clear(buffer, 0, byteCount + 1);
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void CaptureCapabilities(uint deviceHandle, uint surfaceHandle)
    {
        var limits = GetDeviceLimits(deviceHandle);
        var format = GetSurfaceTextureFormat(surfaceHandle);
        var capabilities = new WebGpuCapabilities(deviceHandle, surfaceHandle, limits, format);

        lock (s_capabilitiesLock)
        {
            s_latestCapabilities = capabilities;
        }

        DeviceCapabilitiesChanged?.Invoke(null, new WebGpuCapabilitiesChangedEventArgs(capabilities));
    }

    private static void ClearCapabilitiesForSurface(uint surfaceHandle)
    {
        lock (s_capabilitiesLock)
        {
            if (s_latestCapabilities?.SurfaceHandle == surfaceHandle)
            {
                s_latestCapabilities = null;
            }
        }
    }

    private static async Task<WebGpuNativeMethods.VelloWebGpuFuturePollResultNative> WaitForFutureAsync(
        uint futureId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            WebGpuNativeMethods.VelloWebGpuStatus status;
            WebGpuNativeMethods.VelloWebGpuFuturePollResultNative result;
            if (WebGpuRuntimeTestHooks.FuturePollOverride is not null)
            {
                var tuple = WebGpuRuntimeTestHooks.FuturePollOverride(futureId);
                status = tuple.Status;
                result = tuple.Result;
            }
            else
            {
                status = WebGpuNativeMethods.FuturePoll(futureId, out result);
            }
            if (status != WebGpuNativeMethods.VelloWebGpuStatus.Success)
            {
                Throw(status);
            }

            if (result.State == WebGpuNativeMethods.VelloWebGpuFutureState.Pending)
            {
                await Task.Yield();
                continue;
            }

            if (result.State == WebGpuNativeMethods.VelloWebGpuFutureState.Failed)
            {
                Throw(WebGpuNativeMethods.VelloWebGpuStatus.Failed);
            }

            return result;
        }
    }

    private static void EnsureLogCallbackRegistered()
    {
        if (Volatile.Read(ref s_logCallbackRegistered))
        {
            return;
        }

        if (WebGpuRuntimeTestHooks.SuppressLogCallbackRegistration)
        {
            Volatile.Write(ref s_logCallbackRegistered, true);
            return;
        }

        unsafe
        {
            WebGpuNativeMethods.SetLogCallback(&OnNativeLogMessage, IntPtr.Zero);
        }

        Volatile.Write(ref s_logCallbackRegistered, true);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OnNativeLogMessage(
        WebGpuNativeMethods.VelloWebGpuLogLevel level,
        IntPtr messagePtr,
        IntPtr _)
    {
        try
        {
            var message = Marshal.PtrToStringUTF8(messagePtr) ?? string.Empty;
            LogMessage?.Invoke((WebGpuLogLevel)level, message);
        }
        catch
        {
            // Swallow exceptions to avoid propagating into unmanaged code.
        }
    }

    private static void Throw(WebGpuNativeMethods.VelloWebGpuStatus status)
    {
        switch (status)
        {
            case WebGpuNativeMethods.VelloWebGpuStatus.Success:
            case WebGpuNativeMethods.VelloWebGpuStatus.AlreadyInitialized:
                return;
            case WebGpuNativeMethods.VelloWebGpuStatus.Unsupported:
                throw new NotSupportedException("Browser WebGPU interop is not supported on this platform.");
            default:
                var message = WebGpuRuntimeTestHooks.LastErrorMessageOverride?.Invoke();
                if (string.IsNullOrEmpty(message))
                {
                    message = WebGpuNativeMethods.GetLastErrorMessage()
                        ?? $"WebGPU interop call failed with status {status}.";
                }

                throw new WebGpuInteropException(message);

        }
    }

    public readonly struct WebGpuDeviceHandles(uint deviceHandle, uint queueHandle)
    {
        public uint DeviceHandle { get; } = deviceHandle;
        public uint QueueHandle { get; } = queueHandle;
    }

    public readonly struct WebGpuDeviceLimits
    {
        private readonly WebGpuNativeMethods.VelloWebGpuDeviceLimitsNative _native;

        internal WebGpuDeviceLimits(WebGpuNativeMethods.VelloWebGpuDeviceLimitsNative native)
        {
            _native = native;
        }

        public uint MaxTextureDimension1D => _native.MaxTextureDimension1D;
        public uint MaxTextureDimension2D => _native.MaxTextureDimension2D;
        public uint MaxTextureDimension3D => _native.MaxTextureDimension3D;
        public uint MaxTextureArrayLayers => _native.MaxTextureArrayLayers;
        public uint MaxBindGroups => _native.MaxBindGroups;
        public uint MaxBindingsPerBindGroup => _native.MaxBindingsPerBindGroup;
        public uint MaxDynamicUniformBuffersPerPipelineLayout => _native.MaxDynamicUniformBuffersPerPipelineLayout;
        public uint MaxDynamicStorageBuffersPerPipelineLayout => _native.MaxDynamicStorageBuffersPerPipelineLayout;
        public uint MaxSampledTexturesPerShaderStage => _native.MaxSampledTexturesPerShaderStage;
        public uint MaxSamplersPerShaderStage => _native.MaxSamplersPerShaderStage;
        public uint MaxStorageBuffersPerShaderStage => _native.MaxStorageBuffersPerShaderStage;
        public uint MaxStorageTexturesPerShaderStage => _native.MaxStorageTexturesPerShaderStage;
        public uint MaxUniformBuffersPerShaderStage => _native.MaxUniformBuffersPerShaderStage;
        public ulong MaxUniformBufferBindingSize => _native.MaxUniformBufferBindingSize;
        public ulong MaxStorageBufferBindingSize => _native.MaxStorageBufferBindingSize;
        public ulong MaxBufferSize => _native.MaxBufferSize;
        public uint MaxVertexBuffers => _native.MaxVertexBuffers;
        public uint MaxVertexAttributes => _native.MaxVertexAttributes;
        public uint MaxVertexBufferArrayStride => _native.MaxVertexBufferArrayStride;
        public uint MaxInterStageShaderComponents => _native.MaxInterStageShaderComponents;
        public uint MaxColorAttachments => _native.MaxColorAttachments;
        public uint MaxColorAttachmentBytesPerSample => _native.MaxColorAttachmentBytesPerSample;
        public uint MaxComputeWorkgroupStorageSize => _native.MaxComputeWorkgroupStorageSize;
        public uint MaxComputeInvocationsPerWorkgroup => _native.MaxComputeInvocationsPerWorkgroup;
        public uint MaxComputeWorkgroupSizeX => _native.MaxComputeWorkgroupSizeX;
        public uint MaxComputeWorkgroupSizeY => _native.MaxComputeWorkgroupSizeY;
        public uint MaxComputeWorkgroupSizeZ => _native.MaxComputeWorkgroupSizeZ;
        public uint MaxComputeWorkgroupsPerDimension => _native.MaxComputeWorkgroupsPerDimension;
        public uint MaxPushConstantSize => _native.MaxPushConstantSize;
        public uint MaxNonSamplerBindings => _native.MaxNonSamplerBindings;
    }

    public sealed record class WebGpuCapabilities(
        uint DeviceHandle,
        uint SurfaceHandle,
        WebGpuDeviceLimits DeviceLimits,
        WebGpuTextureFormat SurfaceTextureFormat);

    public sealed class WebGpuCapabilitiesChangedEventArgs : EventArgs
    {
        public WebGpuCapabilitiesChangedEventArgs(WebGpuCapabilities capabilities)
        {
            Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        }

        public WebGpuCapabilities Capabilities { get; }
    }

    public readonly struct WebGpuRequestAdapterOptions(
        WebGpuPowerPreference powerPreference,
        bool forceFallbackAdapter)
    {
        public static WebGpuRequestAdapterOptions Default => new(WebGpuPowerPreference.Auto, forceFallbackAdapter: false);

        public WebGpuPowerPreference PowerPreference { get; } = powerPreference;
        public bool ForceFallbackAdapter { get; } = forceFallbackAdapter;

        internal WebGpuNativeMethods.VelloWebGpuRequestAdapterOptionsNative ToNative()
        {
            return new WebGpuNativeMethods.VelloWebGpuRequestAdapterOptionsNative
            {
                PowerPreference = (WebGpuNativeMethods.VelloWebGpuPowerPreference)PowerPreference,
                ForceFallbackAdapter = ForceFallbackAdapter ? (byte)1 : (byte)0,
            };
        }
    }

    public readonly struct WebGpuRequestDeviceOptions(
        ulong requiredFeaturesMask,
        bool requireDownlevelDefaults,
        bool requireDefaultLimits)
    {
        public static WebGpuRequestDeviceOptions Default => new(
            requiredFeaturesMask: 0,
            requireDownlevelDefaults: true,
            requireDefaultLimits: false);

        public ulong RequiredFeaturesMask { get; } = requiredFeaturesMask;
        public bool RequireDownlevelDefaults { get; } = requireDownlevelDefaults;
        public bool RequireDefaultLimits { get; } = requireDefaultLimits;

        internal WebGpuNativeMethods.VelloWebGpuRequestDeviceOptionsNative ToNative()
        {
            return new WebGpuNativeMethods.VelloWebGpuRequestDeviceOptionsNative
            {
                RequiredFeaturesMask = RequiredFeaturesMask,
                RequireDownlevelDefaults = RequireDownlevelDefaults ? (byte)1 : (byte)0,
                RequireDefaultLimits = RequireDefaultLimits ? (byte)1 : (byte)0,
                Label = IntPtr.Zero,
            };
        }
    }

    public readonly struct WebGpuSurfaceConfiguration(uint width, uint height, WebGpuPresentMode presentMode)
    {
        public uint Width { get; } = width;
        public uint Height { get; } = height;
        public WebGpuPresentMode PresentMode { get; } = presentMode;

        internal WebGpuNativeMethods.VelloWebGpuSurfaceConfigurationNative ToNative()
        {
            return new WebGpuNativeMethods.VelloWebGpuSurfaceConfigurationNative
            {
                Width = Width,
                Height = Height,
                PresentMode = (WebGpuNativeMethods.VelloWebGpuPresentMode)PresentMode,
            };
        }
    }

    public readonly struct WebGpuSurfaceDescriptor
    {
        public WebGpuSurfaceDescriptor(string binding, WebGpuSurfaceBindingKind bindingKind)
        {
            if (string.IsNullOrWhiteSpace(binding))
            {
                throw new ArgumentException("Surface binding cannot be null or empty.", nameof(binding));
            }

            var trimmed = binding.Trim();
            if (trimmed.Length == 0)
            {
                throw new ArgumentException("Surface binding cannot be null or empty.", nameof(binding));
            }

            Binding = trimmed;
            BindingKind = bindingKind;
        }

        public string Binding { get; }
        public WebGpuSurfaceBindingKind BindingKind { get; }

        public static WebGpuSurfaceDescriptor FromCssSelector(string selector) =>
            new(selector, WebGpuSurfaceBindingKind.CssSelector);

        public static WebGpuSurfaceDescriptor FromCanvasId(string canvasId) =>
            new(canvasId, WebGpuSurfaceBindingKind.CanvasId);
    }

    public enum WebGpuSurfaceBindingKind
    {
        CssSelector = 0,
        CanvasId = 1,
    }

    public enum WebGpuPowerPreference
    {
        Auto = 0,
        LowPower = 1,
        HighPerformance = 2,
    }

    public enum WebGpuPresentMode
    {
        Auto = 0,
        Fifo = 1,
        Immediate = 2,
    }

    public enum WebGpuTextureFormat
    {
        Undefined = 0,
        Rgba8Unorm = 1,
        Rgba8UnormSrgb = 2,
        Bgra8Unorm = 3,
        Bgra8UnormSrgb = 4,
    }

    public enum WebGpuLogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4,
    }

    private unsafe delegate WebGpuNativeMethods.VelloWebGpuStatus CreateSurfaceDelegate(
        byte* bindingUtf8,
        out uint surfaceHandle);
}

public sealed class WebGpuInteropException : InvalidOperationException
{
    public WebGpuInteropException(string message)
        : base(message)
    {
    }
}

