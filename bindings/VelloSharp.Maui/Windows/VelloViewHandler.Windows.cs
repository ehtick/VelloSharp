#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VelloSharp.Uno.Controls;
using PlatformView = Microsoft.UI.Xaml.FrameworkElement;

namespace VelloSharp.Maui;

public partial class VelloViewHandler
{
    protected partial PlatformView CreatePlatformViewCore()
        => new VelloSwapChainPanel
        {
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch,
        };

    partial void InitializePlatformView(PlatformView platformView)
    {
    }

    partial void TeardownPlatformView(PlatformView platformView)
    {
    }
}
#endif
