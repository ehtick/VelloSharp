using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

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

public enum WgpuBackendType
{
    Noop = 0,
    Vulkan = 1,
    Metal = 2,
    Dx12 = 3,
    Gl = 4,
    BrowserWebGpu = 5,
}

public enum WgpuDeviceType
{
    Other = 0,
    IntegratedGpu = 1,
    DiscreteGpu = 2,
    VirtualGpu = 3,
    Cpu = 4,
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
    PolygonModeLine = 1ul << 19,
    ClearTexture = 1ul << 23,
    PipelineCache = 1ul << 41,
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
    R8Uint,
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

public enum WgpuTextureDimension
{
    D1 = 0,
    D2 = 1,
    D3 = 2,
}

[Flags]
public enum WgpuBufferUsage : uint
{
    None = 0,
    MapRead = 1u << 0,
    MapWrite = 1u << 1,
    CopySrc = 1u << 2,
    CopyDst = 1u << 3,
    Index = 1u << 4,
    Vertex = 1u << 5,
    Uniform = 1u << 6,
    Storage = 1u << 7,
    Indirect = 1u << 8,
    QueryResolve = 1u << 9,
}

public enum WgpuAddressMode
{
    ClampToEdge = 0,
    Repeat = 1,
    MirrorRepeat = 2,
    ClampToBorder = 3,
}

public enum WgpuFilterMode
{
    Nearest = 0,
    Linear = 1,
}

public enum WgpuCompareFunction
{
    Undefined = 0,
    Never = 1,
    Less = 2,
    LessEqual = 3,
    Equal = 4,
    Greater = 5,
    NotEqual = 6,
    GreaterEqual = 7,
    Always = 8,
}

public enum WgpuStencilOperation
{
    Keep = 0,
    Zero = 1,
    Replace = 2,
    Invert = 3,
    IncrementClamp = 4,
    DecrementClamp = 5,
    IncrementWrap = 6,
    DecrementWrap = 7,
}

[Flags]
public enum WgpuColorWriteMask : uint
{
    None = 0,
    Red = 0x1,
    Green = 0x2,
    Blue = 0x4,
    Alpha = 0x8,
    All = Red | Green | Blue | Alpha,
}

public enum WgpuBlendFactor
{
    Zero = 0,
    One = 1,
    Src = 2,
    OneMinusSrc = 3,
    SrcAlpha = 4,
    OneMinusSrcAlpha = 5,
    Dst = 6,
    OneMinusDst = 7,
    DstAlpha = 8,
    OneMinusDstAlpha = 9,
    SrcAlphaSaturated = 10,
    Constant = 11,
    OneMinusConstant = 12,
}

public enum WgpuBlendOperation
{
    Add = 0,
    Subtract = 1,
    ReverseSubtract = 2,
    Min = 3,
    Max = 4,
}

public enum WgpuPrimitiveTopology
{
    PointList = 0,
    LineList = 1,
    LineStrip = 2,
    TriangleList = 3,
    TriangleStrip = 4,
}

public enum WgpuFrontFace
{
    Ccw = 0,
    Cw = 1,
}

public enum WgpuCullMode
{
    None = 0,
    Front = 1,
    Back = 2,
}

public enum WgpuPolygonMode
{
    Fill = 0,
    Line = 1,
    Point = 2,
}

public enum WgpuSamplerBindingType
{
    Filtering = 0,
    NonFiltering = 1,
    Comparison = 2,
}

public enum WgpuBufferBindingType
{
    Uniform = 0,
    Storage = 1,
    ReadOnlyStorage = 2,
}

public enum WgpuTextureSampleType
{
    Float = 0,
    UnfilterableFloat = 1,
    Depth = 2,
    Sint = 3,
    Uint = 4,
}

public enum WgpuStorageTextureAccess
{
    ReadOnly = 0,
    WriteOnly = 1,
    ReadWrite = 2,
}

[Flags]
public enum WgpuShaderStage : uint
{
    None = 0,
    Vertex = 1u << 0,
    Fragment = 1u << 1,
    Compute = 1u << 2,
}

public enum WgpuVertexStepMode
{
    Vertex = 0,
    Instance = 1,
}

public enum WgpuVertexFormat
{
    Float32 = 0,
    Float32x2 = 1,
    Float32x3 = 2,
    Float32x4 = 3,
    Uint32 = 4,
    Uint32x2 = 5,
    Uint32x3 = 6,
    Uint32x4 = 7,
    Sint32 = 8,
    Sint32x2 = 9,
    Sint32x3 = 10,
    Sint32x4 = 11,
    Float16x2 = 12,
    Float16x4 = 13,
}

public enum WgpuIndexFormat
{
    Undefined = 0,
    Uint16 = 1,
    Uint32 = 2,
}

public enum WgpuLoadOp
{
    Load = 0,
    Clear = 1,
}

public enum WgpuStoreOp
{
    Store = 0,
    Discard = 1,
}

public enum WgpuShaderModuleSourceKind
{
    Wgsl,
    Spirv,
}

public enum WgpuBindingLayoutType
{
    Buffer,
    Sampler,
    Texture,
    StorageTexture,
}

public enum WgpuBindGroupEntryType
{
    Buffer,
    Sampler,
    TextureView,
}

public readonly struct WgpuExtent3D
{
    public uint Width { get; init; }
    public uint Height { get; init; }
    public uint DepthOrArrayLayers { get; init; }
}

public readonly struct WgpuShaderModuleDescriptor
{
    public WgpuShaderModuleDescriptor(string wgsl, string? label = null)
    {
        Label = label;
        Wgsl = wgsl ?? throw new ArgumentNullException(nameof(wgsl));
        Spirv = ReadOnlyMemory<uint>.Empty;
        Source = WgpuShaderModuleSourceKind.Wgsl;
    }

    public WgpuShaderModuleDescriptor(ReadOnlyMemory<uint> spirv, string? label = null)
    {
        Label = label;
        Wgsl = null;
        Spirv = spirv;
        Source = WgpuShaderModuleSourceKind.Spirv;
    }

    public string? Label { get; }
    public string? Wgsl { get; }
    public ReadOnlyMemory<uint> Spirv { get; }
    public WgpuShaderModuleSourceKind Source { get; }
}

public readonly struct WgpuBufferDescriptor
{
    public string? Label { get; init; }
    public WgpuBufferUsage Usage { get; init; }
    public ulong Size { get; init; }
    public bool MappedAtCreation { get; init; }
}

public readonly struct WgpuSamplerDescriptor
{
    public string? Label { get; init; }
    public WgpuAddressMode AddressModeU { get; init; }
    public WgpuAddressMode AddressModeV { get; init; }
    public WgpuAddressMode AddressModeW { get; init; }
    public WgpuFilterMode MagFilter { get; init; }
    public WgpuFilterMode MinFilter { get; init; }
    public WgpuFilterMode MipFilter { get; init; }
    public float LodMinClamp { get; init; }
    public float LodMaxClamp { get; init; }
    public WgpuCompareFunction Compare { get; init; }
    public ushort AnisotropyClamp { get; init; }
}

public readonly struct WgpuTextureDescriptor
{
    public string? Label { get; init; }
    public WgpuExtent3D Size { get; init; }
    public uint MipLevelCount { get; init; }
    public uint SampleCount { get; init; }
    public WgpuTextureDimension Dimension { get; init; }
    public WgpuTextureFormat Format { get; init; }
    public WgpuTextureUsage Usage { get; init; }
    public IReadOnlyList<WgpuTextureFormat>? ViewFormats { get; init; }
}

public readonly struct WgpuBufferBindingLayout
{
    public WgpuBufferBindingType Type { get; init; }
    public bool HasDynamicOffset { get; init; }
    public ulong MinBindingSize { get; init; }
}

public readonly struct WgpuSamplerBindingLayout
{
    public WgpuSamplerBindingType Type { get; init; }
}

public readonly struct WgpuTextureBindingLayout
{
    public WgpuTextureSampleType SampleType { get; init; }
    public WgpuTextureViewDimension Dimension { get; init; }
    public bool Multisampled { get; init; }
}

public readonly struct WgpuStorageTextureBindingLayout
{
    public WgpuStorageTextureAccess Access { get; init; }
    public WgpuTextureFormat Format { get; init; }
    public WgpuTextureViewDimension Dimension { get; init; }
}

public readonly struct WgpuBindGroupLayoutEntry
{
    public WgpuBindGroupLayoutEntry(
        uint binding,
        WgpuShaderStage visibility,
        WgpuBufferBindingLayout layout)
    {
        Binding = binding;
        Visibility = visibility;
        Kind = WgpuBindingLayoutType.Buffer;
        Buffer = layout;
        Sampler = null;
        Texture = null;
        StorageTexture = null;
    }

    public WgpuBindGroupLayoutEntry(
        uint binding,
        WgpuShaderStage visibility,
        WgpuSamplerBindingLayout layout)
    {
        Binding = binding;
        Visibility = visibility;
        Kind = WgpuBindingLayoutType.Sampler;
        Buffer = null;
        Sampler = layout;
        Texture = null;
        StorageTexture = null;
    }

    public WgpuBindGroupLayoutEntry(
        uint binding,
        WgpuShaderStage visibility,
        WgpuTextureBindingLayout layout)
    {
        Binding = binding;
        Visibility = visibility;
        Kind = WgpuBindingLayoutType.Texture;
        Buffer = null;
        Sampler = null;
        Texture = layout;
        StorageTexture = null;
    }

    public WgpuBindGroupLayoutEntry(
        uint binding,
        WgpuShaderStage visibility,
        WgpuStorageTextureBindingLayout layout)
    {
        Binding = binding;
        Visibility = visibility;
        Kind = WgpuBindingLayoutType.StorageTexture;
        Buffer = null;
        Sampler = null;
        Texture = null;
        StorageTexture = layout;
    }

    public uint Binding { get; }
    public WgpuShaderStage Visibility { get; }
    public WgpuBindingLayoutType Kind { get; }
    public WgpuBufferBindingLayout? Buffer { get; }
    public WgpuSamplerBindingLayout? Sampler { get; }
    public WgpuTextureBindingLayout? Texture { get; }
    public WgpuStorageTextureBindingLayout? StorageTexture { get; }
}

public readonly struct WgpuBindGroupLayoutDescriptor
{
    public string? Label { get; init; }
    public IReadOnlyList<WgpuBindGroupLayoutEntry> Entries { get; init; }
}

public readonly struct WgpuBufferBinding
{
    public WgpuBuffer Buffer { get; init; }
    public ulong Offset { get; init; }
    public ulong? Size { get; init; }
}

public readonly struct WgpuBindGroupEntry
{
    public static WgpuBindGroupEntry CreateBuffer(uint binding, WgpuBufferBinding bindingInfo)
    {
        ArgumentNullException.ThrowIfNull(bindingInfo.Buffer);
        return new WgpuBindGroupEntry(binding, WgpuBindGroupEntryType.Buffer, bindingInfo, null, null);
    }

    public static WgpuBindGroupEntry CreateSampler(uint binding, WgpuSampler sampler)
    {
        ArgumentNullException.ThrowIfNull(sampler);
        return new WgpuBindGroupEntry(binding, WgpuBindGroupEntryType.Sampler, null, sampler, null);
    }

    public static WgpuBindGroupEntry CreateTextureView(uint binding, WgpuTextureView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new WgpuBindGroupEntry(binding, WgpuBindGroupEntryType.TextureView, null, null, view);
    }

    private WgpuBindGroupEntry(
        uint binding,
        WgpuBindGroupEntryType type,
        WgpuBufferBinding? buffer,
        WgpuSampler? sampler,
        WgpuTextureView? textureView)
    {
        Binding = binding;
        Type = type;
        Buffer = buffer;
        Sampler = sampler;
        TextureView = textureView;
    }

    public uint Binding { get; }
    public WgpuBindGroupEntryType Type { get; }
    public WgpuBufferBinding? Buffer { get; }
    public WgpuSampler? Sampler { get; }
    public WgpuTextureView? TextureView { get; }
}

public readonly struct WgpuBindGroupDescriptor
{
    public string? Label { get; init; }
    public WgpuBindGroupLayout Layout { get; init; }
    public IReadOnlyList<WgpuBindGroupEntry> Entries { get; init; }
}

public readonly struct WgpuPipelineLayoutDescriptor
{
    public string? Label { get; init; }
    public IReadOnlyList<WgpuBindGroupLayout> BindGroupLayouts { get; init; }
}

public readonly struct WgpuVertexAttribute
{
    public WgpuVertexFormat Format { get; init; }
    public ulong Offset { get; init; }
    public uint ShaderLocation { get; init; }
}

public readonly struct WgpuVertexBufferLayout
{
    public ulong ArrayStride { get; init; }
    public WgpuVertexStepMode StepMode { get; init; }
    public IReadOnlyList<WgpuVertexAttribute> Attributes { get; init; }
}

public readonly struct WgpuVertexState
{
    public WgpuShaderModule Module { get; init; }
    public string EntryPoint { get; init; }
    public IReadOnlyList<WgpuVertexBufferLayout>? Buffers { get; init; }
}

public readonly struct WgpuBlendComponent
{
    public WgpuBlendFactor SrcFactor { get; init; }
    public WgpuBlendFactor DstFactor { get; init; }
    public WgpuBlendOperation Operation { get; init; }
}

public readonly struct WgpuBlendState
{
    public WgpuBlendComponent Color { get; init; }
    public WgpuBlendComponent Alpha { get; init; }
}

public readonly struct WgpuColorTargetState
{
    public WgpuTextureFormat Format { get; init; }
    public WgpuBlendState? Blend { get; init; }
    public WgpuColorWriteMask WriteMask { get; init; }
}

public readonly struct WgpuFragmentState
{
    public WgpuShaderModule Module { get; init; }
    public string EntryPoint { get; init; }
    public IReadOnlyList<WgpuColorTargetState> Targets { get; init; }
}

public readonly struct WgpuPrimitiveState
{
    public WgpuPrimitiveTopology Topology { get; init; }
    public WgpuIndexFormat? StripIndexFormat { get; init; }
    public WgpuFrontFace FrontFace { get; init; }
    public WgpuCullMode CullMode { get; init; }
    public bool UnclippedDepth { get; init; }
    public WgpuPolygonMode PolygonMode { get; init; }
    public bool Conservative { get; init; }
}

public readonly struct WgpuStencilFaceState
{
    public WgpuCompareFunction Compare { get; init; }
    public WgpuStencilOperation Fail { get; init; }
    public WgpuStencilOperation DepthFail { get; init; }
    public WgpuStencilOperation Pass { get; init; }
}

public readonly struct WgpuDepthStencilState
{
    public WgpuTextureFormat Format { get; init; }
    public bool DepthWriteEnabled { get; init; }
    public WgpuCompareFunction DepthCompare { get; init; }
    public WgpuStencilFaceState StencilFront { get; init; }
    public WgpuStencilFaceState StencilBack { get; init; }
    public uint StencilReadMask { get; init; }
    public uint StencilWriteMask { get; init; }
    public int BiasConstant { get; init; }
    public float BiasSlopeScale { get; init; }
    public float BiasClamp { get; init; }
}

public readonly struct WgpuMultisampleState
{
    public uint Count { get; init; }
    public uint Mask { get; init; }
    public bool AlphaToCoverageEnabled { get; init; }
}

public readonly struct WgpuColor
{
    public double R { get; init; }
    public double G { get; init; }
    public double B { get; init; }
    public double A { get; init; }

    public static WgpuColor Transparent => new() { R = 0, G = 0, B = 0, A = 0 };
}

public readonly struct WgpuRenderPassColorAttachment
{
    public WgpuTextureView View { get; init; }
    public WgpuTextureView? ResolveTarget { get; init; }
    public WgpuLoadOp Load { get; init; }
    public WgpuStoreOp Store { get; init; }
    public WgpuColor ClearColor { get; init; }
}

public readonly struct WgpuRenderPassDepthStencilAttachment
{
    public WgpuTextureView View { get; init; }
    public WgpuLoadOp DepthLoad { get; init; }
    public WgpuStoreOp DepthStore { get; init; }
    public float DepthClearValue { get; init; }
    public WgpuLoadOp StencilLoad { get; init; }
    public WgpuStoreOp StencilStore { get; init; }
    public uint StencilClearValue { get; init; }
    public bool DepthReadOnly { get; init; }
    public bool StencilReadOnly { get; init; }
}

public readonly struct WgpuRenderPassDescriptor
{
    public string? Label { get; init; }
    public IReadOnlyList<WgpuRenderPassColorAttachment> ColorAttachments { get; init; }
    public WgpuRenderPassDepthStencilAttachment? DepthStencilAttachment { get; init; }
}

public readonly struct WgpuCommandEncoderDescriptor
{
    public string? Label { get; init; }
}

public readonly struct WgpuCommandBufferDescriptor
{
    public string? Label { get; init; }
}

public readonly struct WgpuOrigin3D
{
    public uint X { get; init; }
    public uint Y { get; init; }
    public uint Z { get; init; }
}

public readonly struct WgpuImageCopyTexture
{
    public WgpuTexture Texture { get; init; }
    public uint MipLevel { get; init; }
    public WgpuOrigin3D Origin { get; init; }
    public WgpuTextureAspect Aspect { get; init; }
}

public readonly struct WgpuTextureDataLayout
{
    public ulong Offset { get; init; }
    public uint? BytesPerRow { get; init; }
    public uint? RowsPerImage { get; init; }
}

public readonly struct WgpuRenderPipelineDescriptor
{
    public string? Label { get; init; }
    public WgpuPipelineLayout? Layout { get; init; }
    public WgpuVertexState Vertex { get; init; }
    public WgpuPrimitiveState Primitive { get; init; }
    public WgpuDepthStencilState? DepthStencil { get; init; }
    public WgpuMultisampleState Multisample { get; init; }
    public WgpuFragmentState? Fragment { get; init; }
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

public readonly struct WgpuAdapterInfo
{
    public WgpuAdapterInfo(uint vendor, uint device, WgpuBackendType backend, WgpuDeviceType deviceType)
    {
        Vendor = vendor;
        Device = device;
        Backend = backend;
        DeviceType = deviceType;
    }

    public uint Vendor { get; }
    public uint Device { get; }
    public WgpuBackendType Backend { get; }
    public WgpuDeviceType DeviceType { get; }
}

public readonly struct WgpuPipelineCacheDescriptor
{
    public WgpuPipelineCacheDescriptor(string? label, ReadOnlyMemory<byte> data, bool fallback)
    {
        Label = label;
        Data = data;
        Fallback = fallback;
    }

    public string? Label { get; }
    public ReadOnlyMemory<byte> Data { get; }
    public bool Fallback { get; }
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

    public WgpuAdapterInfo GetInfo()
    {
        ThrowIfDisposed();
        NativeHelpers.ThrowOnError(
            NativeMethods.vello_wgpu_adapter_get_info(_handle, out var info),
            "vello_wgpu_adapter_get_info");
        return new WgpuAdapterInfo(info.Vendor, info.Device, (WgpuBackendType)info.Backend, (WgpuDeviceType)info.DeviceType);
    }

    public WgpuFeature GetFeatures()
    {
        ThrowIfDisposed();
        NativeHelpers.ThrowOnError(
            NativeMethods.vello_wgpu_adapter_get_features(_handle, out var features),
            "vello_wgpu_adapter_get_features");
        return (WgpuFeature)features;
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
                var descriptorValue = descriptor.Value;
                var native = new WgpuDeviceDescriptorNative
                {
                    RequiredFeatures = (ulong)descriptorValue.RequiredFeatures,
                    Limits = (WgpuLimitsPresetNative)descriptorValue.Limits,
                    Label = IntPtr.Zero,
                };

                Span<byte> labelScratch = stackalloc byte[256];
                byte[]? rented = null;
                var buffer = stackalloc WgpuDeviceDescriptorNative[1];
                try
                {
                    buffer[0] = native;
                    var labelSpan = NativeHelpers.EncodeUtf8NullTerminated(descriptorValue.Label, labelScratch, ref rented);

                    if (!labelSpan.IsEmpty)
                    {
                        fixed (byte* labelPtr = labelSpan)
                        {
                            buffer[0].Label = (IntPtr)labelPtr;
                            device = NativeMethods.vello_wgpu_adapter_request_device(_handle, buffer);
                            buffer[0].Label = IntPtr.Zero;
                        }
                    }
                    else
                    {
                        buffer[0].Label = IntPtr.Zero;
                        device = NativeMethods.vello_wgpu_adapter_request_device(_handle, buffer);
                    }
                }
                finally
                {
                    if (rented is not null)
                    {
                        ArrayPool<byte>.Shared.Return(rented);
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

    public WgpuFeature GetFeatures()
    {
        ThrowIfDisposed();
        NativeHelpers.ThrowOnError(
            NativeMethods.vello_wgpu_device_get_features(_handle, out var features),
            "vello_wgpu_device_get_features");
        return (WgpuFeature)features;
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

    public WgpuPipelineCache CreatePipelineCache(WgpuPipelineCacheDescriptor descriptor)
    {
        ThrowIfDisposed();
        unsafe
        {
            Span<byte> labelScratch = stackalloc byte[256];
            byte[]? rented = null;
            try
            {
                var native = new WgpuPipelineCacheDescriptorNative
                {
                    Label = IntPtr.Zero,
                    Data = IntPtr.Zero,
                    DataLength = (nuint)descriptor.Data.Length,
                    Fallback = descriptor.Fallback,
                };

                var dataSpan = descriptor.Data.Span;
                var labelSpan = NativeHelpers.EncodeUtf8NullTerminated(descriptor.Label, labelScratch, ref rented);

                fixed (byte* dataPtr = dataSpan)
                {
                    native.Data = (IntPtr)dataPtr;
                    if (!labelSpan.IsEmpty)
                    {
                        fixed (byte* labelPtr = labelSpan)
                        {
                            native.Label = (IntPtr)labelPtr;
                            var handle = NativeMethods.vello_wgpu_device_create_pipeline_cache(_handle, &native);
                            native.Label = IntPtr.Zero;
                            if (handle == IntPtr.Zero)
                            {
                                throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create pipeline cache.");
                            }

                            return new WgpuPipelineCache(handle);
                        }
                    }
                    else
                    {
                        var handle = NativeMethods.vello_wgpu_device_create_pipeline_cache(_handle, &native);
                        if (handle == IntPtr.Zero)
                        {
                            throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create pipeline cache.");
                        }

                        return new WgpuPipelineCache(handle);
                    }
                }
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }
    }

    public WgpuShaderModule CreateShaderModule(WgpuShaderModuleDescriptor descriptor)
    {
        ThrowIfDisposed();
        unsafe
        {
            scoped Span<byte> labelScratch = stackalloc byte[256];
            scoped Span<byte> sourceScratch = stackalloc byte[1024];
            byte[]? rentedLabel = null;
            byte[]? rentedSource = null;
            try
            {
                var native = new WgpuShaderModuleDescriptorNative
                {
                    SourceKind = descriptor.Source switch
                    {
                        WgpuShaderModuleSourceKind.Spirv => WgpuShaderSourceKindNative.Spirv,
                        _ => WgpuShaderSourceKindNative.Wgsl,
                    },
                    SourceWgsl = default,
                    SourceSpirv = default,
                };

                var labelSpan = NativeHelpers.EncodeUtf8NullTerminated(descriptor.Label, labelScratch, ref rentedLabel);

                Span<byte> wgslSpan = Span<byte>.Empty;
                ReadOnlySpan<uint> spirvSpan = ReadOnlySpan<uint>.Empty;
                if (native.SourceKind == WgpuShaderSourceKindNative.Wgsl)
                {
                    if (string.IsNullOrEmpty(descriptor.Wgsl))
                    {
                        throw new ArgumentException("WGSL source must be provided for WGSL shader modules.", nameof(descriptor));
                    }

#pragma warning disable CS9080
                    var wgslText = descriptor.Wgsl!;
                    var chars = wgslText.AsSpan();
                    var byteCount = Encoding.UTF8.GetByteCount(chars);
                    if (byteCount <= sourceScratch.Length)
                    {
                        wgslSpan = sourceScratch[..byteCount];
                        Encoding.UTF8.GetBytes(chars, wgslSpan);
                    }
                    else
                    {
                        rentedSource = ArrayPool<byte>.Shared.Rent(byteCount);
                        wgslSpan = rentedSource.AsSpan(0, byteCount);
                        Encoding.UTF8.GetBytes(chars, wgslSpan);
                    }

                    if (wgslSpan.IsEmpty)
                    {
                        throw new ArgumentException("WGSL source must not be empty.", nameof(descriptor));
                    }
#pragma warning restore CS9080
                }
                else
                {
                    spirvSpan = descriptor.Spirv.Span;
                    if (spirvSpan.IsEmpty)
                    {
                        throw new ArgumentException("SPIR-V source must not be empty.", nameof(descriptor));
                    }
                }

                fixed (byte* labelPtr = labelSpan)
                fixed (byte* wgslPtr = wgslSpan)
                fixed (uint* spirvPtr = spirvSpan)
                {
                    native.Label = labelSpan.IsEmpty ? IntPtr.Zero : (IntPtr)labelPtr;
                    if (native.SourceKind == WgpuShaderSourceKindNative.Wgsl)
                    {
                        native.SourceWgsl = new VelloBytesNative
                        {
                            Data = (IntPtr)wgslPtr,
                            Length = (nuint)wgslSpan.Length,
                        };
                        native.SourceSpirv = default;
                    }
                    else
                    {
                        native.SourceWgsl = default;
                        native.SourceSpirv = new VelloU32SliceNative
                        {
                            Data = (IntPtr)spirvPtr,
                            Length = (nuint)spirvSpan.Length,
                        };
                    }

                    var handle = NativeMethods.vello_wgpu_device_create_shader_module(_handle, &native);
                    if (handle == IntPtr.Zero)
                    {
                        throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create shader module.");
                    }

                    return new WgpuShaderModule(handle);
                }
            }
            finally
            {
                if (rentedLabel is not null)
                {
                    ArrayPool<byte>.Shared.Return(rentedLabel);
                }

                if (rentedSource is not null)
                {
                    ArrayPool<byte>.Shared.Return(rentedSource);
                }
            }
        }
    }

    public WgpuBuffer CreateBuffer(WgpuBufferDescriptor descriptor)
    {
        ThrowIfDisposed();
        if (descriptor.Size == 0)
        {
            throw new ArgumentException("Buffer size must be greater than zero.", nameof(descriptor));
        }

        if (descriptor.Usage == WgpuBufferUsage.None)
        {
            throw new ArgumentException("Buffer usage must include at least one flag.", nameof(descriptor));
        }

        unsafe
        {
            Span<byte> labelScratch = stackalloc byte[256];
            byte[]? rentedLabel = null;
            try
            {
                var native = new WgpuBufferDescriptorNative
                {
                    Label = IntPtr.Zero,
                    Usage = (uint)descriptor.Usage,
                    Size = descriptor.Size,
                    MappedAtCreation = descriptor.MappedAtCreation,
                    InitialData = default,
                };

                var labelSpan = NativeHelpers.EncodeUtf8NullTerminated(descriptor.Label, labelScratch, ref rentedLabel);
                fixed (byte* labelPtr = labelSpan)
                {
                    native.Label = labelSpan.IsEmpty ? IntPtr.Zero : (IntPtr)labelPtr;
                    var handle = NativeMethods.vello_wgpu_device_create_buffer(_handle, &native);
                    if (handle == IntPtr.Zero)
                    {
                        throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create buffer.");
                    }

                    return new WgpuBuffer(handle);
                }
            }
            finally
            {
                if (rentedLabel is not null)
                {
                    ArrayPool<byte>.Shared.Return(rentedLabel);
                }
            }
        }
    }

    public WgpuSampler CreateSampler(WgpuSamplerDescriptor descriptor)
    {
        ThrowIfDisposed();
        unsafe
        {
            Span<byte> labelScratch = stackalloc byte[256];
            byte[]? rentedLabel = null;
            try
            {
                var native = new WgpuSamplerDescriptorNative
                {
                    AddressModeU = (uint)descriptor.AddressModeU,
                    AddressModeV = (uint)descriptor.AddressModeV,
                    AddressModeW = (uint)descriptor.AddressModeW,
                    MagFilter = (uint)descriptor.MagFilter,
                    MinFilter = (uint)descriptor.MinFilter,
                    MipFilter = (uint)descriptor.MipFilter,
                    LodMinClamp = descriptor.LodMinClamp,
                    LodMaxClamp = descriptor.LodMaxClamp,
                    Compare = (uint)descriptor.Compare,
                    AnisotropyClamp = descriptor.AnisotropyClamp == 0 ? (ushort)1 : descriptor.AnisotropyClamp,
                };

                var labelSpan = NativeHelpers.EncodeUtf8NullTerminated(descriptor.Label, labelScratch, ref rentedLabel);
                fixed (byte* labelPtr = labelSpan)
                {
                    native.Label = labelSpan.IsEmpty ? IntPtr.Zero : (IntPtr)labelPtr;
                    var handle = NativeMethods.vello_wgpu_device_create_sampler(_handle, &native);
                    if (handle == IntPtr.Zero)
                    {
                        throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create sampler.");
                    }

                    return new WgpuSampler(handle);
                }
            }
            finally
            {
                if (rentedLabel is not null)
                {
                    ArrayPool<byte>.Shared.Return(rentedLabel);
                }
            }
        }
    }

    public WgpuTexture CreateTexture(WgpuTextureDescriptor descriptor)
    {
        ThrowIfDisposed();
        if (descriptor.Size.Width == 0 || descriptor.Size.Height == 0)
        {
            throw new ArgumentException("Texture dimensions must be greater than zero.", nameof(descriptor));
        }

        unsafe
        {
            Span<byte> labelScratch = stackalloc byte[256];
            byte[]? rentedLabel = null;
            WgpuTextureFormatNative[]? viewFormatArray = null;
            GCHandle viewFormatsHandle = default;
            try
            {
                var native = new WgpuTextureDescriptorNative
                {
                    Label = IntPtr.Zero,
                    Size = new WgpuExtent3DNative
                    {
                        Width = descriptor.Size.Width,
                        Height = descriptor.Size.Height,
                        DepthOrArrayLayers = descriptor.Size.DepthOrArrayLayers,
                    },
                    MipLevelCount = descriptor.MipLevelCount,
                    SampleCount = descriptor.SampleCount,
                    Dimension = (uint)descriptor.Dimension,
                    Format = (WgpuTextureFormatNative)descriptor.Format,
                    Usage = (uint)descriptor.Usage,
                    ViewFormatCount = 0,
                    ViewFormats = IntPtr.Zero,
                };

                var viewFormats = descriptor.ViewFormats;
                if (viewFormats is not null && viewFormats.Count > 0)
                {
                    viewFormatArray = new WgpuTextureFormatNative[viewFormats.Count];
                    for (int i = 0; i < viewFormats.Count; i++)
                    {
                        viewFormatArray[i] = (WgpuTextureFormatNative)viewFormats[i];
                    }

                    viewFormatsHandle = GCHandle.Alloc(viewFormatArray, GCHandleType.Pinned);
                    native.ViewFormatCount = (nuint)viewFormatArray.Length;
                    native.ViewFormats = viewFormatsHandle.AddrOfPinnedObject();
                }

                var labelSpan = NativeHelpers.EncodeUtf8NullTerminated(descriptor.Label, labelScratch, ref rentedLabel);
                fixed (byte* labelPtr = labelSpan)
                {
                    native.Label = labelSpan.IsEmpty ? IntPtr.Zero : (IntPtr)labelPtr;
                    var handle = NativeMethods.vello_wgpu_device_create_texture(_handle, &native);
                    if (handle == IntPtr.Zero)
                    {
                        throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create texture.");
                    }

                    return new WgpuTexture(handle);
                }
            }
            finally
            {
                if (viewFormatsHandle.IsAllocated)
                {
                    viewFormatsHandle.Free();
                }

                if (rentedLabel is not null)
                {
                    ArrayPool<byte>.Shared.Return(rentedLabel);
                }
            }
        }
    }

    public WgpuBindGroupLayout CreateBindGroupLayout(WgpuBindGroupLayoutDescriptor descriptor)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(descriptor.Entries);
        unsafe
        {
            Span<byte> labelScratch = stackalloc byte[256];
            byte[]? rentedLabel = null;
            var entries = descriptor.Entries;
            var nativeEntries = new WgpuBindGroupLayoutEntryNative[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var native = new WgpuBindGroupLayoutEntryNative
                {
                    Binding = entry.Binding,
                    Visibility = (uint)entry.Visibility,
                };

                switch (entry.Kind)
                {
                    case WgpuBindingLayoutType.Buffer:
                        if (entry.Buffer is null)
                        {
                            throw new ArgumentException("Buffer layout data was not provided for buffer binding entry.", nameof(descriptor));
                        }

                        var buffer = entry.Buffer.Value;
                        native.Type = 0;
                        native.HasDynamicOffset = buffer.HasDynamicOffset;
                        native.MinBindingSize = buffer.MinBindingSize;
                        native.BufferType = (uint)buffer.Type;
                        break;
                    case WgpuBindingLayoutType.Sampler:
                        if (entry.Sampler is null)
                        {
                            throw new ArgumentException("Sampler layout data was not provided for sampler binding entry.", nameof(descriptor));
                        }

                        native.Type = 1;
                        native.SamplerType = (uint)entry.Sampler.Value.Type;
                        break;
                    case WgpuBindingLayoutType.Texture:
                        if (entry.Texture is null)
                        {
                            throw new ArgumentException("Texture layout data was not provided for texture binding entry.", nameof(descriptor));
                        }

                        native.Type = 2;
                        var textureLayout = entry.Texture.Value;
                        if (textureLayout.Dimension == WgpuTextureViewDimension.Default)
                        {
                            throw new ArgumentException("Texture view dimension must be specified for texture bindings.", nameof(descriptor));
                        }

                        native.TextureViewDimension = (uint)textureLayout.Dimension;
                        native.TextureSampleType = (uint)entry.Texture.Value.SampleType;
                        native.TextureMultisampled = entry.Texture.Value.Multisampled;
                        break;
                    case WgpuBindingLayoutType.StorageTexture:
                        if (entry.StorageTexture is null)
                        {
                            throw new ArgumentException("Storage texture layout data was not provided for storage texture binding entry.", nameof(descriptor));
                        }

                        native.Type = 3;
                        var storageTexture = entry.StorageTexture.Value;
                        if (storageTexture.Dimension == WgpuTextureViewDimension.Default)
                        {
                            throw new ArgumentException("Texture view dimension must be specified for storage texture bindings.", nameof(descriptor));
                        }

                        native.StorageTextureAccess = (uint)storageTexture.Access;
                        native.StorageTextureFormat = (WgpuTextureFormatNative)storageTexture.Format;
                        native.TextureViewDimension = (uint)storageTexture.Dimension;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(descriptor), "Unsupported binding layout type.");
                }

                nativeEntries[i] = native;
            }

            GCHandle entriesHandle = GCHandle.Alloc(nativeEntries, GCHandleType.Pinned);
            try
            {
                var native = new WgpuBindGroupLayoutDescriptorNative
                {
                    Label = IntPtr.Zero,
                    EntryCount = (nuint)nativeEntries.Length,
                    Entries = entriesHandle.AddrOfPinnedObject(),
                };

                var labelSpan = NativeHelpers.EncodeUtf8NullTerminated(descriptor.Label, labelScratch, ref rentedLabel);
                fixed (byte* labelPtr = labelSpan)
                {
                    native.Label = labelSpan.IsEmpty ? IntPtr.Zero : (IntPtr)labelPtr;
                    var handle = NativeMethods.vello_wgpu_device_create_bind_group_layout(_handle, &native);
                    if (handle == IntPtr.Zero)
                    {
                        throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create bind group layout.");
                    }

                    return new WgpuBindGroupLayout(handle);
                }
            }
            finally
            {
                entriesHandle.Free();
                if (rentedLabel is not null)
                {
                    ArrayPool<byte>.Shared.Return(rentedLabel);
                }
            }
        }
    }

    public WgpuBindGroup CreateBindGroup(WgpuBindGroupDescriptor descriptor)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(descriptor.Layout);
        ArgumentNullException.ThrowIfNull(descriptor.Entries);
        unsafe
        {
            Span<byte> labelScratch = stackalloc byte[256];
            byte[]? rentedLabel = null;
            var entries = descriptor.Entries;
            var nativeEntries = new WgpuBindGroupEntryNative[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var native = new WgpuBindGroupEntryNative
                {
                    Binding = entry.Binding,
                };

                switch (entry.Type)
                {
                    case WgpuBindGroupEntryType.Buffer:
                        if (entry.Buffer is null)
                        {
                            throw new ArgumentException("Buffer entry is missing buffer data.", nameof(descriptor));
                        }

                        var buffer = entry.Buffer.Value;
                        native.Buffer = buffer.Buffer.Handle;
                        native.Offset = buffer.Offset;
                        native.Size = buffer.Size ?? 0;
                        break;
                    case WgpuBindGroupEntryType.Sampler:
                        if (entry.Sampler is null)
                        {
                            throw new ArgumentException("Sampler entry is missing sampler data.", nameof(descriptor));
                        }

                        native.Sampler = entry.Sampler.Handle;
                        break;
                    case WgpuBindGroupEntryType.TextureView:
                        if (entry.TextureView is null)
                        {
                            throw new ArgumentException("Texture view entry is missing view data.", nameof(descriptor));
                        }

                        native.TextureView = entry.TextureView.Handle;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(descriptor), "Unsupported bind group entry type.");
                }

                nativeEntries[i] = native;
            }

            GCHandle entriesHandle = GCHandle.Alloc(nativeEntries, GCHandleType.Pinned);
            try
            {
                var native = new WgpuBindGroupDescriptorNative
                {
                    Label = IntPtr.Zero,
                    Layout = descriptor.Layout.Handle,
                    EntryCount = (nuint)nativeEntries.Length,
                    Entries = entriesHandle.AddrOfPinnedObject(),
                };

                var labelSpan = NativeHelpers.EncodeUtf8NullTerminated(descriptor.Label, labelScratch, ref rentedLabel);
                fixed (byte* labelPtr = labelSpan)
                {
                    native.Label = labelSpan.IsEmpty ? IntPtr.Zero : (IntPtr)labelPtr;
                    var handle = NativeMethods.vello_wgpu_device_create_bind_group(_handle, &native);
                    if (handle == IntPtr.Zero)
                    {
                        throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create bind group.");
                    }

                    return new WgpuBindGroup(handle);
                }
            }
            finally
            {
                entriesHandle.Free();
                if (rentedLabel is not null)
                {
                    ArrayPool<byte>.Shared.Return(rentedLabel);
                }
            }
        }
    }

    public WgpuPipelineLayout CreatePipelineLayout(WgpuPipelineLayoutDescriptor descriptor)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(descriptor.BindGroupLayouts);
        unsafe
        {
            Span<byte> labelScratch = stackalloc byte[256];
            byte[]? rentedLabel = null;
            var layouts = descriptor.BindGroupLayouts;
            var layoutPointers = new IntPtr[layouts.Count];
            for (int i = 0; i < layouts.Count; i++)
            {
                layoutPointers[i] = layouts[i].Handle;
            }

            GCHandle layoutsHandle = GCHandle.Alloc(layoutPointers, GCHandleType.Pinned);
            try
            {
                var native = new WgpuPipelineLayoutDescriptorNative
                {
                    Label = IntPtr.Zero,
                    BindGroupLayoutCount = (nuint)layoutPointers.Length,
                    BindGroupLayouts = layoutPointers.Length == 0 ? IntPtr.Zero : layoutsHandle.AddrOfPinnedObject(),
                };

                var labelSpan = NativeHelpers.EncodeUtf8NullTerminated(descriptor.Label, labelScratch, ref rentedLabel);
                fixed (byte* labelPtr = labelSpan)
                {
                    native.Label = labelSpan.IsEmpty ? IntPtr.Zero : (IntPtr)labelPtr;
                    var handle = NativeMethods.vello_wgpu_device_create_pipeline_layout(_handle, &native);
                    if (handle == IntPtr.Zero)
                    {
                        throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create pipeline layout.");
                    }

                    return new WgpuPipelineLayout(handle);
                }
            }
            finally
            {
                layoutsHandle.Free();
                if (rentedLabel is not null)
                {
                    ArrayPool<byte>.Shared.Return(rentedLabel);
                }
            }
        }
    }

    public WgpuRenderPipeline CreateRenderPipeline(WgpuRenderPipelineDescriptor descriptor)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(descriptor.Vertex.Module);
        if (string.IsNullOrEmpty(descriptor.Vertex.EntryPoint))
        {
            throw new ArgumentException("Vertex entry point must be provided.", nameof(descriptor));
        }

        unsafe
        {
            scoped Span<byte> labelScratch = stackalloc byte[256];
            scoped Span<byte> entryScratch = stackalloc byte[256];
            scoped Span<byte> fragmentEntryScratch = stackalloc byte[256];
            byte[]? rentedLabel = null;
            byte[]? rentedVertexEntry = null;
            byte[]? rentedFragmentEntry = null;

            var handles = new List<GCHandle>();
            try
            {
                var labelSpan = NativeHelpers.EncodeUtf8NullTerminated(descriptor.Label, labelScratch, ref rentedLabel);
                var vertexChars = descriptor.Vertex.EntryPoint.AsSpan();
                var vertexByteCount = Encoding.UTF8.GetByteCount(vertexChars);
#pragma warning disable CS9080
                Span<byte> vertexEntrySpan;
                if (vertexByteCount <= entryScratch.Length)
                {
                    vertexEntrySpan = entryScratch[..vertexByteCount];
                    Encoding.UTF8.GetBytes(vertexChars, vertexEntrySpan);
                }
                else
                {
                    rentedVertexEntry = ArrayPool<byte>.Shared.Rent(vertexByteCount);
                    vertexEntrySpan = rentedVertexEntry.AsSpan(0, vertexByteCount);
                    Encoding.UTF8.GetBytes(vertexChars, vertexEntrySpan);
                }
#pragma warning restore CS9080

                var vertexBuffers = descriptor.Vertex.Buffers;
                var vertexLayouts = vertexBuffers is null ? Array.Empty<WgpuVertexBufferLayout>() : vertexBuffers;
                var nativeVertexLayouts = new WgpuVertexBufferLayoutNative[vertexLayouts.Count];
                var attributeArrays = new WgpuVertexAttributeNative[vertexLayouts.Count][];

                for (int i = 0; i < vertexLayouts.Count; i++)
                {
                    var layout = vertexLayouts[i];
                    var attributes = layout.Attributes ?? Array.Empty<WgpuVertexAttribute>();
                    var nativeAttributes = new WgpuVertexAttributeNative[attributes.Count];
                    for (int j = 0; j < attributes.Count; j++)
                    {
                        nativeAttributes[j] = new WgpuVertexAttributeNative
                        {
                            Format = (uint)attributes[j].Format,
                            Offset = attributes[j].Offset,
                            ShaderLocation = attributes[j].ShaderLocation,
                        };
                    }

                    attributeArrays[i] = nativeAttributes;
                    var attrHandle = GCHandle.Alloc(nativeAttributes, GCHandleType.Pinned);
                    handles.Add(attrHandle);

                    nativeVertexLayouts[i] = new WgpuVertexBufferLayoutNative
                    {
                        ArrayStride = layout.ArrayStride,
                        StepMode = (uint)layout.StepMode,
                        AttributeCount = (nuint)nativeAttributes.Length,
                        Attributes = nativeAttributes.Length == 0 ? IntPtr.Zero : attrHandle.AddrOfPinnedObject(),
                    };
                }

                var vertexLayoutsHandle = GCHandle.Alloc(nativeVertexLayouts, GCHandleType.Pinned);
                handles.Add(vertexLayoutsHandle);

                var primitive = descriptor.Primitive;
                var nativePrimitive = new WgpuPrimitiveStateNative
                {
                    Topology = (uint)primitive.Topology,
                    StripIndexFormat = primitive.StripIndexFormat is null ? 0u : (uint)primitive.StripIndexFormat.Value,
                    FrontFace = (uint)primitive.FrontFace,
                    CullMode = (uint)primitive.CullMode,
                    UnclippedDepth = primitive.UnclippedDepth,
                    PolygonMode = (uint)primitive.PolygonMode,
                    Conservative = primitive.Conservative,
                };

                WgpuDepthStencilStateNative[]? depthArray = null;
                GCHandle depthHandle = default;
                if (descriptor.DepthStencil is { } depth)
                {
                    var nativeDepth = new WgpuDepthStencilStateNative
                    {
                        Format = (WgpuTextureFormatNative)depth.Format,
                        DepthWriteEnabled = depth.DepthWriteEnabled,
                        DepthCompare = (uint)depth.DepthCompare,
                        StencilFront = new WgpuStencilFaceStateNative
                        {
                            Compare = (uint)depth.StencilFront.Compare,
                            FailOp = (uint)depth.StencilFront.Fail,
                            DepthFailOp = (uint)depth.StencilFront.DepthFail,
                            PassOp = (uint)depth.StencilFront.Pass,
                        },
                        StencilBack = new WgpuStencilFaceStateNative
                        {
                            Compare = (uint)depth.StencilBack.Compare,
                            FailOp = (uint)depth.StencilBack.Fail,
                            DepthFailOp = (uint)depth.StencilBack.DepthFail,
                            PassOp = (uint)depth.StencilBack.Pass,
                        },
                        StencilReadMask = depth.StencilReadMask,
                        StencilWriteMask = depth.StencilWriteMask,
                        BiasConstant = depth.BiasConstant,
                        BiasSlopeScale = depth.BiasSlopeScale,
                        BiasClamp = depth.BiasClamp,
                    };

                    depthArray = new[] { nativeDepth };
                    depthHandle = GCHandle.Alloc(depthArray, GCHandleType.Pinned);
                    handles.Add(depthHandle);
                }

                var multisample = descriptor.Multisample;
                var nativeMultisample = new WgpuMultisampleStateNative
                {
                    Count = multisample.Count == 0 ? 1u : multisample.Count,
                    Mask = multisample.Mask,
                    AlphaToCoverageEnabled = multisample.AlphaToCoverageEnabled,
                };

                WgpuColorTargetStateNative[]? colorTargetsArray = null;
                WgpuBlendStateNative[]? blendStatesArray = null;
                GCHandle colorTargetsHandle = default;
                GCHandle blendStatesHandle = default;
                Span<byte> fragmentEntrySpan = Span<byte>.Empty;
                WgpuFragmentStateNative[]? fragmentStateArray = null;
                GCHandle fragmentStateHandle = default;
                if (descriptor.Fragment is { } fragment)
                {
                    if (string.IsNullOrEmpty(fragment.EntryPoint))
                    {
                        throw new ArgumentException("Fragment entry point must be provided when fragment state is specified.", nameof(descriptor));
                    }

                    var fragmentChars = fragment.EntryPoint.AsSpan();
                    var fragmentByteCount = Encoding.UTF8.GetByteCount(fragmentChars);
#pragma warning disable CS9080
                    if (fragmentByteCount <= fragmentEntryScratch.Length)
                    {
                        fragmentEntrySpan = fragmentEntryScratch[..fragmentByteCount];
                        Encoding.UTF8.GetBytes(fragmentChars, fragmentEntrySpan);
                    }
                    else
                    {
                        rentedFragmentEntry = ArrayPool<byte>.Shared.Rent(fragmentByteCount);
                        fragmentEntrySpan = rentedFragmentEntry.AsSpan(0, fragmentByteCount);
                        Encoding.UTF8.GetBytes(fragmentChars, fragmentEntrySpan);
                    }
#pragma warning restore CS9080

                    var targets = fragment.Targets ?? Array.Empty<WgpuColorTargetState>();
                    colorTargetsArray = new WgpuColorTargetStateNative[targets.Count];
                    var blendStates = new List<WgpuBlendStateNative>();
                    for (int i = 0; i < targets.Count; i++)
                    {
                        var target = targets[i];
                        var nativeTarget = new WgpuColorTargetStateNative
                        {
                            Format = (WgpuTextureFormatNative)target.Format,
                            WriteMask = (uint)(target.WriteMask == WgpuColorWriteMask.None ? WgpuColorWriteMask.All : target.WriteMask),
                        };

                        if (target.Blend is { } blend)
                        {
                            var nativeBlend = new WgpuBlendStateNative
                            {
                                Color = new WgpuBlendComponentNative
                                {
                                    SrcFactor = (uint)blend.Color.SrcFactor,
                                    DstFactor = (uint)blend.Color.DstFactor,
                                    Operation = (uint)blend.Color.Operation,
                                },
                                Alpha = new WgpuBlendComponentNative
                                {
                                    SrcFactor = (uint)blend.Alpha.SrcFactor,
                                    DstFactor = (uint)blend.Alpha.DstFactor,
                                    Operation = (uint)blend.Alpha.Operation,
                                },
                            };
                            blendStates.Add(nativeBlend);
                        }
                        else
                        {
                            blendStates.Add(default);
                        }

                        colorTargetsArray[i] = nativeTarget;
                    }

                    blendStatesArray = blendStates.ToArray();
                    blendStatesHandle = GCHandle.Alloc(blendStatesArray, GCHandleType.Pinned);
                    handles.Add(blendStatesHandle);

                    for (int i = 0; i < colorTargetsArray.Length; i++)
                    {
                        var blend = targets[i].Blend;
                        if (blend is not null)
                        {
                            colorTargetsArray[i].Blend = blendStatesHandle.AddrOfPinnedObject() + i * Marshal.SizeOf<WgpuBlendStateNative>();
                        }
                    }

                    colorTargetsHandle = GCHandle.Alloc(colorTargetsArray, GCHandleType.Pinned);
                    handles.Add(colorTargetsHandle);

                    var nativeFragment = new WgpuFragmentStateNative
                    {
                        Module = fragment.Module.Handle,
                        EntryPoint = new VelloBytesNative
                        {
                            Data = IntPtr.Zero,
                            Length = (nuint)fragmentEntrySpan.Length,
                        },
                        TargetCount = (nuint)colorTargetsArray.Length,
                        Targets = colorTargetsArray.Length == 0 ? IntPtr.Zero : colorTargetsHandle.AddrOfPinnedObject(),
                    };

                    fragmentStateArray = new[] { nativeFragment };
                    fragmentStateHandle = GCHandle.Alloc(fragmentStateArray, GCHandleType.Pinned);
                    handles.Add(fragmentStateHandle);
                }

                var nativeVertex = new WgpuVertexStateNative
                {
                    Module = descriptor.Vertex.Module.Handle,
                    EntryPoint = default,
                    BufferCount = (nuint)nativeVertexLayouts.Length,
                    Buffers = nativeVertexLayouts.Length == 0 ? IntPtr.Zero : vertexLayoutsHandle.AddrOfPinnedObject(),
                };

                var nativeDescriptor = new WgpuRenderPipelineDescriptorNative
                {
                    Label = IntPtr.Zero,
                    Layout = descriptor.Layout?.Handle ?? IntPtr.Zero,
                    Vertex = nativeVertex,
                    Primitive = nativePrimitive,
                    DepthStencil = depthArray is null ? IntPtr.Zero : depthHandle.AddrOfPinnedObject(),
                    Multisample = nativeMultisample,
                    Fragment = fragmentStateArray is null ? IntPtr.Zero : fragmentStateHandle.AddrOfPinnedObject(),
                };

                fixed (byte* labelPtr = labelSpan)
                fixed (byte* vertexEntryPtr = vertexEntrySpan)
                fixed (byte* fragmentEntryPtr = fragmentEntrySpan)
                {
                    nativeDescriptor.Label = labelSpan.IsEmpty ? IntPtr.Zero : (IntPtr)labelPtr;
                    nativeDescriptor.Vertex.EntryPoint = new VelloBytesNative
                    {
                        Data = (IntPtr)vertexEntryPtr,
                        Length = (nuint)vertexEntrySpan.Length,
                    };

                    if (fragmentStateArray is not null)
                    {
                        fragmentStateArray[0].EntryPoint = new VelloBytesNative
                        {
                            Data = (IntPtr)fragmentEntryPtr,
                            Length = (nuint)fragmentEntrySpan.Length,
                        };
                    }

                    var pipelineHandle = NativeMethods.vello_wgpu_device_create_render_pipeline(_handle, &nativeDescriptor);
                    if (pipelineHandle == IntPtr.Zero)
                    {
                        throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create render pipeline.");
                    }

                    return new WgpuRenderPipeline(pipelineHandle);
                }
            }
            finally
            {
                foreach (var handle in handles)
                {
                    if (handle.IsAllocated)
                    {
                        handle.Free();
                    }
                }

                if (rentedLabel is not null)
                {
                    ArrayPool<byte>.Shared.Return(rentedLabel);
                }

                if (rentedVertexEntry is not null)
                {
                    ArrayPool<byte>.Shared.Return(rentedVertexEntry);
                }

                if (rentedFragmentEntry is not null)
                {
                    ArrayPool<byte>.Shared.Return(rentedFragmentEntry);
                }
            }
        }
    }

    public WgpuCommandEncoder CreateCommandEncoder(WgpuCommandEncoderDescriptor? descriptor = null)
    {
        ThrowIfDisposed();
        unsafe
        {
            Span<byte> labelScratch = stackalloc byte[256];
            byte[]? rentedLabel = null;
            try
            {
                IntPtr handle;
                if (descriptor is { } desc && !string.IsNullOrEmpty(desc.Label))
                {
                    var labelSpan = NativeHelpers.EncodeUtf8NullTerminated(desc.Label, labelScratch, ref rentedLabel);
                    fixed (byte* labelPtr = labelSpan)
                    {
                        var native = new VelloWgpuCommandEncoderDescriptorNative
                        {
                            Label = (IntPtr)labelPtr,
                        };
                        handle = NativeMethods.vello_wgpu_device_create_command_encoder(_handle, &native);
                    }
                }
                else
                {
                    handle = NativeMethods.vello_wgpu_device_create_command_encoder(_handle, null);
                }

                if (handle == IntPtr.Zero)
                {
                    throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create command encoder.");
                }

                return new WgpuCommandEncoder(handle);
            }
            finally
            {
                if (rentedLabel is not null)
                {
                    ArrayPool<byte>.Shared.Return(rentedLabel);
                }
            }
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

public sealed class WgpuCommandBuffer : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    internal WgpuCommandBuffer(IntPtr handle)
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

    internal void MarkConsumed()
    {
        if (_disposed)
        {
            return;
        }

        _handle = IntPtr.Zero;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_command_buffer_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgpuCommandBuffer()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_command_buffer_destroy(_handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WgpuCommandBuffer));
        }
    }
}

public sealed class WgpuRenderPass : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;
    private bool _ended;

    internal WgpuRenderPass(IntPtr handle)
    {
        _handle = handle;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed || _handle == IntPtr.Zero)
        {
            throw new ObjectDisposedException(nameof(WgpuRenderPass));
        }
    }

    public void SetViewport(float x, float y, float width, float height, float minDepth = 0f, float maxDepth = 1f)
    {
        ThrowIfDisposed();
        if (width <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Viewport width must be positive.");
        }

        if (height <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Viewport height must be positive.");
        }

        if (minDepth is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(minDepth), "Minimum depth must be between 0 and 1.");
        }

        if (maxDepth is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Maximum depth must be between 0 and 1.");
        }

        if (minDepth > maxDepth)
        {
            throw new ArgumentException("Minimum depth must not exceed maximum depth.", nameof(minDepth));
        }

        NativeMethods.vello_wgpu_render_pass_set_viewport(_handle, x, y, width, height, minDepth, maxDepth);
    }

    public void SetScissorRect(uint x, uint y, uint width, uint height)
    {
        ThrowIfDisposed();
        if (width == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Scissor width must be greater than zero.");
        }

        if (height == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Scissor height must be greater than zero.");
        }

        NativeMethods.vello_wgpu_render_pass_set_scissor_rect(_handle, x, y, width, height);
    }

    public void SetPipeline(WgpuRenderPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ThrowIfDisposed();
        NativeMethods.vello_wgpu_render_pass_set_pipeline(_handle, pipeline.Handle);
    }

    public void SetBindGroup(uint index, WgpuBindGroup bindGroup, ReadOnlySpan<uint> dynamicOffsets = default)
    {
        ArgumentNullException.ThrowIfNull(bindGroup);
        ThrowIfDisposed();
        unsafe
        {
            fixed (uint* offsetsPtr = dynamicOffsets)
            {
                NativeMethods.vello_wgpu_render_pass_set_bind_group(
                    _handle,
                    index,
                    bindGroup.Handle,
                    offsetsPtr,
                    (nuint)dynamicOffsets.Length);
            }
        }
    }

    public void SetVertexBuffer(uint slot, WgpuBuffer buffer, ulong offset = 0, ulong size = 0)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ThrowIfDisposed();
        NativeMethods.vello_wgpu_render_pass_set_vertex_buffer(_handle, slot, buffer.Handle, offset, size);
    }

    public void SetIndexBuffer(WgpuBuffer buffer, WgpuIndexFormat format, ulong offset = 0, ulong size = 0)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ThrowIfDisposed();
        NativeMethods.vello_wgpu_render_pass_set_index_buffer(_handle, buffer.Handle, (uint)format, offset, size);
    }

    public void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0)
    {
        ThrowIfDisposed();
        NativeMethods.vello_wgpu_render_pass_draw(_handle, vertexCount, instanceCount, firstVertex, firstInstance);
    }

    public void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int baseVertex = 0, uint firstInstance = 0)
    {
        ThrowIfDisposed();
        NativeMethods.vello_wgpu_render_pass_draw_indexed(_handle, indexCount, instanceCount, firstIndex, baseVertex, firstInstance);
    }

    public void End()
    {
        if (_disposed || _ended || _handle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.vello_wgpu_render_pass_end(_handle);
        _ended = true;
        _handle = IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        End();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgpuRenderPass()
    {
        if (_handle != IntPtr.Zero && !_ended)
        {
            NativeMethods.vello_wgpu_render_pass_end(_handle);
        }
    }
}

public sealed class WgpuCommandEncoder : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;
    private bool _finished;

    internal WgpuCommandEncoder(IntPtr handle)
    {
        _handle = handle;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed || _finished || _handle == IntPtr.Zero)
        {
            throw new ObjectDisposedException(nameof(WgpuCommandEncoder));
        }
    }

    public WgpuRenderPass BeginRenderPass(WgpuRenderPassDescriptor descriptor)
    {
        ThrowIfDisposed();
        var attachments = descriptor.ColorAttachments ?? throw new ArgumentException("Color attachments must be provided.", nameof(descriptor));
        if (attachments.Count == 0)
        {
            throw new ArgumentException("Render pass requires at least one color attachment.", nameof(descriptor));
        }

        var nativeColorAttachments = new VelloWgpuRenderPassColorAttachmentNative[attachments.Count];
        var handles = new List<GCHandle>();

        for (int i = 0; i < attachments.Count; i++)
        {
            var attachment = attachments[i];
            if (attachment.View is null)
            {
                throw new ArgumentException($"Color attachment {i} must specify a texture view.", nameof(descriptor));
            }

            nativeColorAttachments[i] = new VelloWgpuRenderPassColorAttachmentNative
            {
                View = attachment.View.Handle,
                ResolveTarget = attachment.ResolveTarget?.Handle ?? IntPtr.Zero,
                Load = (uint)attachment.Load,
                Store = (uint)attachment.Store,
                ClearColor = new VelloWgpuColorNative
                {
                    R = attachment.ClearColor.R,
                    G = attachment.ClearColor.G,
                    B = attachment.ClearColor.B,
                    A = attachment.ClearColor.A,
                },
            };
        }

        var colorHandle = GCHandle.Alloc(nativeColorAttachments, GCHandleType.Pinned);
        handles.Add(colorHandle);

        VelloWgpuRenderPassDepthStencilAttachmentNative[]? depthArray = null;
        GCHandle depthHandle = default;
        if (descriptor.DepthStencilAttachment is { } depth)
        {
            if (depth.View is null)
            {
                throw new ArgumentException("Depth stencil attachment must specify a texture view.", nameof(descriptor));
            }

            depthArray = new[]
            {
                new VelloWgpuRenderPassDepthStencilAttachmentNative
                {
                    View = depth.View.Handle,
                    DepthLoad = (uint)depth.DepthLoad,
                    DepthStore = (uint)depth.DepthStore,
                    DepthClear = depth.DepthClearValue,
                    StencilLoad = (uint)depth.StencilLoad,
                    StencilStore = (uint)depth.StencilStore,
                    StencilClear = depth.StencilClearValue,
                    DepthReadOnly = depth.DepthReadOnly,
                    StencilReadOnly = depth.StencilReadOnly,
                }
            };
            depthHandle = GCHandle.Alloc(depthArray, GCHandleType.Pinned);
            handles.Add(depthHandle);
        }

        Span<byte> labelScratch = stackalloc byte[256];
        byte[]? rentedLabel = null;
        try
        {
            var nativeDescriptor = new VelloWgpuRenderPassDescriptorNative
            {
                Label = IntPtr.Zero,
                ColorAttachmentCount = (nuint)nativeColorAttachments.Length,
                ColorAttachments = colorHandle.AddrOfPinnedObject(),
                DepthStencil = depthArray is null ? IntPtr.Zero : depthHandle.AddrOfPinnedObject(),
            };

            IntPtr passHandle;
            if (!string.IsNullOrEmpty(descriptor.Label))
            {
                var labelSpan = NativeHelpers.EncodeUtf8NullTerminated(descriptor.Label, labelScratch, ref rentedLabel);
                unsafe
                {
                    fixed (byte* labelPtr = labelSpan)
                    {
                        nativeDescriptor.Label = labelSpan.IsEmpty ? IntPtr.Zero : (IntPtr)labelPtr;
                        passHandle = NativeMethods.vello_wgpu_command_encoder_begin_render_pass(_handle, &nativeDescriptor);
                    }
                }
            }
            else
            {
                unsafe
                {
                    passHandle = NativeMethods.vello_wgpu_command_encoder_begin_render_pass(_handle, &nativeDescriptor);
                }
            }

            if (passHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to begin render pass.");
            }

            return new WgpuRenderPass(passHandle);
        }
        finally
        {
            foreach (var handle in handles)
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }

            if (rentedLabel is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedLabel);
            }
        }
    }

    public WgpuCommandBuffer Finish(WgpuCommandBufferDescriptor? descriptor = null)
    {
        ThrowIfDisposed();
        unsafe
        {
            Span<byte> labelScratch = stackalloc byte[256];
            byte[]? rentedLabel = null;
            try
            {
                IntPtr bufferHandle;
                if (descriptor is { } desc && !string.IsNullOrEmpty(desc.Label))
                {
                    var labelSpan = NativeHelpers.EncodeUtf8NullTerminated(desc.Label, labelScratch, ref rentedLabel);
                    unsafe
                    {
                        fixed (byte* labelPtr = labelSpan)
                        {
                            var native = new VelloWgpuCommandBufferDescriptorNative
                            {
                                Label = labelSpan.IsEmpty ? IntPtr.Zero : (IntPtr)labelPtr,
                            };
                            bufferHandle = NativeMethods.vello_wgpu_command_encoder_finish(_handle, &native);
                        }
                    }
                }
                else
                {
                    bufferHandle = NativeMethods.vello_wgpu_command_encoder_finish(_handle, null);
                }

                if (bufferHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to finish command encoder.");
                }

                _finished = true;
                _handle = IntPtr.Zero;
                _disposed = true;
                GC.SuppressFinalize(this);

                return new WgpuCommandBuffer(bufferHandle);
            }
            finally
            {
                if (rentedLabel is not null)
                {
                    ArrayPool<byte>.Shared.Return(rentedLabel);
                }
            }
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
            NativeMethods.vello_wgpu_command_encoder_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgpuCommandEncoder()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_command_encoder_destroy(_handle);
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

    public ulong Submit(ReadOnlySpan<WgpuCommandBuffer> commandBuffers)
    {
        ThrowIfDisposed();
        unsafe
        {
            if (commandBuffers.IsEmpty)
            {
                return NativeMethods.vello_wgpu_queue_submit(_handle, null, 0);
            }

            Span<IntPtr> bufferHandles = commandBuffers.Length <= 16
                ? stackalloc IntPtr[commandBuffers.Length]
                : new IntPtr[commandBuffers.Length];

            for (int i = 0; i < commandBuffers.Length; i++)
            {
                ArgumentNullException.ThrowIfNull(commandBuffers[i]);
                bufferHandles[i] = commandBuffers[i].Handle;
            }

            ulong submission;
            fixed (IntPtr* ptr = bufferHandles)
            {
                submission = NativeMethods.vello_wgpu_queue_submit(_handle, ptr, (nuint)commandBuffers.Length);
            }

            for (int i = 0; i < commandBuffers.Length; i++)
            {
                commandBuffers[i].MarkConsumed();
            }

            return submission;
        }
    }

    public void WriteBuffer(WgpuBuffer buffer, ulong offset, ReadOnlySpan<byte> data)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ThrowIfDisposed();
        if (data.IsEmpty)
        {
            return;
        }

        unsafe
        {
            fixed (byte* dataPtr = data)
            {
                var nativeBytes = new VelloBytesNative
                {
                    Data = (IntPtr)dataPtr,
                    Length = (nuint)data.Length,
                };

                NativeHelpers.ThrowOnError(
                    NativeMethods.vello_wgpu_queue_write_buffer(_handle, buffer.Handle, offset, nativeBytes),
                    "vello_wgpu_queue_write_buffer");
            }
        }
    }

    public void WriteTexture(WgpuImageCopyTexture destination, ReadOnlySpan<byte> data, WgpuTextureDataLayout dataLayout, WgpuExtent3D size)
    {
        ArgumentNullException.ThrowIfNull(destination.Texture);
        ThrowIfDisposed();
        if (data.IsEmpty)
        {
            return;
        }

        unsafe
        {
            fixed (byte* dataPtr = data)
            {
                var nativeDestination = new VelloWgpuImageCopyTextureNative
                {
                    Texture = destination.Texture.Handle,
                    MipLevel = destination.MipLevel,
                    Origin = new VelloWgpuOrigin3dNative
                    {
                        X = destination.Origin.X,
                        Y = destination.Origin.Y,
                        Z = destination.Origin.Z,
                    },
                    Aspect = (uint)destination.Aspect,
                };

                var nativeLayout = new VelloWgpuTextureDataLayoutNative
                {
                    Offset = dataLayout.Offset,
                    BytesPerRow = dataLayout.BytesPerRow ?? 0,
                    RowsPerImage = dataLayout.RowsPerImage ?? 0,
                };

                var nativeExtent = new VelloWgpuExtent3dNative
                {
                    Width = size.Width,
                    Height = size.Height,
                    DepthOrArrayLayers = size.DepthOrArrayLayers,
                };

                var nativeBytes = new VelloBytesNative
                {
                    Data = (IntPtr)dataPtr,
                    Length = (nuint)data.Length,
                };

                NativeHelpers.ThrowOnError(
                    NativeMethods.vello_wgpu_queue_write_texture(
                        _handle,
                        &nativeDestination,
                        nativeBytes,
                        nativeLayout,
                        nativeExtent),
                    "vello_wgpu_queue_write_texture");
            }
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

public sealed class WgpuShaderModule : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    internal WgpuShaderModule(IntPtr handle)
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
            NativeMethods.vello_wgpu_shader_module_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgpuShaderModule()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_shader_module_destroy(_handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WgpuShaderModule));
        }
    }
}

public sealed class WgpuBuffer : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    internal WgpuBuffer(IntPtr handle)
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

    public ulong Size
    {
        get
        {
            ThrowIfDisposed();
            return NativeMethods.vello_wgpu_buffer_get_size(_handle);
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
            NativeMethods.vello_wgpu_buffer_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgpuBuffer()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_buffer_destroy(_handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WgpuBuffer));
        }
    }
}

public sealed class WgpuSampler : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    internal WgpuSampler(IntPtr handle)
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
            NativeMethods.vello_wgpu_sampler_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgpuSampler()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_sampler_destroy(_handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WgpuSampler));
        }
    }
}

public sealed class WgpuTexture : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    internal WgpuTexture(IntPtr handle)
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

    public WgpuTextureView CreateView(WgpuTextureViewDescriptor? descriptor = null)
    {
        ThrowIfDisposed();
        unsafe
        {
            Span<byte> labelScratch = stackalloc byte[256];
            byte[]? rentedLabel = null;
            try
            {
                IntPtr viewHandle;
                if (descriptor.HasValue)
                {
                    var desc = descriptor.Value;
                    var native = new WgpuTextureViewDescriptorNative
                    {
                        Label = IntPtr.Zero,
                        Format = (WgpuTextureFormatNative)desc.Format,
                        Dimension = (uint)desc.Dimension,
                        Aspect = (uint)desc.Aspect,
                        BaseMipLevel = desc.BaseMipLevel,
                        MipLevelCount = desc.MipLevelCount ?? 0,
                        BaseArrayLayer = desc.BaseArrayLayer,
                        ArrayLayerCount = desc.ArrayLayerCount ?? 0,
                    };

                    var buffer = stackalloc WgpuTextureViewDescriptorNative[1];
                    buffer[0] = native;
                    var labelSpan = NativeHelpers.EncodeUtf8NullTerminated(desc.Label, labelScratch, ref rentedLabel);

                    fixed (byte* labelPtr = labelSpan)
                    {
                        buffer[0].Label = labelSpan.IsEmpty ? IntPtr.Zero : (IntPtr)labelPtr;
                        viewHandle = NativeMethods.vello_wgpu_texture_create_view(_handle, buffer);
                        buffer[0].Label = IntPtr.Zero;
                    }
                }
                else
                {
                    viewHandle = NativeMethods.vello_wgpu_texture_create_view(_handle, null);
                }

                if (viewHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create texture view.");
                }

                return new WgpuTextureView(viewHandle);
            }
            finally
            {
                if (rentedLabel is not null)
                {
                    ArrayPool<byte>.Shared.Return(rentedLabel);
                }
            }
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
            NativeMethods.vello_wgpu_texture_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgpuTexture()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_texture_destroy(_handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WgpuTexture));
        }
    }
}

public sealed class WgpuBindGroupLayout : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    internal WgpuBindGroupLayout(IntPtr handle)
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
            NativeMethods.vello_wgpu_bind_group_layout_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgpuBindGroupLayout()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_bind_group_layout_destroy(_handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WgpuBindGroupLayout));
        }
    }
}

public sealed class WgpuBindGroup : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    internal WgpuBindGroup(IntPtr handle)
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
            NativeMethods.vello_wgpu_bind_group_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgpuBindGroup()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_bind_group_destroy(_handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WgpuBindGroup));
        }
    }
}

public sealed class WgpuPipelineLayout : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    internal WgpuPipelineLayout(IntPtr handle)
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
            NativeMethods.vello_wgpu_pipeline_layout_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgpuPipelineLayout()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_pipeline_layout_destroy(_handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WgpuPipelineLayout));
        }
    }
}

public sealed class WgpuRenderPipeline : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    internal WgpuRenderPipeline(IntPtr handle)
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
            NativeMethods.vello_wgpu_render_pipeline_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgpuRenderPipeline()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_render_pipeline_destroy(_handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WgpuRenderPipeline));
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

            WgpuTextureFormatNative[]? rented = null;
            Span<WgpuTextureFormatNative> formatBuffer = count <= 8
                ? stackalloc WgpuTextureFormatNative[8]
                : (rented = ArrayPool<WgpuTextureFormatNative>.Shared.Rent(count)).AsSpan();

            var span = formatBuffer[..count];
            if (viewFormats is not null)
            {
                for (int i = 0; i < count; i++)
                {
                    span[i] = (WgpuTextureFormatNative)viewFormats[i];
                }
            }

            try
            {
                fixed (WgpuTextureFormatNative* ptr = span)
                {
                    native.ViewFormats = (IntPtr)ptr;
                    var status = NativeMethods.vello_wgpu_surface_configure(_handle, device.Handle, &native);
                    NativeHelpers.ThrowOnError(status, "Failed to configure surface.");
                    native.ViewFormats = IntPtr.Zero;
                }
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<WgpuTextureFormatNative>.Shared.Return(rented);
                }
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
                var descriptorValue = descriptor.Value;
                Span<byte> labelScratch = stackalloc byte[256];
                byte[]? rented = null;
                try
                {
                    var native = new WgpuTextureViewDescriptorNative
                    {
                        Format = (WgpuTextureFormatNative)descriptorValue.Format,
                        Dimension = (uint)descriptorValue.Dimension,
                        Aspect = (uint)descriptorValue.Aspect,
                        BaseMipLevel = descriptorValue.BaseMipLevel,
                        MipLevelCount = descriptorValue.MipLevelCount ?? 0,
                        BaseArrayLayer = descriptorValue.BaseArrayLayer,
                        ArrayLayerCount = descriptorValue.ArrayLayerCount ?? 0,
                    };

                    var buffer = stackalloc WgpuTextureViewDescriptorNative[1];
                    buffer[0] = native;

                    var labelSpan = NativeHelpers.EncodeUtf8NullTerminated(descriptorValue.Label, labelScratch, ref rented);

                    if (!labelSpan.IsEmpty)
                    {
                        fixed (byte* labelPtr = labelSpan)
                        {
                            buffer[0].Label = (IntPtr)labelPtr;
                            viewHandle = NativeMethods.vello_wgpu_surface_texture_create_view(_handle, buffer);
                            buffer[0].Label = IntPtr.Zero;
                        }
                    }
                    else
                    {
                        buffer[0].Label = IntPtr.Zero;
                        viewHandle = NativeMethods.vello_wgpu_surface_texture_create_view(_handle, buffer);
                    }
                }
                finally
                {
                    if (rented is not null)
                    {
                        ArrayPool<byte>.Shared.Return(rented);
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

public sealed class WgpuPipelineCache : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    internal WgpuPipelineCache(IntPtr handle)
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

    public byte[] GetData()
    {
        ThrowIfDisposed();
        NativeHelpers.ThrowOnError(
            NativeMethods.vello_wgpu_pipeline_cache_get_data(_handle, out var dataPtr, out var length),
            "vello_wgpu_pipeline_cache_get_data");
        if (dataPtr == IntPtr.Zero || length == 0)
        {
            return Array.Empty<byte>();
        }
        var size = checked((int)length);
        var data = new byte[size];
        unsafe
        {
            var source = new ReadOnlySpan<byte>((void*)dataPtr, size);
            source.CopyTo(data);
        }
        NativeMethods.vello_wgpu_pipeline_cache_free_data(dataPtr, length);
        return data;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_pipeline_cache_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgpuPipelineCache()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_wgpu_pipeline_cache_destroy(_handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WgpuPipelineCache));
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

    public bool SetProfilerEnabled(bool enabled)
    {
        ThrowIfDisposed();
        var status = NativeMethods.vello_wgpu_renderer_profiler_set_enabled(_handle, enabled);
        if (status == VelloStatus.Unsupported)
        {
            return false;
        }

        NativeHelpers.ThrowOnError(status, "Failed to configure GPU profiler.");
        return true;
    }

    public GpuProfilerFrame? TryGetProfilerFrame()
    {
        ThrowIfDisposed();

        VelloGpuProfilerResults nativeResults;
        var status = NativeMethods.vello_wgpu_renderer_profiler_get_results(_handle, out nativeResults);
        if (status == VelloStatus.Unsupported)
        {
            return null;
        }

        NativeHelpers.ThrowOnError(status, "Failed to retrieve GPU profiler results.");

        if (nativeResults.Handle == IntPtr.Zero || nativeResults.SliceCount == 0)
        {
            return null;
        }

        try
        {
            var sliceCount = checked((int)nativeResults.SliceCount);
            var labelLength = checked((int)nativeResults.LabelsLength);
            var labelBytes = labelLength > 0 ? new byte[labelLength] : Array.Empty<byte>();
            if (labelLength > 0)
            {
                unsafe
                {
                    var labelSpan = new ReadOnlySpan<byte>((void*)nativeResults.Labels, labelLength);
                    labelSpan.CopyTo(labelBytes);
                }
            }

            var slices = new GpuProfilerSlice[sliceCount];

            unsafe
            {
                var source = (VelloGpuProfilerSlice*)nativeResults.Slices;
                for (int i = 0; i < sliceCount; i++)
                {
                    var slice = source[i];
                    string label = string.Empty;
                    if (slice.LabelLength > 0 && labelBytes.Length > 0)
                    {
                        var offset = checked((int)slice.LabelOffset);
                        var length = checked((int)slice.LabelLength);
                        label = Encoding.UTF8.GetString(labelBytes, offset, length);
                    }

                    slices[i] = new GpuProfilerSlice(
                        label,
                        slice.Depth,
                        slice.HasTime != 0,
                        slice.TimeStartMs,
                        slice.TimeEndMs);
                }
            }

            return new GpuProfilerFrame(nativeResults.TotalGpuTimeMs, slices);
        }
        finally
        {
            NativeMethods.vello_wgpu_renderer_profiler_results_free(nativeResults.Handle);
        }
    }

    public void RenderSurface(
        Scene scene,
        WgpuTextureView surfaceView,
        RenderParams parameters,
        WgpuTextureFormat surfaceFormat)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(surfaceView);
        ThrowIfDisposed();

        var nativeParams = new VelloRenderParams
        {
            Width = parameters.Width,
            Height = parameters.Height,
            BaseColor = parameters.BaseColor.ToNative(),
            Antialiasing = (VelloAaMode)parameters.Antialiasing,
            Format = (VelloRenderFormat)parameters.Format,
        };

        var status = NativeMethods.vello_wgpu_renderer_render_surface(
            _handle,
            scene.Handle,
            surfaceView.Handle,
            nativeParams,
            (WgpuTextureFormatNative)surfaceFormat);

        NativeHelpers.ThrowOnError(status, "Failed to render to wgpu surface texture.");
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

public readonly struct GpuProfilerSlice
{
    public GpuProfilerSlice(string label, uint depth, bool hasTime, double startMilliseconds, double endMilliseconds)
    {
        Label = label;
        Depth = depth;
        HasTime = hasTime;
        StartMilliseconds = startMilliseconds;
        EndMilliseconds = endMilliseconds;
    }

    public string Label { get; }
    public uint Depth { get; }
    public bool HasTime { get; }
    public double StartMilliseconds { get; }
    public double EndMilliseconds { get; }
    public double DurationMilliseconds => HasTime ? Math.Max(0.0, EndMilliseconds - StartMilliseconds) : 0.0;
}

public readonly struct GpuProfilerFrame
{
    public GpuProfilerFrame(double totalMilliseconds, IReadOnlyList<GpuProfilerSlice> slices)
    {
        TotalMilliseconds = totalMilliseconds;
        Slices = slices;
    }

    public double TotalMilliseconds { get; }
    public IReadOnlyList<GpuProfilerSlice> Slices { get; }
}
