#if IOS
using Microsoft.Maui;
using Metal;
using VelloSharp.Maui.Controls;

namespace VelloSharp.Maui.Internal;

internal abstract partial class MauiVelloPresenterAdapter
{
    internal static partial MauiVelloPresenterAdapter? CreatePlatformAdapter(IVelloView view, IMauiContext? mauiContext)
    {
        return MTLDevice.SystemDefault is null
            ? null
            : new MauiVelloMetalPresenter(view, isMacCatalyst: false);
    }
}
#endif
