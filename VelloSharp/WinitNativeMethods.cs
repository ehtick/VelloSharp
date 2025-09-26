using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VelloSharp;

internal static partial class WinitNativeMethods
{
    private const string LibraryName = "winit_ffi";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void WinitEventCallback(nint userData, nint context, ref WinitEvent evt);

    [LibraryImport(LibraryName, EntryPoint = "winit_last_error_message")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nint winit_last_error_message();

    [LibraryImport(LibraryName, EntryPoint = "winit_event_loop_run")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial WinitStatus winit_event_loop_run(ref WinitRunOptions options, WinitEventCallback callback, nint userData);

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
}
