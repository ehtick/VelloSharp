#if HAS_UNO

using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Controls;
using VelloSharp.Windows;

namespace VelloSharp.Uno.Interop;

internal sealed class UnoSwapChainPanelSurfaceSource : IWindowsSurfaceSource, IDisposable
{
    private readonly SwapChainPanel _panel;
    private IntPtr _swapChainPanelNative;
    private bool _disposed;
    private static readonly Guid SwapChainPanelNativeGuid = new("63AAD0B8-7C24-40FF-85A8-640D944CC325");

    public UnoSwapChainPanelSurfaceSource(SwapChainPanel panel)
    {
        _panel = panel ?? throw new ArgumentNullException(nameof(panel));
    }

    public WindowsSurfaceDescriptor GetSurfaceDescriptor()
    {
        var native = EnsureNativePointer();
        return native == IntPtr.Zero
            ? default
            : WindowsSurfaceDescriptor.FromSwapChainPanel(native);
    }

    public WindowsSurfaceSize GetSurfaceSize()
    {
        var scale = _panel.XamlRoot?.RasterizationScale ?? 1.0;
        var width = (uint)Math.Max(0, Math.Round(_panel.ActualWidth * scale));
        var height = (uint)Math.Max(0, Math.Round(_panel.ActualHeight * scale));
        if (width == 0 || height == 0)
        {
            return WindowsSurfaceSize.Empty;
        }

        return new WindowsSurfaceSize(width, height);
    }

    public string? DiagnosticsLabel => _panel.Name;

    public void OnSwapChainCreated(WindowsSwapChainSurface surface) { }

    public void OnSwapChainResized(WindowsSwapChainSurface surface, WindowsSurfaceSize size) { }

    public void OnSwapChainDestroyed() { }

    public void OnDeviceLost(string? reason) { }

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
        if (_swapChainPanelNative != IntPtr.Zero)
        {
            Marshal.Release(_swapChainPanelNative);
            _swapChainPanelNative = IntPtr.Zero;
        }
    }

    private IntPtr EnsureNativePointer()
    {
        if (_disposed)
        {
            return IntPtr.Zero;
        }

        if (_swapChainPanelNative != IntPtr.Zero)
        {
            return _swapChainPanelNative;
        }

        try
        {
            var unknown = Marshal.GetIUnknownForObject(_panel);
            try
            {
                var iid = SwapChainPanelNativeGuid;
                var hr = Marshal.QueryInterface(unknown, ref iid, out var native);
                if (hr >= 0 && native != IntPtr.Zero)
                {
                    _swapChainPanelNative = native;
                }
                else if (native != IntPtr.Zero)
                {
                    Marshal.Release(native);
                }
            }
            finally
            {
                Marshal.Release(unknown);
            }
        }
        catch
        {
            _swapChainPanelNative = IntPtr.Zero;
        }

        return _swapChainPanelNative;
    }
}

#endif
