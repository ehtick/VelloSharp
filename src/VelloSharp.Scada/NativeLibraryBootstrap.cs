using System.Runtime.CompilerServices;
using VelloSharp;

namespace VelloSharp.Scada;

#pragma warning disable CS0436
internal static class NativeLibraryBootstrap
{
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize()
    {
        NativeLibraryLoader.RegisterNativeLibrary("vello_scada_runtime");
    }
}
#pragma warning restore CS0436
