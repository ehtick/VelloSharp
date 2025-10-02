using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VelloSharp;

internal static partial class AccessKitNativeMethods
{
    private const string LibraryName = "accesskit_ffi";

    [LibraryImport(LibraryName, EntryPoint = "accesskit_last_error_message")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nint accesskit_last_error_message();

    [LibraryImport(LibraryName, EntryPoint = "accesskit_string_free")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void accesskit_string_free(nint value);

    [LibraryImport(LibraryName, EntryPoint = "accesskit_tree_update_from_json", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial AccessKitStatus accesskit_tree_update_from_json(string json, out nint handle);

    [LibraryImport(LibraryName, EntryPoint = "accesskit_tree_update_clone")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial AccessKitStatus accesskit_tree_update_clone(nint handle, out nint clone);

    [LibraryImport(LibraryName, EntryPoint = "accesskit_tree_update_to_json")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial AccessKitStatus accesskit_tree_update_to_json(nint handle, out nint json);

    [LibraryImport(LibraryName, EntryPoint = "accesskit_tree_update_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void accesskit_tree_update_destroy(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "accesskit_action_request_from_json", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial AccessKitStatus accesskit_action_request_from_json(string json, out nint handle);

    [LibraryImport(LibraryName, EntryPoint = "accesskit_action_request_to_json")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial AccessKitStatus accesskit_action_request_to_json(nint handle, out nint json);

    [LibraryImport(LibraryName, EntryPoint = "accesskit_action_request_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void accesskit_action_request_destroy(nint handle);
}
