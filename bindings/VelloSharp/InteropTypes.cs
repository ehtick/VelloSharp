using System;
using System.Runtime.InteropServices;

namespace VelloSharp;

#pragma warning disable CS1591
internal enum VelloStatus
{
    Success = 0,
    NullPointer = 1,
    InvalidArgument = 2,
    DeviceCreationFailed = 3,
    RenderError = 4,
    MapFailed = 5,
    Unsupported = 6,
}

internal enum AccessKitStatus : int
{
    Success = 0,
    NullPointer = 1,
    InvalidArgument = 2,
    JsonError = 3,
    OutOfMemory = 4,
}


internal enum KurboStatus : int
{
    Success = 0,
    NullPointer = 1,
    InvalidArgument = 2,
    Singular = 3,
    OutOfMemory = 4,
}

internal enum VelloSparseStatus : int
{
    Success = 0,
    NullPointer = 1,
    InvalidArgument = 2,
    RenderError = 3,
}

#pragma warning restore CS1591

public enum PenikoStatus : int
{
    Success = 0,
    NullPointer = 1,
    InvalidArgument = 2,
    OutOfMemory = 3,
    Unsupported = 4,
}

public enum PenikoExtend : int
{
    Pad = 0,
    Repeat = 1,
    Reflect = 2,
}

public enum PenikoBrushKind : int
{
    Solid = 0,
    Gradient = 1,
    Image = 2,
}

public enum PenikoGradientKind : int
{
    Linear = 0,
    Radial = 1,
    Sweep = 2,
}

[StructLayout(LayoutKind.Sequential)]
public struct PenikoPoint
{
    public double X;
    public double Y;
}

[StructLayout(LayoutKind.Sequential)]
public struct PenikoLinearGradient
{
    public PenikoPoint Start;
    public PenikoPoint End;
}

[StructLayout(LayoutKind.Sequential)]
public struct PenikoRadialGradient
{
    public PenikoPoint StartCenter;
    public float StartRadius;
    public PenikoPoint EndCenter;
    public float EndRadius;
}

[StructLayout(LayoutKind.Sequential)]
public struct PenikoSweepGradient
{
    public PenikoPoint Center;
    public float StartAngle;
    public float EndAngle;
}

[StructLayout(LayoutKind.Sequential)]
public struct PenikoColorStop
{
    public float Offset;
    public VelloColor Color;
}

public enum WinitStatus : int
{
    Success = 0,
    NullPointer = 1,
    InvalidArgument = 2,
    RuntimeError = 3,
    WindowCreationFailed = 4,
    CallbackPanicked = 5,
    InvalidState = 6,
    Unsupported = 7,
}

public enum WinitControlFlow : int
{
    Poll = 0,
    Wait = 1,
    WaitUntil = 2,
    Exit = 3,
}

public enum WinitStartCause : int
{
    Init = 0,
    Poll = 1,
    WaitCancelled = 2,
    ResumeTimeReached = 3,
}

public enum WinitEventKind : int
{
    NewEvents = 0,
    Resumed = 1,
    Suspended = 2,
    WindowCreated = 3,
    WindowResized = 4,
    WindowScaleFactorChanged = 5,
    WindowCloseRequested = 6,
    WindowRedrawRequested = 7,
    WindowDestroyed = 8,
    AboutToWait = 9,
    MemoryWarning = 10,
    Exiting = 11,
    WindowFocused = 12,
    WindowFocusLost = 13,
    CursorMoved = 14,
    CursorEntered = 15,
    CursorLeft = 16,
    MouseInput = 17,
    MouseWheel = 18,
    KeyboardInput = 19,
    ModifiersChanged = 20,
    Touch = 21,
    TextInput = 22,
    AccessKitEvent = 23,
}

public enum WinitAccessKitEventKind : int
{
    None = 0,
    InitialTreeRequested = 1,
    ActionRequested = 2,
    AccessibilityDeactivated = 3,
}

public enum WinitResizeDirection : int
{
    East = 0,
    North = 1,
    NorthEast = 2,
    NorthWest = 3,
    South = 4,
    SouthEast = 5,
    SouthWest = 6,
    West = 7,
}

public enum WinitWindowLevel : int
{
    AlwaysOnBottom = 0,
    Normal = 1,
    AlwaysOnTop = 2,
}

[Flags]
public enum WinitWindowButtons : uint
{
    None = 0,
    Close = 1 << 0,
    Minimize = 1 << 1,
    Maximize = 1 << 2,
}

public enum WinitCursorIcon : int
{
    Default = 0,
    Pointer = 1,
    Text = 2,
    Crosshair = 3,
    Wait = 4,
    Progress = 5,
    Help = 6,
    NotAllowed = 7,
    Move = 8,
    Alias = 9,
    Copy = 10,
    Grab = 11,
    Grabbing = 12,
    EResize = 13,
    NResize = 14,
    NeResize = 15,
    NwResize = 16,
    SResize = 17,
    SeResize = 18,
    SwResize = 19,
    WResize = 20,
    EwResize = 21,
    NsResize = 22,
    NeswResize = 23,
    NwseResize = 24,
    ColResize = 25,
    RowResize = 26,
    AllScroll = 27,
}

public enum WinitMouseButton : int
{
    Left = 0,
    Right = 1,
    Middle = 2,
    Back = 3,
    Forward = 4,
    Other = 5,
}

public enum WinitElementState : int
{
    Released = 0,
    Pressed = 1,
}

public enum WinitMouseScrollDeltaKind : int
{
    LineDelta = 0,
    PixelDelta = 1,
}

public enum WinitKeyLocation : int
{
    Standard = 0,
    Left = 1,
    Right = 2,
    Numpad = 3,
}

public enum WinitTouchPhaseKind : int
{
    Started = 0,
    Moved = 1,
    Ended = 2,
    Cancelled = 3,
}

[Flags]
public enum WinitModifiers : uint
{
    None = 0,
    Shift = 0b100,
    Control = 0b100 << 3,
    Alt = 0b100 << 6,
    Meta = 0b100 << 9,
}

[StructLayout(LayoutKind.Sequential)]
internal struct WinitWindowDescriptor
{
    public uint Width;
    public uint Height;
    public uint MinWidth;
    public uint MinHeight;
    public uint MaxWidth;
    public uint MaxHeight;
    [MarshalAs(UnmanagedType.I1)]
    public bool Resizable;
    [MarshalAs(UnmanagedType.I1)]
    public bool Decorations;
    [MarshalAs(UnmanagedType.I1)]
    public bool Transparent;
    [MarshalAs(UnmanagedType.I1)]
    public bool Visible;
    public nint Title;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WinitRunOptions
{
    [MarshalAs(UnmanagedType.I1)]
    public bool CreateWindow;
    public WinitWindowDescriptor Window;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct WinitEvent
{
    public WinitEventKind Kind;
    public nint Window;
    public uint Width;
    public uint Height;
    public double ScaleFactor;
    public WinitStartCause StartCause;
    public double MouseX;
    public double MouseY;
    public double DeltaX;
    public double DeltaY;
    public uint Modifiers;
    public WinitMouseButton MouseButton;
    public uint MouseButtonValue;
    public WinitElementState ElementState;
    public WinitMouseScrollDeltaKind ScrollDeltaKind;
    public uint KeyCode;
    public fixed byte KeyCodeName[64];
    public WinitKeyLocation KeyLocation;
    [MarshalAs(UnmanagedType.I1)]
    public bool Repeat;
    public ulong TouchId;
    public WinitTouchPhaseKind TouchPhase;
    public nint Text;
    public WinitAccessKitEventKind AccessKitEventKind;
    public nint AccessKitAction;
}


internal enum VelloFillRule : int
{
    NonZero = 0,
    EvenOdd = 1,
}

internal enum VelloPathVerb : int
{
    MoveTo = 0,
    LineTo = 1,
    QuadTo = 2,
    CubicTo = 3,
    Close = 4,
}

internal enum VelloLineCap : int
{
    Butt = 0,
    Round = 1,
    Square = 2,
}

internal enum VelloLineJoin : int
{
    Miter = 0,
    Round = 1,
    Bevel = 2,
}

internal enum VelloAaMode : int
{
    Area = 0,
    Msaa8 = 1,
    Msaa16 = 2,
}

internal enum VelloPresentMode : int
{
    AutoVsync = 0,
    AutoNoVsync = 1,
    Fifo = 2,
    Immediate = 3,
}

internal enum VelloRenderFormat : int
{
    Rgba8 = 0,
    Bgra8 = 1,
}

public enum VelloWindowHandleKind : int
{
    None = 0,
    Win32 = 1,
    AppKit = 2,
    Wayland = 3,
    Xlib = 4,
    Headless = 100,
}

internal enum VelloImageAlphaMode : int
{
    Straight = 0,
    Premultiplied = 1,
}

internal enum VelloExtendMode : int
{
    Pad = 0,
    Repeat = 1,
    Reflect = 2,
}

internal enum VelloImageQualityMode : int
{
    Low = 0,
    Medium = 1,
    High = 2,
}

internal enum VelloBrushKind : int
{
    Solid = 0,
    LinearGradient = 1,
    RadialGradient = 2,
    SweepGradient = 3,
    Image = 4,
}

internal enum VelloBlendMix : int
{
    Normal = 0,
    Multiply = 1,
    Screen = 2,
    Overlay = 3,
    Darken = 4,
    Lighten = 5,
    ColorDodge = 6,
    ColorBurn = 7,
    HardLight = 8,
    SoftLight = 9,
    Difference = 10,
    Exclusion = 11,
    Hue = 12,
    Saturation = 13,
    Color = 14,
    Luminosity = 15,
    Clip = 128,
}

internal enum VelloBlendCompose : int
{
    Clear = 0,
    Copy = 1,
    Dest = 2,
    SrcOver = 3,
    DestOver = 4,
    SrcIn = 5,
    DestIn = 6,
    SrcOut = 7,
    DestOut = 8,
    SrcAtop = 9,
    DestAtop = 10,
    Xor = 11,
    Plus = 12,
    PlusLighter = 13,
}

internal enum VelloGlyphRunStyle : int
{
    Fill = 0,
    Stroke = 1,
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloPoint
{
    public double X;
    public double Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloPathElement
{
    public VelloPathVerb Verb;
    private int _padding;
    public double X0;
    public double Y0;
    public double X1;
    public double Y1;
    public double X2;
    public double Y2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloGlyphOutlineData
{
    public IntPtr Commands;
    public nuint CommandCount;
    public VelloRect Bounds;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloAffine
{
    public double M11;
    public double M12;
    public double M21;
    public double M22;
    public double Dx;
    public double Dy;
}

[StructLayout(LayoutKind.Sequential)]
public struct VelloColor
{
    public float R;
    public float G;
    public float B;
    public float A;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloGradientStop
{
    public float Offset;
    public VelloColor Color;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct VelloLinearGradient
{
    public VelloPoint Start;
    public VelloPoint End;
    public VelloExtendMode Extend;
    public IntPtr Stops;
    public nuint StopCount;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct VelloRadialGradient
{
    public VelloPoint StartCenter;
    public float StartRadius;
    public VelloPoint EndCenter;
    public float EndRadius;
    public VelloExtendMode Extend;
    public IntPtr Stops;
    public nuint StopCount;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct VelloSweepGradient
{
    public VelloPoint Center;
    public float StartAngle;
    public float EndAngle;
    public VelloExtendMode Extend;
    public IntPtr Stops;
    public nuint StopCount;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct VelloImageBrushParams
{
    public IntPtr Image;
    public VelloExtendMode XExtend;
    public VelloExtendMode YExtend;
    public VelloImageQualityMode Quality;
    public float Alpha;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct VelloBrush
{
    public VelloBrushKind Kind;
    public VelloColor Solid;
    public VelloLinearGradient Linear;
    public VelloRadialGradient Radial;
    public VelloSweepGradient Sweep;
    public VelloImageBrushParams Image;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct VelloStrokeStyle
{
    public double Width;
    public double MiterLimit;
    public VelloLineCap StartCap;
    public VelloLineCap EndCap;
    public VelloLineJoin LineJoin;
    public double DashPhase;
    public IntPtr DashPattern;
    public nuint DashLength;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloGlyphMetricsNative
{
    public float Advance;
    public float XBearing;
    public float YBearing;
    public float Width;
    public float Height;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloFontMetricsNative
{
    public ushort UnitsPerEm;
    public ushort GlyphCount;
    public float Ascent;
    public float Descent;
    public float Leading;
    public float UnderlinePosition;
    public float UnderlineThickness;
    public float StrikeoutPosition;
    public float StrikeoutThickness;
    [MarshalAs(UnmanagedType.I1)]
    public bool IsMonospace;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloParleyFontInfoNative
{
    public IntPtr FamilyName;
    public IntPtr Data;
    public nuint Length;
    public uint Index;
    public float Weight;
    public float Stretch;
    public int Style;
    [MarshalAs(UnmanagedType.I1)]
    public bool IsMonospace;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloStringArrayNative
{
    public IntPtr Items;
    public nuint Count;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloShapedGlyphNative
{
    public uint GlyphId;
    public uint Cluster;
    public float XAdvance;
    public float XOffset;
    public float YOffset;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloOpenTypeFeatureNative
{
    public uint Tag;
    public uint Value;
    public uint Start;
    public uint End;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloVariationAxisValueNative
{
    public uint Tag;
    public float Value;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VelloTextShapeOptionsNative
{
    public float FontSize;
    public int Direction;
    public uint ScriptTag;
    public byte* Language;
    public nuint LanguageLength;
    public VelloOpenTypeFeatureNative* Features;
    public nuint FeatureCount;
    public VelloVariationAxisValueNative* VariationAxes;
    public nuint VariationAxisCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloScriptSegmentNative
{
    public uint Start;
    public uint Length;
    public uint ScriptTag;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloScriptSegmentArrayNative
{
    public IntPtr Segments;
    public nuint Count;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloShapedRunNative
{
    public IntPtr Glyphs;
    public nuint GlyphCount;
    public float Advance;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloVariationAxisNative
{
    public uint Tag;
    public float MinValue;
    public float DefaultValue;
    public float MaxValue;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloVariationAxisArrayNative
{
    public IntPtr Axes;
    public nuint Count;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloRenderParams
{
    public uint Width;
    public uint Height;
    public VelloColor BaseColor;
    public VelloAaMode Antialiasing;
    public VelloRenderFormat Format;
}

internal enum VelloSparseRenderMode : int
{
    OptimizeSpeed = 0,
    OptimizeQuality = 1,
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloRect
{
    public double X;
    public double Y;
    public double Width;
    public double Height;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloImageInfoNative
{
    public uint Width;
    public uint Height;
    public VelloRenderFormat Format;
    public VelloImageAlphaMode Alpha;
    public nuint Stride;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloBlobDataNative
{
    public IntPtr Data;
    public nuint Length;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloVelatoCompositionInfo
{
    public double StartFrame;
    public double EndFrame;
    public double FrameRate;
    public uint Width;
    public uint Height;
}

internal enum WgpuPowerPreferenceNative : uint
{
    None = 0,
    LowPower = 1,
    HighPerformance = 2,
}

internal enum WgpuDx12CompilerNative : uint
{
    Default = 0,
    Fxc = 1,
    Dxc = 2,
}

internal enum WgpuLimitsPresetNative : uint
{
    Default = 0,
    DownlevelWebGl2 = 1,
    DownlevelDefault = 2,
    AdapterDefault = 3,
}

internal enum WgpuCompositeAlphaModeNative : uint
{
    Auto = 0,
    Opaque = 1,
    Premultiplied = 2,
    PostMultiplied = 3,
    Inherit = 4,
}

internal enum WgpuTextureFormatNative : uint
{
    Rgba8Unorm = 0,
    Rgba8UnormSrgb = 1,
    Bgra8Unorm = 2,
    Bgra8UnormSrgb = 3,
    Rgba16Float = 4,
    R8Uint = 5,
}

internal enum WgpuBackendTypeNative : uint
{
    Noop = 0,
    Vulkan = 1,
    Metal = 2,
    Dx12 = 3,
    Gl = 4,
    BrowserWebGpu = 5,
}

internal enum WgpuDeviceTypeNative : uint
{
    Other = 0,
    IntegratedGpu = 1,
    DiscreteGpu = 2,
    VirtualGpu = 3,
    Cpu = 4,
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuInstanceDescriptorNative
{
    public uint Backends;
    public uint Flags;
    public WgpuDx12CompilerNative Dx12ShaderCompiler;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuRequestAdapterOptionsNative
{
    public WgpuPowerPreferenceNative PowerPreference;
    [MarshalAs(UnmanagedType.I1)]
    public bool ForceFallbackAdapter;
    public IntPtr CompatibleSurface;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuDeviceDescriptorNative
{
    public IntPtr Label;
    public ulong RequiredFeatures;
    public WgpuLimitsPresetNative Limits;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuAdapterInfoNative
{
    public uint Vendor;
    public uint Device;
    public WgpuBackendTypeNative Backend;
    public WgpuDeviceTypeNative DeviceType;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuSurfaceConfigurationNative
{
    public uint Usage;
    public WgpuTextureFormatNative Format;
    public uint Width;
    public uint Height;
    public VelloPresentMode PresentMode;
    public WgpuCompositeAlphaModeNative AlphaMode;
    public nuint ViewFormatCount;
    public IntPtr ViewFormats;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuTextureViewDescriptorNative
{
    public IntPtr Label;
    public WgpuTextureFormatNative Format;
    public uint Dimension;
    public uint Aspect;
    public uint BaseMipLevel;
    public uint MipLevelCount;
    public uint BaseArrayLayer;
    public uint ArrayLayerCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuPipelineCacheDescriptorNative
{
    public IntPtr Label;
    public IntPtr Data;
    public nuint DataLength;
    [MarshalAs(UnmanagedType.I1)]
    public bool Fallback;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloBytesNative
{
    public IntPtr Data;
    public nuint Length;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloU32SliceNative
{
    public IntPtr Data;
    public nuint Length;
}

internal enum WgpuShaderSourceKindNative : uint
{
    Wgsl = 0,
    Spirv = 1,
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuShaderModuleDescriptorNative
{
    public IntPtr Label;
    public WgpuShaderSourceKindNative SourceKind;
    public VelloBytesNative SourceWgsl;
    public VelloU32SliceNative SourceSpirv;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuBufferDescriptorNative
{
    public IntPtr Label;
    public uint Usage;
    public ulong Size;
    [MarshalAs(UnmanagedType.I1)]
    public bool MappedAtCreation;
    public VelloBytesNative InitialData;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuExtent3DNative
{
    public uint Width;
    public uint Height;
    public uint DepthOrArrayLayers;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuTextureDescriptorNative
{
    public IntPtr Label;
    public WgpuExtent3DNative Size;
    public uint MipLevelCount;
    public uint SampleCount;
    public uint Dimension;
    public WgpuTextureFormatNative Format;
    public uint Usage;
    public nuint ViewFormatCount;
    public IntPtr ViewFormats;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuSamplerDescriptorNative
{
    public IntPtr Label;
    public uint AddressModeU;
    public uint AddressModeV;
    public uint AddressModeW;
    public uint MagFilter;
    public uint MinFilter;
    public uint MipFilter;
    public float LodMinClamp;
    public float LodMaxClamp;
    public uint Compare;
    public ushort AnisotropyClamp;
    public ushort Padding;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuBindGroupLayoutEntryNative
{
    public uint Binding;
    public uint Visibility;
    public uint Type;
    [MarshalAs(UnmanagedType.I1)]
    public bool HasDynamicOffset;
    public ulong MinBindingSize;
    public uint BufferType;
    public uint TextureViewDimension;
    public uint TextureSampleType;
    [MarshalAs(UnmanagedType.I1)]
    public bool TextureMultisampled;
    public uint StorageTextureAccess;
    public WgpuTextureFormatNative StorageTextureFormat;
    public uint SamplerType;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuBindGroupLayoutDescriptorNative
{
    public IntPtr Label;
    public nuint EntryCount;
    public IntPtr Entries;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuBindGroupEntryNative
{
    public uint Binding;
    public IntPtr Buffer;
    public ulong Offset;
    public ulong Size;
    public IntPtr Sampler;
    public IntPtr TextureView;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuBindGroupDescriptorNative
{
    public IntPtr Label;
    public IntPtr Layout;
    public nuint EntryCount;
    public IntPtr Entries;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuPipelineLayoutDescriptorNative
{
    public IntPtr Label;
    public nuint BindGroupLayoutCount;
    public IntPtr BindGroupLayouts;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuVertexAttributeNative
{
    public uint Format;
    public ulong Offset;
    public uint ShaderLocation;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuVertexBufferLayoutNative
{
    public ulong ArrayStride;
    public uint StepMode;
    public nuint AttributeCount;
    public IntPtr Attributes;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuVertexStateNative
{
    public IntPtr Module;
    public VelloBytesNative EntryPoint;
    public nuint BufferCount;
    public IntPtr Buffers;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuBlendComponentNative
{
    public uint SrcFactor;
    public uint DstFactor;
    public uint Operation;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuBlendStateNative
{
    public WgpuBlendComponentNative Color;
    public WgpuBlendComponentNative Alpha;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuColorTargetStateNative
{
    public WgpuTextureFormatNative Format;
    public IntPtr Blend;
    public uint WriteMask;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuFragmentStateNative
{
    public IntPtr Module;
    public VelloBytesNative EntryPoint;
    public nuint TargetCount;
    public IntPtr Targets;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuStencilFaceStateNative
{
    public uint Compare;
    public uint FailOp;
    public uint DepthFailOp;
    public uint PassOp;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuDepthStencilStateNative
{
    public WgpuTextureFormatNative Format;
    [MarshalAs(UnmanagedType.I1)]
    public bool DepthWriteEnabled;
    public uint DepthCompare;
    public WgpuStencilFaceStateNative StencilFront;
    public WgpuStencilFaceStateNative StencilBack;
    public uint StencilReadMask;
    public uint StencilWriteMask;
    public int BiasConstant;
    public float BiasSlopeScale;
    public float BiasClamp;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuPrimitiveStateNative
{
    public uint Topology;
    public uint StripIndexFormat;
    public uint FrontFace;
    public uint CullMode;
    [MarshalAs(UnmanagedType.I1)]
    public bool UnclippedDepth;
    public uint PolygonMode;
    [MarshalAs(UnmanagedType.I1)]
    public bool Conservative;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuMultisampleStateNative
{
    public uint Count;
    public uint Mask;
    [MarshalAs(UnmanagedType.I1)]
    public bool AlphaToCoverageEnabled;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WgpuRenderPipelineDescriptorNative
{
    public IntPtr Label;
    public IntPtr Layout;
    public WgpuVertexStateNative Vertex;
    public WgpuPrimitiveStateNative Primitive;
    public IntPtr DepthStencil;
    public WgpuMultisampleStateNative Multisample;
    public IntPtr Fragment;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloWgpuCommandEncoderDescriptorNative
{
    public IntPtr Label;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloWgpuColorNative
{
    public double R;
    public double G;
    public double B;
    public double A;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloWgpuRenderPassColorAttachmentNative
{
    public IntPtr View;
    public IntPtr ResolveTarget;
    public uint Load;
    public uint Store;
    public VelloWgpuColorNative ClearColor;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloWgpuRenderPassDepthStencilAttachmentNative
{
    public IntPtr View;
    public uint DepthLoad;
    public uint DepthStore;
    public float DepthClear;
    public uint StencilLoad;
    public uint StencilStore;
    public uint StencilClear;
    [MarshalAs(UnmanagedType.I1)]
    public bool DepthReadOnly;
    [MarshalAs(UnmanagedType.I1)]
    public bool StencilReadOnly;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloWgpuRenderPassDescriptorNative
{
    public IntPtr Label;
    public nuint ColorAttachmentCount;
    public IntPtr ColorAttachments;
    public IntPtr DepthStencil;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloWgpuOrigin3dNative
{
    public uint X;
    public uint Y;
    public uint Z;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloWgpuImageCopyTextureNative
{
    public IntPtr Texture;
    public uint MipLevel;
    public VelloWgpuOrigin3dNative Origin;
    public uint Aspect;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloWgpuTextureDataLayoutNative
{
    public ulong Offset;
    public uint BytesPerRow;
    public uint RowsPerImage;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloWgpuExtent3dNative
{
    public uint Width;
    public uint Height;
    public uint DepthOrArrayLayers;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloWgpuCommandBufferDescriptorNative
{
    public IntPtr Label;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloLayerParams
{
    public VelloBlendMix Mix;
    public VelloBlendCompose Compose;
    public float Alpha;
    public VelloAffine Transform;
    public IntPtr ClipElements;
    public nuint ClipElementCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloRendererOptions
{
    [MarshalAs(UnmanagedType.I1)]
    public bool UseCpu;
    [MarshalAs(UnmanagedType.I1)]
    public bool SupportArea;
    [MarshalAs(UnmanagedType.I1)]
    public bool SupportMsaa8;
    [MarshalAs(UnmanagedType.I1)]
    public bool SupportMsaa16;
    public int InitThreads;
    public IntPtr PipelineCache;
}

[StructLayout(LayoutKind.Sequential)]
public struct VelloWin32WindowHandle
{
    public IntPtr Hwnd;
    public IntPtr HInstance;
}

[StructLayout(LayoutKind.Sequential)]
public struct VelloAppKitWindowHandle
{
    public IntPtr NsView;
}

[StructLayout(LayoutKind.Sequential)]
public struct VelloWaylandWindowHandle
{
    public IntPtr Surface;
    public IntPtr Display;
}

[StructLayout(LayoutKind.Sequential)]
public struct VelloXlibWindowHandle
{
    public ulong Window;
    public IntPtr Display;
    public int Screen;
    public ulong VisualId;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloGpuProfilerSlice
{
    public nuint LabelOffset;
    public nuint LabelLength;
    public uint Depth;
    public byte HasTime;
    public double TimeStartMs;
    public double TimeEndMs;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloGpuProfilerResults
{
    public IntPtr Handle;
    public IntPtr Slices;
    public nuint SliceCount;
    public IntPtr Labels;
    public nuint LabelsLength;
    public double TotalGpuTimeMs;
}

[StructLayout(LayoutKind.Explicit)]
public struct VelloWindowHandlePayload
{
    [FieldOffset(0)]
    public VelloWin32WindowHandle Win32;
    [FieldOffset(0)]
    public VelloAppKitWindowHandle AppKit;
    [FieldOffset(0)]
    public VelloWaylandWindowHandle Wayland;
    [FieldOffset(0)]
    public VelloXlibWindowHandle Xlib;
    [FieldOffset(0)]
    public IntPtr None;
}

[StructLayout(LayoutKind.Sequential)]
public struct VelloWindowHandle
{
    public VelloWindowHandleKind Kind;
    public VelloWindowHandlePayload Payload;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloSurfaceDescriptor
{
    public uint Width;
    public uint Height;
    public VelloPresentMode PresentMode;
    public VelloWindowHandle Handle;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloGlyph
{
    public uint Id;
    public float X;
    public float Y;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct VelloGlyphRunOptions
{
    public VelloAffine Transform;
    public IntPtr GlyphTransform;
    public float FontSize;
    [MarshalAs(UnmanagedType.I1)]
    public bool Hint;
    public VelloGlyphRunStyle Style;
    public VelloBrush Brush;
    public float BrushAlpha;
    public VelloStrokeStyle StrokeStyle;
}
