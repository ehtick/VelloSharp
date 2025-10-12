#if MACCATALYST
using System;
using Metal;
using UIKit;
using VelloSharp.Maui.Internal;
using PlatformView = UIKit.UIView;

namespace VelloSharp.Maui;

public partial class VelloViewHandler
{
    protected partial PlatformView CreatePlatformViewCore()
    {
        var device = MTLDevice.SystemDefault ?? throw new InvalidOperationException("Metal is not supported on this device.");
        return new MauiMetalView(device, isMacCatalyst: true)
        {
            BackgroundColor = UIColor.Clear,
        };
    }

    partial void InitializePlatformView(PlatformView platformView)
    {
    }

    partial void TeardownPlatformView(PlatformView platformView)
    {
    }
}
#endif
