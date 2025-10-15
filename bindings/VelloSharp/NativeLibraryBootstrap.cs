using System.Runtime.CompilerServices;

namespace VelloSharp;

internal static class NativeLibraryBootstrap
{
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize()
    {
        NativeLibraryLoader.RegisterResolverForAssembly(typeof(AccessKitNativeMethods).Assembly);
        NativeLibraryLoader.RegisterResolverForAssembly(typeof(SparseNativeMethods).Assembly);
        NativeLibraryLoader.RegisterNativeLibraries(
            "vello_ffi",
            "vello_sparse_ffi",
            "kurbo_ffi",
            "peniko_ffi",
            "accesskit_ffi",
            "winit_ffi",
            "vello_webgpu_ffi");
    }
}
