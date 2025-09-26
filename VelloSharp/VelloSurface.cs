using System;

namespace VelloSharp;

public sealed class VelloSurfaceContext : IDisposable
{
    private IntPtr _handle;

    public VelloSurfaceContext()
    {
        _handle = NativeMethods.vello_render_context_create();
        if (_handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create render context.");
        }
    }

    internal IntPtr Handle
    {
        get
        {
            if (_handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(VelloSurfaceContext));
            }
            return _handle;
        }
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_render_context_destroy(_handle);
            _handle = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }
    }

    ~VelloSurfaceContext()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_render_context_destroy(_handle);
        }
    }
}

public sealed class VelloSurface : IDisposable
{
    private readonly VelloSurfaceContext? _context;
    private IntPtr _handle;

    public VelloSurface(VelloSurfaceContext? context, in SurfaceDescriptor descriptor)
    {
        _context = context;
        var nativeDescriptor = descriptor.ToNative();
        _handle = NativeMethods.vello_render_surface_create(context?.Handle ?? IntPtr.Zero, nativeDescriptor);
        if (_handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create render surface.");
        }
    }

    internal IntPtr Handle
    {
        get
        {
            if (_handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(VelloSurface));
            }
            return _handle;
        }
    }

    public void Resize(uint width, uint height)
    {
        var status = NativeMethods.vello_render_surface_resize(Handle, width, height);
        NativeHelpers.ThrowOnError(status, "Surface resize failed");
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_render_surface_destroy(_handle);
            _handle = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }
    }

    ~VelloSurface()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_render_surface_destroy(_handle);
        }
    }
}

public sealed class VelloSurfaceRenderer : IDisposable
{
    private IntPtr _handle;

    public VelloSurfaceRenderer(VelloSurface surface, RendererOptions? options = null)
    {
        if (surface is null)
        {
            throw new ArgumentNullException(nameof(surface));
        }

        var nativeOptions = (options ?? new RendererOptions()).ToNative();
        _handle = NativeMethods.vello_surface_renderer_create(surface.Handle, nativeOptions);
        if (_handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create surface renderer.");
        }
    }

    internal IntPtr Handle
    {
        get
        {
            if (_handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(VelloSurfaceRenderer));
            }
            return _handle;
        }
    }

    public void Render(VelloSurface surface, Scene scene, RenderParams renderParams)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(scene);

        var nativeParams = new VelloRenderParams
        {
            Width = renderParams.Width,
            Height = renderParams.Height,
            BaseColor = renderParams.BaseColor.ToNative(),
            Antialiasing = (VelloAaMode)renderParams.Antialiasing,
            Format = (VelloRenderFormat)renderParams.Format,
        };

        var status = NativeMethods.vello_surface_renderer_render(
            Handle,
            surface.Handle,
            scene.Handle,
            nativeParams);
        NativeHelpers.ThrowOnError(status, "Surface render failed");
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_surface_renderer_destroy(_handle);
            _handle = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }
    }

    ~VelloSurfaceRenderer()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_surface_renderer_destroy(_handle);
        }
    }
}
public readonly struct SurfaceHandle
{
    private readonly VelloWindowHandle _handle;

    private SurfaceHandle(VelloWindowHandle handle)
    {
        _handle = handle;
    }

    internal VelloWindowHandle ToNative() => _handle;

    public static SurfaceHandle Headless => new(new VelloWindowHandle
    {
        Kind = VelloWindowHandleKind.Headless,
    });

    public static SurfaceHandle FromVelloHandle(VelloWindowHandle handle)
    {
        if (handle.Kind == VelloWindowHandleKind.None)
        {
            throw new ArgumentException("Window handle must not be None.", nameof(handle));
        }

        return new SurfaceHandle(handle);
    }

    public static SurfaceHandle FromWin32(IntPtr hwnd, IntPtr hinstance = default)
    {
        if (hwnd == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(hwnd));
        }

        return new SurfaceHandle(new VelloWindowHandle
        {
            Kind = VelloWindowHandleKind.Win32,
            Payload = new VelloWindowHandlePayload
            {
                Win32 = new VelloWin32WindowHandle
                {
                    Hwnd = hwnd,
                    HInstance = hinstance,
                },
            },
        });
    }

    public static SurfaceHandle FromAppKit(IntPtr nsView)
    {
        if (nsView == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(nsView));
        }

        return new SurfaceHandle(new VelloWindowHandle
        {
            Kind = VelloWindowHandleKind.AppKit,
            Payload = new VelloWindowHandlePayload
            {
                AppKit = new VelloAppKitWindowHandle
                {
                    NsView = nsView,
                },
            },
        });
    }

    public static SurfaceHandle FromWayland(IntPtr surface, IntPtr display)
    {
        if (surface == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(surface));
        }
        if (display == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(display));
        }

        return new SurfaceHandle(new VelloWindowHandle
        {
            Kind = VelloWindowHandleKind.Wayland,
            Payload = new VelloWindowHandlePayload
            {
                Wayland = new VelloWaylandWindowHandle
                {
                    Surface = surface,
                    Display = display,
                },
            },
        });
    }

    public static SurfaceHandle FromXlib(ulong window, IntPtr display, int screen, ulong visualId)
    {
        if (window == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(window));
        }

        return new SurfaceHandle(new VelloWindowHandle
        {
            Kind = VelloWindowHandleKind.Xlib,
            Payload = new VelloWindowHandlePayload
            {
                Xlib = new VelloXlibWindowHandle
                {
                    Window = window,
                    Display = display,
                    Screen = screen,
                    VisualId = visualId,
                },
            },
        });
    }
}

public readonly struct SurfaceDescriptor
{
    public uint Width { get; init; }
    public uint Height { get; init; }
    public PresentMode PresentMode { get; init; }
    public SurfaceHandle Handle { get; init; }

    internal VelloSurfaceDescriptor ToNative()
    {
        if (Width == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), "Width must be positive.");
        }
        if (Height == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Height), "Height must be positive.");
        }

        var nativeHandle = Handle.ToNative();
        if (nativeHandle.Kind == VelloWindowHandleKind.None)
        {
            throw new InvalidOperationException("Surface handle must be specified.");
        }

        return new VelloSurfaceDescriptor
        {
            Width = Width,
            Height = Height,
            PresentMode = (VelloPresentMode)PresentMode,
            Handle = nativeHandle,
        };
    }
}
