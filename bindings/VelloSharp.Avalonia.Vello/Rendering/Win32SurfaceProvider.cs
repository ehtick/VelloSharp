using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.Winit;
using VelloSharp;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal sealed class Win32SurfaceProvider : IVelloWinitSurfaceProvider
{
    private readonly INativePlatformHandleSurface _surface;

    public Win32SurfaceProvider(INativePlatformHandleSurface surface)
    {
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Win32SurfaceProvider requires Windows.");
        }
    }

    public SurfaceHandle CreateSurfaceHandle()
    {
        var hwnd = _surface.Handle;
        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("HWND is not available.");
        }

        var hinstance = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_HINSTANCE);
        if (hinstance == IntPtr.Zero)
        {
            hinstance = NativeMethods.GetModuleHandle(null);
        }

        return SurfaceHandle.FromWin32(hwnd, hinstance);
    }

    public PixelSize SurfacePixelSize => ClampSize(_surface.Size);

    public double RenderScaling => _surface.Scaling;

    public void PrePresent()
    {
    }

    public void RequestRedraw()
    {
        var hwnd = _surface.Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        void Invalidate()
        {
            NativeMethods.InvalidateRect(hwnd, IntPtr.Zero, false);
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Invalidate();
        }
        else
        {
            Dispatcher.UIThread.Post(Invalidate, DispatcherPriority.Render);
        }
    }

    private static PixelSize ClampSize(PixelSize size)
    {
        return new PixelSize(Math.Max(1, size.Width), Math.Max(1, size.Height));
    }

    private static class NativeMethods
    {
        internal const int GWL_HINSTANCE = -6;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        internal static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "InvalidateRect", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetModuleHandle(string? lpModuleName);
    }
}
