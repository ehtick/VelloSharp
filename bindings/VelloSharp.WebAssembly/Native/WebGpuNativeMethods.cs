using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VelloSharp;

internal static partial class WebGpuNativeMethods
{
    internal const string LibraryName = "vello_webgpu_ffi";

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloWebGpuRequestAdapterOptionsNative
    {
        public VelloWebGpuPowerPreference PowerPreference;
        public byte ForceFallbackAdapter;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloWebGpuRequestDeviceOptionsNative
    {
        public ulong RequiredFeaturesMask;
        public byte RequireDownlevelDefaults;
        public byte RequireDefaultLimits;
        public IntPtr Label;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloWebGpuFuturePollResultNative
    {
        public VelloWebGpuFutureState State;
        public VelloWebGpuFutureKind Kind;
        public uint AdapterHandle;
        public uint DeviceHandle;
        public uint QueueHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloWebGpuSurfaceConfigurationNative
    {
        public uint Width;
        public uint Height;
        public VelloWebGpuPresentMode PresentMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VelloWebGpuDeviceLimitsNative
    {
        public uint MaxTextureDimension1D;
        public uint MaxTextureDimension2D;
        public uint MaxTextureDimension3D;
        public uint MaxTextureArrayLayers;
        public uint MaxBindGroups;
        public uint MaxBindingsPerBindGroup;
        public uint MaxDynamicUniformBuffersPerPipelineLayout;
        public uint MaxDynamicStorageBuffersPerPipelineLayout;
        public uint MaxSampledTexturesPerShaderStage;
        public uint MaxSamplersPerShaderStage;
        public uint MaxStorageBuffersPerShaderStage;
        public uint MaxStorageTexturesPerShaderStage;
        public uint MaxUniformBuffersPerShaderStage;
        public ulong MaxUniformBufferBindingSize;
        public ulong MaxStorageBufferBindingSize;
        public ulong MaxBufferSize;
        public uint MaxVertexBuffers;
        public uint MaxVertexAttributes;
        public uint MaxVertexBufferArrayStride;
        public uint MaxInterStageShaderComponents;
        public uint MaxColorAttachments;
        public uint MaxColorAttachmentBytesPerSample;
        public uint MaxComputeWorkgroupStorageSize;
        public uint MaxComputeInvocationsPerWorkgroup;
        public uint MaxComputeWorkgroupSizeX;
        public uint MaxComputeWorkgroupSizeY;
        public uint MaxComputeWorkgroupSizeZ;
        public uint MaxComputeWorkgroupsPerDimension;
        public uint MaxPushConstantSize;
        public uint MaxNonSamplerBindings;
    }

    internal enum VelloWebGpuStatus
    {
        Success = 0,
        NullPointer = 1,
        Unsupported = 2,
        Panic = 3,
        AlreadyInitialized = 4,
        NotInitialized = 5,
        InvalidArgument = 6,
        Failed = 7,
        InvalidHandle = 8,
    }

    internal enum VelloWebGpuLogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4,
    }

    internal enum VelloWebGpuPowerPreference
    {
        Auto = 0,
        LowPower = 1,
        HighPerformance = 2,
    }

    internal enum VelloWebGpuFutureState
    {
        Pending = 0,
        Ready = 1,
        Failed = 2,
    }

    internal enum VelloWebGpuFutureKind
    {
        Adapter = 0,
        Device = 1,
    }

    internal enum VelloWebGpuPresentMode
    {
        Auto = 0,
        Fifo = 1,
        Immediate = 2,
    }

    internal enum VelloWebGpuTextureFormat
    {
        Undefined = 0,
        Rgba8Unorm = 1,
        Rgba8UnormSrgb = 2,
        Bgra8Unorm = 3,
        Bgra8UnormSrgb = 4,
    }

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_initialize")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloWebGpuStatus Initialize();
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_shutdown")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void Shutdown();
    #endif

    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_last_error_message")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial IntPtr LastErrorMessage();

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_set_log_callback")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial void SetLogCallback(
        delegate* unmanaged[Cdecl]<VelloWebGpuLogLevel, IntPtr, IntPtr, void> callback,
        IntPtr userData);
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_request_adapter_async")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloWebGpuStatus RequestAdapterAsync(
        ref VelloWebGpuRequestAdapterOptionsNative options,
        out uint futureId);
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_request_device_async")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloWebGpuStatus RequestDeviceAsync(
        uint adapterHandle,
        ref VelloWebGpuRequestDeviceOptionsNative options,
        out uint futureId);
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_future_poll")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloWebGpuStatus FuturePoll(
        uint futureId,
        out VelloWebGpuFuturePollResultNative result);
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_surface_configure")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloWebGpuStatus SurfaceConfigure(
        uint surfaceHandle,
        uint adapterHandle,
        uint deviceHandle,
        ref VelloWebGpuSurfaceConfigurationNative configuration);
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_surface_acquire_next_texture")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloWebGpuStatus SurfaceAcquireNextTexture(
        uint surfaceHandle,
        out uint textureHandle);
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_surface_texture_create_view")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloWebGpuStatus SurfaceTextureCreateView(
        uint textureHandle,
        out uint textureViewHandle);
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_surface_present")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloWebGpuStatus SurfacePresent(uint surfaceHandle, uint textureHandle);
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_surface_texture_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloWebGpuStatus SurfaceTextureDestroy(uint textureHandle);
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_texture_view_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloWebGpuStatus TextureViewDestroy(uint textureViewHandle);
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_device_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloWebGpuStatus DeviceDestroy(uint deviceHandle);
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_device_get_limits")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloWebGpuStatus DeviceGetLimits(
        uint deviceHandle,
        out VelloWebGpuDeviceLimitsNative limits);
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_queue_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloWebGpuStatus QueueDestroy(uint queueHandle);
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_renderer_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloWebGpuStatus RendererCreate(
        uint deviceHandle,
        uint queueHandle,
        ref VelloRendererOptions options,
        out uint rendererHandle);
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_renderer_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloWebGpuStatus RendererDestroy(uint rendererHandle);
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_renderer_render_surface")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloWebGpuStatus RendererRenderSurface(
        uint rendererHandle,
        IntPtr sceneHandle,
        uint textureViewHandle,
        ref VelloRenderParams parameters,
        VelloWebGpuTextureFormat surfaceFormat);
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_surface_from_canvas_selector")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloWebGpuStatus SurfaceFromCanvasSelector(
        byte* selectorUtf8,
        out uint surfaceHandle);
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_surface_from_canvas_id")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial VelloWebGpuStatus SurfaceFromCanvasId(
        byte* canvasIdUtf8,
        out uint surfaceHandle);
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_surface_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloWebGpuStatus SurfaceDestroy(uint surfaceHandle);
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_surface_get_current_texture_format")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloWebGpuStatus SurfaceGetCurrentTextureFormat(
        uint surfaceHandle,
        out VelloWebGpuTextureFormat textureFormat);
    #endif

    #if !BROWSER
    [LibraryImport(LibraryName, EntryPoint = "vello_webgpu_surface_resize_canvas")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial VelloWebGpuStatus SurfaceResizeCanvas(
        uint surfaceHandle,
        float logicalWidth,
        float logicalHeight,
        float devicePixelRatio);
    #endif

    internal static string? GetLastErrorMessage()
    {
        var ptr = LastErrorMessage();
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
    }
}
