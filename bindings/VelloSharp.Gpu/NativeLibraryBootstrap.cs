using System.Runtime.CompilerServices;

namespace VelloSharp;

internal static class GpuNativeLibraryBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        NativeLibraryLoader.RegisterNativeLibrary("accesskit_ffi");
        NativeLibraryLoader.RegisterNativeLibrary("winit_ffi");
    }
}
