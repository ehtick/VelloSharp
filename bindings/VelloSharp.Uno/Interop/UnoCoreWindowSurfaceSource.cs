#if HAS_UNO

using System;
using System.Runtime.InteropServices;
using Windows.Graphics.Display;
using Windows.UI.Core;
using VelloSharp.Windows;

namespace VelloSharp.Uno.Interop;

internal sealed class UnoCoreWindowSurfaceSource : IWindowsSurfaceSource, IDisposable
{
    private static readonly Guid IInspectableGuid = new("AF86E2E0-B12D-4C6A-9C5A-D7AA65101E90");

    private readonly CoreWindow _coreWindow;
    private IntPtr _coreWindowInspectable;
    private bool _disposed;

    public UnoCoreWindowSurfaceSource(CoreWindow coreWindow)
    {
        _coreWindow = coreWindow ?? throw new ArgumentNullException(nameof(coreWindow));
    }

    public WindowsSurfaceDescriptor GetSurfaceDescriptor()
    {
        var pointer = EnsureNativePointer();
        return pointer == IntPtr.Zero ? default : WindowsSurfaceDescriptor.FromCoreWindow(pointer);
    }

    public WindowsSurfaceSize GetSurfaceSize()
    {
        if (_disposed)
        {
            return WindowsSurfaceSize.Empty;
        }

        var bounds = _coreWindow.Bounds;
        var scale = GetScale();
        var width = (uint)Math.Max(0, Math.Round(bounds.Width * scale));
        var height = (uint)Math.Max(0, Math.Round(bounds.Height * scale));
        if (width == 0 || height == 0)
        {
            return WindowsSurfaceSize.Empty;
        }

        return new WindowsSurfaceSize(width, height);
    }

    public string? DiagnosticsLabel => _coreWindow?.Dispatcher?.ToString();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ReleaseNativePointer();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public void ReleaseNativePointer()
    {
        if (_coreWindowInspectable != IntPtr.Zero)
        {
            Marshal.Release(_coreWindowInspectable);
            _coreWindowInspectable = IntPtr.Zero;
        }
    }

    public void OnSwapChainCreated(WindowsSwapChainSurface surface) { }

    public void OnSwapChainResized(WindowsSwapChainSurface surface, WindowsSurfaceSize size) { }

    public void OnSwapChainDestroyed() { }

    public void OnDeviceLost(string? reason) { }

    private IntPtr EnsureNativePointer()
    {
        if (_disposed)
        {
            return IntPtr.Zero;
        }

        if (_coreWindowInspectable != IntPtr.Zero)
        {
            return _coreWindowInspectable;
        }

        try
        {
            var unknown = Marshal.GetIUnknownForObject(_coreWindow);
            try
            {
                var iid = IInspectableGuid;
                var hr = Marshal.QueryInterface(unknown, ref iid, out var inspectable);
                if (hr >= 0 && inspectable != IntPtr.Zero)
                {
                    _coreWindowInspectable = inspectable;
                }
                else if (inspectable != IntPtr.Zero)
                {
                    Marshal.Release(inspectable);
                }
            }
            finally
            {
                Marshal.Release(unknown);
            }
        }
        catch
        {
            _coreWindowInspectable = IntPtr.Zero;
        }

        return _coreWindowInspectable;
    }

    private static double GetScale()
    {
        try
        {
            var display = DisplayInformation.GetForCurrentView();
            return display?.RawPixelsPerViewPixel ?? 1.0;
        }
        catch
        {
            return 1.0;
        }
    }
}

#endif

