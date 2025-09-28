using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VelloSharp;

internal static unsafe partial class WinitNativeMethods
{
    private const string LibraryName = "winit_ffi";

    [LibraryImport(LibraryName, EntryPoint = "winit_last_error_message")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nint winit_last_error_message();

    [LibraryImport(LibraryName, EntryPoint = "winit_event_loop_run")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_event_loop_run(ref WinitRunOptions options, delegate* unmanaged[Cdecl]<nint, nint, WinitEvent*, void> callback, nint userData);

    [LibraryImport(LibraryName, EntryPoint = "winit_context_set_control_flow")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_context_set_control_flow(nint context, WinitControlFlow flow, long waitMillis);

    [LibraryImport(LibraryName, EntryPoint = "winit_context_exit")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_context_exit(nint context);

    [LibraryImport(LibraryName, EntryPoint = "winit_context_is_exiting")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_context_is_exiting(nint context, [MarshalAs(UnmanagedType.I1)] out bool exiting);

    [LibraryImport(LibraryName, EntryPoint = "winit_context_get_window")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_context_get_window(nint context, out nint window);

    [LibraryImport(LibraryName, EntryPoint = "winit_window_request_redraw")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_window_request_redraw(nint window);

    [LibraryImport(LibraryName, EntryPoint = "winit_window_pre_present_notify")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_window_pre_present_notify(nint window);

    [LibraryImport(LibraryName, EntryPoint = "winit_window_surface_size")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_window_surface_size(nint window, out uint width, out uint height);

    [LibraryImport(LibraryName, EntryPoint = "winit_window_scale_factor")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_window_scale_factor(nint window, out double scale);

    [LibraryImport(LibraryName, EntryPoint = "winit_window_id")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_window_id(nint window, out ulong id);

    [LibraryImport(LibraryName, EntryPoint = "winit_window_set_title", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_window_set_title(nint window, string title);

    [LibraryImport(LibraryName, EntryPoint = "winit_window_get_vello_handle")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_window_get_vello_handle(nint window, out VelloWindowHandle handle);

    [LibraryImport(LibraryName, EntryPoint = "winit_event_loop_wake")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_event_loop_wake();

    [LibraryImport(LibraryName, EntryPoint = "winit_context_create_window")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_context_create_window(nint context, ref WinitWindowDescriptor descriptor, out nint window);

    [LibraryImport(LibraryName, EntryPoint = "winit_context_destroy_window")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_context_destroy_window(nint context, nint window);

    [LibraryImport(LibraryName, EntryPoint = "winit_window_set_inner_size")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_window_set_inner_size(nint window, uint width, uint height);

    [LibraryImport(LibraryName, EntryPoint = "winit_window_set_min_inner_size")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_window_set_min_inner_size(nint window, uint width, uint height);

    [LibraryImport(LibraryName, EntryPoint = "winit_window_set_max_inner_size")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_window_set_max_inner_size(nint window, uint width, uint height);

    [LibraryImport(LibraryName, EntryPoint = "winit_window_set_visible")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_window_set_visible(nint window, [MarshalAs(UnmanagedType.I1)] bool visible);

    [LibraryImport(LibraryName, EntryPoint = "winit_window_set_resizable")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_window_set_resizable(nint window, [MarshalAs(UnmanagedType.I1)] bool resizable);

    [LibraryImport(LibraryName, EntryPoint = "winit_window_set_decorations")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_window_set_decorations(nint window, [MarshalAs(UnmanagedType.I1)] bool decorations);

    [LibraryImport(LibraryName, EntryPoint = "winit_window_set_minimized")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_window_set_minimized(nint window, [MarshalAs(UnmanagedType.I1)] bool minimized);

    [LibraryImport(LibraryName, EntryPoint = "winit_window_set_maximized")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_window_set_maximized(nint window, [MarshalAs(UnmanagedType.I1)] bool maximized);

    [LibraryImport(LibraryName, EntryPoint = "winit_window_focus")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_window_focus(nint window);

    [LibraryImport(LibraryName, EntryPoint = "winit_clipboard_is_available")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool winit_clipboard_is_available();

    [LibraryImport(LibraryName, EntryPoint = "winit_clipboard_set_text")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_clipboard_set_text(nint text);

    [LibraryImport(LibraryName, EntryPoint = "winit_clipboard_get_text")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_clipboard_get_text(out nint text);

    [LibraryImport(LibraryName, EntryPoint = "winit_clipboard_free_text")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void winit_clipboard_free_text(nint text);
}
