using Microsoft.Maui.Handlers;
using VelloSharp.Composition.Input;
using VelloSharp.Maui.Controls;
using VelloSharp.Maui.Input;
using VelloSharp.Maui.Internal;
#if WINDOWS
using PlatformView = Microsoft.UI.Xaml.FrameworkElement;
#elif ANDROID
using PlatformView = Android.Views.View;
#elif IOS || MACCATALYST
using PlatformView = UIKit.UIView;
#else
using PlatformView = System.Object;
#endif

namespace VelloSharp.Maui;

public partial class VelloViewHandler : ViewHandler<IVelloView, PlatformView>, IVelloViewHandler
{
    public static readonly PropertyMapper<IVelloView, VelloViewHandler> Mapper = new(ViewMapper)
    {
        [nameof(IVelloView.DeviceOptions)] = MapDeviceOptions,
        [nameof(IVelloView.PreferredBackend)] = MapPreferredBackend,
        [nameof(IVelloView.RenderMode)] = MapRenderMode,
        [nameof(IVelloView.RenderLoopDriver)] = MapRenderLoopDriver,
        [nameof(IVelloView.IsDiagnosticsEnabled)] = MapDiagnosticsEnabled,
        [nameof(IVelloView.UseTextureView)] = MapUseTextureView,
        [nameof(IVelloView.SuppressGraphicsViewCompositor)] = MapSuppressGraphicsViewCompositor,
    };

    public static readonly CommandMapper<IVelloView, VelloViewHandler> CommandMapper = new(ViewCommandMapper)
    {
        [nameof(IVelloView.InvalidateSurface)] = OnInvalidateSurface,
    };

    private MauiVelloPresenterAdapter? _presenter;
    private MauiCompositionInputSource? _inputSource;

    internal ICompositionInputSource? InputSource => _inputSource;

    ICompositionInputSource? IVelloViewHandler.CompositionInputSource => _inputSource;

    public VelloViewHandler()
        : base(Mapper, CommandMapper)
    {
    }

    public VelloViewHandler(PropertyMapper? mapper, CommandMapper? commands)
        : base(mapper ?? Mapper, commands ?? CommandMapper)
    {
    }

    protected override PlatformView CreatePlatformView()
    {
        var platformView = CreatePlatformViewCore();
        _presenter = MauiVelloPresenterAdapter.Create(VirtualView, MauiContext);
        _presenter.Attach(platformView);
        InitializePlatformView(platformView);
        _inputSource = new MauiCompositionInputSource(VirtualView, platformView);
        return platformView;
    }

    protected override void DisconnectHandler(PlatformView platformView)
    {
        _inputSource?.Dispose();
        _inputSource = null;
        _presenter?.Detach();
        _presenter?.Dispose();
        _presenter = null;
        TeardownPlatformView(platformView);
        base.DisconnectHandler(platformView);
    }

    private static void MapDeviceOptions(VelloViewHandler handler, IVelloView view)
        => handler._presenter?.OnDeviceOptionsChanged();

    private static void MapPreferredBackend(VelloViewHandler handler, IVelloView view)
        => handler._presenter?.OnPreferredBackendChanged();

    private static void MapRenderMode(VelloViewHandler handler, IVelloView view)
        => handler._presenter?.OnRenderModeChanged();

    private static void MapRenderLoopDriver(VelloViewHandler handler, IVelloView view)
        => handler._presenter?.OnRenderLoopDriverChanged();

    private static void MapDiagnosticsEnabled(VelloViewHandler handler, IVelloView view)
        => handler._presenter?.OnDiagnosticsToggled();

    private static void OnInvalidateSurface(VelloViewHandler handler, IVelloView view, object? arg)
        => handler._presenter?.RequestRender();

    private static void MapUseTextureView(VelloViewHandler handler, IVelloView view)
        => handler._presenter?.OnSurfaceConfigurationChanged();

    private static void MapSuppressGraphicsViewCompositor(VelloViewHandler handler, IVelloView view)
        => handler._presenter?.OnGraphicsViewSuppressionChanged();

    partial void InitializePlatformView(PlatformView platformView);

    partial void TeardownPlatformView(PlatformView platformView);

    protected partial PlatformView CreatePlatformViewCore();
}
