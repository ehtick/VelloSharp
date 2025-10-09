using System.Runtime.InteropServices;

namespace VelloSharp.Editor;

internal static partial class NativeMethods
{
    private const string NativeLibraryName = "vello_editor_core";

    [LibraryImport(NativeLibraryName)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool vello_editor_core_initialize();

    [LibraryImport(NativeLibraryName)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool vello_editor_core_is_initialized();
}
