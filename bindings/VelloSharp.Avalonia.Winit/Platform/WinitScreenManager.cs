using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform;

namespace Avalonia.Winit;

internal sealed class WinitScreenManager : IScreenImpl
{
    private Screen[] _screens;
    private PixelSize _lastSize;
    private double _lastScale = 1.0;

    public WinitScreenManager()
    {
        var bounds = new PixelRect(0, 0, 1920, 1080);
        var screen = new Screen(1.0, bounds, bounds, true);
        _screens = new[] { screen };
        _lastSize = new PixelSize(bounds.Width, bounds.Height);
    }

    public int ScreenCount => SnapshotScreens().Length;

    public IReadOnlyList<Screen> AllScreens => SnapshotScreens();

    public Action? Changed { get; set; }

    public Screen? ScreenFromWindow(IWindowBaseImpl window)
    {
        var screens = SnapshotScreens();
        return screens.Length > 0 ? screens[0] : null;
    }

    public Screen? ScreenFromTopLevel(ITopLevelImpl topLevel)
    {
        var screens = SnapshotScreens();
        return screens.Length > 0 ? screens[0] : null;
    }

    public Screen? ScreenFromPoint(PixelPoint point)
    {
        var screens = SnapshotScreens();
        return screens.Length > 0 ? screens[0] : null;
    }

    public Screen? ScreenFromRect(PixelRect rect)
    {
        var screens = SnapshotScreens();
        return screens.Length > 0 ? screens[0] : null;
    }

    public Task<bool> RequestScreenDetails() => Task.FromResult(true);

    public void UpdateFromWindow(PixelSize surfaceSize, double scale)
    {
        var clampedWidth = Math.Max(surfaceSize.Width, 1);
        var clampedHeight = Math.Max(surfaceSize.Height, 1);
        var size = new PixelSize(clampedWidth, clampedHeight);

        if (_lastSize == size && Math.Abs(scale - _lastScale) < double.Epsilon)
        {
            return;
        }

        var bounds = new PixelRect(0, 0, size.Width, size.Height);
        var screen = new Screen(scale, bounds, bounds, true);

        Volatile.Write(ref _screens, new[] { screen });
        _lastSize = size;
        _lastScale = scale;

        Changed?.Invoke();
    }

    private Screen[] SnapshotScreens()
    {
        return Volatile.Read(ref _screens);
    }
}
