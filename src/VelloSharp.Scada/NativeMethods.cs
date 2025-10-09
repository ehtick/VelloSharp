using System.Runtime.InteropServices;

namespace VelloSharp.Scada;

internal static partial class NativeMethods
{
    private const string NativeLibraryName = "vello_scada_runtime";

    [LibraryImport(NativeLibraryName)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool vello_scada_runtime_initialize();

    [LibraryImport(NativeLibraryName)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool vello_scada_runtime_is_initialized();
}
