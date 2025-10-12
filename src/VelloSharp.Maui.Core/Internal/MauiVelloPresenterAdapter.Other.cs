#if !WINDOWS && !MACCATALYST && !IOS && !ANDROID
using VelloSharp.Maui.Controls;
using Microsoft.Maui;

namespace VelloSharp.Maui.Internal;

internal abstract partial class MauiVelloPresenterAdapter
{
    internal static partial MauiVelloPresenterAdapter? CreatePlatformAdapter(IVelloView view, IMauiContext? mauiContext)
        => null;
}
#endif
