using System;
using Avalonia.Platform;
using VelloSharp;

namespace Avalonia.Winit;

internal sealed class WinitWindowingPlatform : IWindowingPlatform
{
    private readonly WinitDispatcher _dispatcher;
    private readonly WinitScreenManager _screens;
    private readonly WinitWindowOptions _defaultOptions;

    public WinitWindowingPlatform(WinitDispatcher dispatcher, WinitScreenManager screens, WinitWindowOptions options)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _screens = screens ?? throw new ArgumentNullException(nameof(screens));
        _defaultOptions = options;
    }

    public IWindowImpl CreateWindow() => new WinitWindowImpl(_dispatcher, _screens, _defaultOptions);

    public ITopLevelImpl CreateEmbeddableTopLevel() => throw new PlatformNotSupportedException("Embeddable toplevels are not supported by the Winit backend.");

    public IWindowImpl CreateEmbeddableWindow() => throw new PlatformNotSupportedException("Embeddable windows are not supported by the Winit backend.");

    public ITrayIconImpl? CreateTrayIcon() => null;
}
