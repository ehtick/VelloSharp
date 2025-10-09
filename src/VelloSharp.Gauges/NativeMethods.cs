using System.Runtime.InteropServices;

namespace VelloSharp.Gauges;

internal static partial class NativeMethods
{
    private const string NativeLibraryName = "vello_gauges_core";

    [LibraryImport(NativeLibraryName)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool vello_gauges_initialize();

    [LibraryImport(NativeLibraryName)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool vello_gauges_is_initialized();
}
