using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.Winit;
using VelloSharp;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal sealed class X11SurfaceProvider : IVelloWinitSurfaceProvider
{
    private readonly INativePlatformHandleSurface _surface;
    private readonly IntPtr _display;
    private readonly int _screen;
    private readonly ulong _visualId;

    private X11SurfaceProvider(
        INativePlatformHandleSurface surface,
        IntPtr display,
        int screen,
        ulong visualId)
    {
        _surface = surface;
        _display = display;
        _screen = screen;
        _visualId = visualId;
    }

    public static X11SurfaceProvider? TryCreate(INativePlatformHandleSurface surface)
    {
        if (surface is null)
        {
            throw new ArgumentNullException(nameof(surface));
        }

        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        var descriptor = surface.HandleDescriptor;
        if (!string.Equals(descriptor, "XID", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (surface.Handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            if (!TryResolveX11Context(surface, out var display, out var screen, out var visualId))
            {
                return null;
            }

            return new X11SurfaceProvider(surface, display, screen, visualId);
        }
        catch
        {
            return null;
        }
    }

    public SurfaceHandle CreateSurfaceHandle()
    {
        if (_display == IntPtr.Zero || _visualId == 0)
        {
            throw new InvalidOperationException("X11 display or visual information is missing.");
        }

        var window = _surface.Handle;
        if (window == IntPtr.Zero)
        {
            throw new InvalidOperationException("X11 window handle is not available.");
        }

        var windowId = unchecked((ulong)window.ToInt64());
        return SurfaceHandle.FromXlib(windowId, _display, _screen, _visualId);
    }

    public PixelSize SurfacePixelSize => ClampSize(_surface.Size);

    public double RenderScaling => _surface.Scaling;

    public void PrePresent()
    {
    }

    public void RequestRedraw()
    {
        if (_display == IntPtr.Zero)
        {
            return;
        }

        var window = _surface.Handle;
        if (window == IntPtr.Zero)
        {
            return;
        }

        void Invalidate()
        {
            X11NativeMethods.XClearArea(_display, window, 0, 0, 0, 0, true);
            X11NativeMethods.XFlush(_display);
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

    private static bool TryResolveX11Context(
        INativePlatformHandleSurface surface,
        out IntPtr display,
        out int screen,
        out ulong visualId)
    {
        display = IntPtr.Zero;
        screen = 0;
        visualId = 0;

        var ownerField = surface.GetType().GetField("_owner", BindingFlags.NonPublic | BindingFlags.Instance);
        var owner = ownerField?.GetValue(surface);
        if (owner is null)
        {
            return false;
        }

        var x11Field = owner.GetType().GetField("_x11", BindingFlags.NonPublic | BindingFlags.Instance);
        var x11Info = x11Field?.GetValue(owner);
        if (x11Info is null)
        {
            return false;
        }

        var displayProperty = x11Info.GetType().GetProperty("Display", BindingFlags.Public | BindingFlags.Instance);
        var screenProperty = x11Info.GetType().GetProperty("DefaultScreen", BindingFlags.Public | BindingFlags.Instance);

        if (displayProperty?.GetValue(x11Info) is not IntPtr rawDisplay || rawDisplay == IntPtr.Zero)
        {
            return false;
        }

        display = rawDisplay;
        screen = screenProperty is null ? 0 : (int)screenProperty.GetValue(x11Info)!;

        if (!X11NativeMethods.TryGetVisualId(display, surface.Handle, out visualId))
        {
            return false;
        }

        return true;
    }

    private static PixelSize ClampSize(PixelSize size)
    {
        return new PixelSize(Math.Max(1, size.Width), Math.Max(1, size.Height));
    }

    private static class X11NativeMethods
    {
        [DllImport("libX11" )]
        private static extern int XGetWindowAttributes(IntPtr display, IntPtr window, out XWindowAttributes attributes);

        [DllImport("libX11" )]
        private static extern IntPtr XVisualIDFromVisual(IntPtr visual);

        [DllImport("libX11" )]
        internal static extern int XClearArea(IntPtr display, IntPtr window, int x, int y, uint width, uint height, bool exposures);

        [DllImport("libX11" )]
        internal static extern int XFlush(IntPtr display);

        internal static bool TryGetVisualId(IntPtr display, IntPtr window, out ulong visualId)
        {
            visualId = 0;

            if (display == IntPtr.Zero || window == IntPtr.Zero)
            {
                return false;
            }

            if (XGetWindowAttributes(display, window, out var attributes) == 0)
            {
                return false;
            }

            if (attributes.visual == IntPtr.Zero)
            {
                return false;
            }

            var visualPtr = XVisualIDFromVisual(attributes.visual);
            if (visualPtr == IntPtr.Zero)
            {
                return false;
            }

            visualId = unchecked((ulong)visualPtr.ToInt64());
            return true;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XWindowAttributes
        {
            internal int X;
            internal int Y;
            internal int Width;
            internal int Height;
            internal int BorderWidth;
            internal int Depth;
            internal IntPtr visual;
            internal IntPtr Root;
            internal int Class;
            internal int BitGravity;
            internal int WinGravity;
            internal int BackingStore;
            internal IntPtr BackingPlanes;
            internal IntPtr BackingPixel;
            internal int SaveUnder;
            internal IntPtr Colourmap;
            internal int MapInstalled;
            internal int MapState;
            internal IntPtr AllEventMasks;
            internal IntPtr YourEventMask;
            internal IntPtr DoNotPropagateMask;
            internal int OverrideDirect;
            internal IntPtr Screen;
        }
    }
}
