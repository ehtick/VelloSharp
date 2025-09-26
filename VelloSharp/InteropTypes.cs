using System;
using System.Runtime.InteropServices;

namespace VelloSharp;

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


internal enum KurboStatus : int
{
    Success = 0,
    NullPointer = 1,
    InvalidArgument = 2,
    Singular = 3,
    OutOfMemory = 4,
}

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
internal struct WinitEvent
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
    public WinitKeyLocation KeyLocation;
    [MarshalAs(UnmanagedType.I1)]
    public bool Repeat;
    public ulong TouchId;
    public WinitTouchPhaseKind TouchPhase;
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
    Image = 3,
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

[StructLayout(LayoutKind.Sequential)]
internal struct VelloLinearGradient
{
    public VelloPoint Start;
    public VelloPoint End;
    public VelloExtendMode Extend;
    public IntPtr Stops;
    public nuint StopCount;
}

[StructLayout(LayoutKind.Sequential)]
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

[StructLayout(LayoutKind.Sequential)]
internal struct VelloImageBrushParams
{
    public IntPtr Image;
    public VelloExtendMode XExtend;
    public VelloExtendMode YExtend;
    public VelloImageQualityMode Quality;
    public float Alpha;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VelloBrush
{
    public VelloBrushKind Kind;
    public VelloColor Solid;
    public VelloLinearGradient Linear;
    public VelloRadialGradient Radial;
    public VelloImageBrushParams Image;
}

[StructLayout(LayoutKind.Sequential)]
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
internal struct VelloRenderParams
{
    public uint Width;
    public uint Height;
    public VelloColor BaseColor;
    public VelloAaMode Antialiasing;
    public VelloRenderFormat Format;
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

[StructLayout(LayoutKind.Sequential)]
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
