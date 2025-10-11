using System.Runtime.CompilerServices;
using VelloSharp;

namespace VelloSharp.Editor;

#pragma warning disable CS0436
internal static class NativeLibraryBootstrap
{
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize()
    {
        NativeLibraryLoader.RegisterNativeLibrary("vello_editor_core");
    }
}
#pragma warning restore CS0436
