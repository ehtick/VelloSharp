using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace VelloSharp;

public interface IWinitEventHandler
{
    void HandleEvent(WinitEventLoopContext context, in WinitEventArgs args);
}

public unsafe sealed class WinitEventLoop
{
    private readonly delegate* unmanaged[Cdecl]<nint, nint, WinitEvent*, void> _callback;

    public WinitEventLoop()
    {
        _callback = &OnNativeEvent;
    }

    public WinitStatus Run(WinitRunConfiguration configuration, IWinitEventHandler handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var nativeOptions = configuration.ToNative(out var titlePtr);
        var handle = GCHandle.Alloc(handler);
        try
        {
            return WinitNativeMethods.winit_event_loop_run(ref nativeOptions, _callback, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            if (titlePtr != nint.Zero)
            {
                unsafe
                {
                    NativeMemory.Free((void*)titlePtr);
                }
            }

            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OnNativeEvent(nint userData, nint contextPtr, WinitEvent* evtPtr)
    {
        if (evtPtr is null)
        {
            return;
        }

        if (userData == nint.Zero)
        {
            return;
        }

        var gcHandle = GCHandle.FromIntPtr(userData);
        if (!gcHandle.IsAllocated || gcHandle.Target is not IWinitEventHandler handler)
        {
            return;
        }

        var context = new WinitEventLoopContext(contextPtr);
        ref readonly WinitEvent evt = ref Unsafe.AsRef<WinitEvent>(evtPtr);
        var args = new WinitEventArgs(in evt);
        handler.HandleEvent(context, args);
    }
}

public readonly struct WinitEventLoopContext
{
    private readonly nint _context;

    internal WinitEventLoopContext(nint context)
    {
        _context = context;
    }

    internal nint Handle => _context;

    public void SetControlFlow(WinitControlFlow flow, TimeSpan? wait = null)
    {
        var millis = wait.HasValue ? (long)Math.Max(0, wait.Value.TotalMilliseconds) : 0;
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_context_set_control_flow(_context, flow, millis), "winit_context_set_control_flow");
    }

    public void Exit()
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_context_exit(_context), "winit_context_exit");
    }

    public bool IsExiting
    {
        get
        {
            NativeHelpers.ThrowOnError(WinitNativeMethods.winit_context_is_exiting(_context, out var exiting), "winit_context_is_exiting");
            return exiting;
        }
    }

    public WinitWindow? GetWindow()
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_context_get_window(_context, out var window), "winit_context_get_window");
        return window == nint.Zero ? null : new WinitWindow(window);
    }

    public bool InitializeAccessKit(WinitWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var status = WinitNativeMethods.winit_context_window_accesskit_init(_context, window.Handle);
        if (status == WinitStatus.Unsupported)
        {
            return false;
        }

        NativeHelpers.ThrowOnError(status, "winit_context_window_accesskit_init");
        return true;
    }
}

public sealed class WinitWindow
{
    private readonly nint _handle;

    internal WinitWindow(nint handle)
    {
        _handle = handle;
    }

    internal nint Handle => _handle;

    public void RequestRedraw()
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_request_redraw(_handle), "winit_window_request_redraw");
    }

    public void PrePresentNotify()
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_pre_present_notify(_handle), "winit_window_pre_present_notify");
    }

    public (uint Width, uint Height) GetSurfaceSize()
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_surface_size(_handle, out var width, out var height), "winit_window_surface_size");
        return (width, height);
    }

    public double ScaleFactor
    {
        get
        {
            NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_scale_factor(_handle, out var scale), "winit_window_scale_factor");
            return scale;
        }
    }

    public ulong Id
    {
        get
        {
            NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_id(_handle, out var id), "winit_window_id");
            return id;
        }
    }

    public void SetTitle(string title)
    {
        title ??= string.Empty;
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_set_title(_handle, title), "winit_window_set_title");
    }

    public VelloWindowHandle GetVelloWindowHandle()
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_get_vello_handle(_handle, out var handle), "winit_window_get_vello_handle");
        return handle;
    }

    public void SetInnerSize(uint width, uint height)
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_set_inner_size(_handle, width, height), "winit_window_set_inner_size");
    }

    public void SetMinInnerSize(uint width, uint height)
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_set_min_inner_size(_handle, width, height), "winit_window_set_min_inner_size");
    }

    public void SetMaxInnerSize(uint width, uint height)
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_set_max_inner_size(_handle, width, height), "winit_window_set_max_inner_size");
    }

    public void SetVisible(bool visible)
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_set_visible(_handle, visible), "winit_window_set_visible");
    }

    public void SetResizable(bool resizable)
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_set_resizable(_handle, resizable), "winit_window_set_resizable");
    }

    public void SetDecorations(bool decorations)
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_set_decorations(_handle, decorations), "winit_window_set_decorations");
    }

    public void SetOuterPosition(int x, int y)
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_set_outer_position(_handle, x, y), "winit_window_set_outer_position");
    }

    public WinitStatus TryBeginMoveDrag()
    {
        var status = WinitNativeMethods.winit_window_drag_window(_handle);
        if (status != WinitStatus.Success && status != WinitStatus.Unsupported)
        {
            NativeHelpers.ThrowOnError(status, "winit_window_drag_window");
        }

        return status;
    }

    public WinitStatus TryBeginResizeDrag(WinitResizeDirection direction)
    {
        var status = WinitNativeMethods.winit_window_drag_resize_window(_handle, direction);
        if (status != WinitStatus.Success && status != WinitStatus.Unsupported)
        {
            NativeHelpers.ThrowOnError(status, "winit_window_drag_resize_window");
        }

        return status;
    }

    public void SetWindowLevel(WinitWindowLevel level)
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_set_window_level(_handle, level), "winit_window_set_window_level");
    }

    public void SetEnabledButtons(WinitWindowButtons buttons)
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_set_enabled_buttons(_handle, (uint)buttons), "winit_window_set_enabled_buttons");
    }

    public void SetEnabled(bool enabled)
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_set_enabled(_handle, enabled), "winit_window_set_enabled");
    }

    public void SetSkipTaskbar(bool skip)
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_set_skip_taskbar(_handle, skip), "winit_window_set_skip_taskbar");
    }

    public void SetWindowIcon(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            unsafe
            {
                NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_set_icon(_handle, null, 0), "winit_window_set_icon");
            }

            return;
        }

        unsafe
        {
            fixed (byte* ptr = data)
            {
                NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_set_icon(_handle, ptr, (nuint)data.Length), "winit_window_set_icon");
            }
        }
    }

    public void ClearWindowIcon()
    {
        unsafe
        {
            NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_set_icon(_handle, null, 0), "winit_window_set_icon");
        }
    }

    public void SetCursor(WinitCursorIcon icon)
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_set_cursor_icon(_handle, icon), "winit_window_set_cursor_icon");
    }

    public void SetCursorVisible(bool visible)
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_set_cursor_visible(_handle, visible), "winit_window_set_cursor_visible");
    }

    public void SetMinimized(bool minimized)
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_set_minimized(_handle, minimized), "winit_window_set_minimized");
    }

    public void SetMaximized(bool maximized)
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_set_maximized(_handle, maximized), "winit_window_set_maximized");
    }

    public void Focus()
    {
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_focus(_handle), "winit_window_focus");
    }

    public void SubmitAccessKitUpdate(string updateJson)
    {
        ArgumentNullException.ThrowIfNull(updateJson);
        NativeHelpers.ThrowOnError(WinitNativeMethods.winit_window_accesskit_update(_handle, updateJson), "winit_window_accesskit_update");
    }
}

public readonly struct WinitEventArgs
{
    private const int KeyCodeNameCapacity = 64;

    internal unsafe WinitEventArgs(in WinitEvent evt)
    {
        Kind = evt.Kind;
        StartCause = evt.StartCause;
        Width = evt.Width;
        Height = evt.Height;
        ScaleFactor = evt.ScaleFactor;
        WindowHandle = evt.Window;
        MouseX = evt.MouseX;
        MouseY = evt.MouseY;
        DeltaX = evt.DeltaX;
        DeltaY = evt.DeltaY;
        Modifiers = (WinitModifiers)evt.Modifiers;
        MouseButton = evt.MouseButton;
        MouseButtonValue = evt.MouseButtonValue;
        ElementState = evt.ElementState;
        ScrollDeltaKind = evt.ScrollDeltaKind;
        KeyCode = evt.KeyCode;
        string? keyCodeName;
        fixed (byte* keyCodeNamePtr = evt.KeyCodeName)
        {
            keyCodeName = DecodeUtf8String(keyCodeNamePtr, KeyCodeNameCapacity);
        }
        KeyCodeName = keyCodeName;
        KeyLocation = evt.KeyLocation;
        Repeat = evt.Repeat;
        TouchId = evt.TouchId;
        TouchPhase = evt.TouchPhase;
        Text = evt.Text == nint.Zero ? null : Marshal.PtrToStringUTF8(evt.Text);
        AccessKitEventKind = evt.AccessKitEventKind;
        AccessKitActionJson = evt.AccessKitAction == nint.Zero ? null : Marshal.PtrToStringUTF8(evt.AccessKitAction);
    }

    public WinitEventKind Kind { get; }

    public WinitStartCause StartCause { get; }

    public uint Width { get; }

    public uint Height { get; }

    public double ScaleFactor { get; }

    public double MouseX { get; }

    public double MouseY { get; }

    public double DeltaX { get; }

    public double DeltaY { get; }

    public WinitModifiers Modifiers { get; }

    public WinitMouseButton MouseButton { get; }

    public uint MouseButtonValue { get; }

    public WinitElementState ElementState { get; }

    public WinitMouseScrollDeltaKind ScrollDeltaKind { get; }

    public uint KeyCode { get; }

    public string? KeyCodeName { get; }

    public WinitKeyLocation KeyLocation { get; }

    public bool Repeat { get; }

    public ulong TouchId { get; }

    public WinitTouchPhaseKind TouchPhase { get; }

    public string? Text { get; }

    public WinitAccessKitEventKind AccessKitEventKind { get; }

    public string? AccessKitActionJson { get; }

    internal nint WindowHandle { get; }

    public WinitWindow? TryGetWindow() => WindowHandle == nint.Zero ? null : new WinitWindow(WindowHandle);

    private static unsafe string? DecodeUtf8String(byte* bytes, int capacity)
    {
        if (bytes == null || capacity == 0)
        {
            return null;
        }

        var length = 0;
        while (length < capacity && bytes[length] != 0)
        {
            length++;
        }

        return length == 0 ? null : Encoding.UTF8.GetString(new ReadOnlySpan<byte>(bytes, length));
    }
}

public readonly struct WinitRunConfiguration
{
    public WinitRunConfiguration()
    {
        CreateWindow = true;
        Window = WinitWindowOptions.Default;
    }

    public bool CreateWindow { get; init; }

    public WinitWindowOptions Window { get; init; }

    internal WinitRunOptions ToNative(out nint titlePtr)
    {
        var descriptor = Window.ToNative(out titlePtr);
        return new WinitRunOptions
        {
            CreateWindow = CreateWindow,
            Window = descriptor,
        };
    }
}

public readonly struct WinitWindowOptions
{
    public WinitWindowOptions()
    {
        Resizable = true;
        Decorations = true;
        Transparent = false;
        Visible = true;
    }

    public static WinitWindowOptions Default => new();

    public uint? Width { get; init; }

    public uint? Height { get; init; }

    public uint? MinWidth { get; init; }

    public uint? MinHeight { get; init; }

    public uint? MaxWidth { get; init; }

    public uint? MaxHeight { get; init; }

    public bool Resizable { get; init; }

    public bool Decorations { get; init; }

    public bool Transparent { get; init; }

    public bool Visible { get; init; }

    public string? Title { get; init; }

    internal WinitWindowDescriptor ToNative(out nint titlePtr)
    {
        titlePtr = nint.Zero;
        var descriptor = new WinitWindowDescriptor
        {
            Width = Width ?? 0,
            Height = Height ?? 0,
            MinWidth = MinWidth ?? 0,
            MinHeight = MinHeight ?? 0,
            MaxWidth = MaxWidth ?? 0,
            MaxHeight = MaxHeight ?? 0,
            Resizable = Resizable,
            Decorations = Decorations,
            Transparent = Transparent,
            Visible = Visible,
            Title = nint.Zero,
        };

        if (!string.IsNullOrEmpty(Title))
        {
            titlePtr = NativeHelpers.AllocUtf8String(Title);
            descriptor.Title = titlePtr;
        }

        return descriptor;
    }
}
