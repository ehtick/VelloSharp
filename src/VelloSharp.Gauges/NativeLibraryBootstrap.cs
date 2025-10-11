using System.Runtime.CompilerServices;
using VelloSharp;

namespace VelloSharp.Gauges;

#pragma warning disable CS0436
internal static class NativeLibraryBootstrap
{
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize()
    {
        NativeLibraryLoader.RegisterNativeLibrary("vello_gauges_core");
    }
}
#pragma warning restore CS0436
