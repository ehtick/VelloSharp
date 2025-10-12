using System;
using Android.Content;
using VelloSharp.Maui.Internal;
using PlatformView = Android.Views.View;

namespace VelloSharp.Maui;

public partial class VelloViewHandler
{
    protected partial PlatformView CreatePlatformViewCore()
    {
        var context = Context ?? throw new InvalidOperationException("MAUI context not initialized.");
        return VirtualView.UseTextureView
            ? new MauiVelloTextureView(context)
            : new MauiVelloNativeSurfaceView(context);
    }

    partial void InitializePlatformView(PlatformView platformView)
    {
    }

    partial void TeardownPlatformView(PlatformView platformView)
    {
    }

    private Context? Context => MauiContext?.Context;
}
