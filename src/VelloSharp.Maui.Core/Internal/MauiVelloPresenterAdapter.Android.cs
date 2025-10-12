#if ANDROID
using Microsoft.Maui;
using VelloSharp.Maui.Controls;

namespace VelloSharp.Maui.Internal;

internal abstract partial class MauiVelloPresenterAdapter
{
    internal static partial MauiVelloPresenterAdapter? CreatePlatformAdapter(IVelloView view, IMauiContext? mauiContext)
        => new MauiVelloAndroidPresenter(view);
}
#endif
