using System.Runtime.CompilerServices;

namespace VelloSharp;

internal static class NativeLibraryBootstrap
{
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize()
    {
        NativeLibraryLoader.RegisterNativeLibraries(
            "vello_ffi",
            "vello_sparse_ffi",
            "kurbo_ffi",
            "peniko_ffi");
    }
}
