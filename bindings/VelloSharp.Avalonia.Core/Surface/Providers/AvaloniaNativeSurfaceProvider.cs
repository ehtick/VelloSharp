using System;
using System.Reflection;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.Winit;
using VelloSharp;

namespace VelloSharp.Avalonia.Core.Surface.Providers;

/// <summary>
/// Provides the surface details required by the Vello renderer for Avalonia.Native on macOS.
/// </summary>
public sealed class AvaloniaNativeSurfaceProvider : IVelloWinitSurfaceProvider
{
    private readonly ITopLevelImpl _topLevel;
    private readonly object _invalidateSync = new();
    private MethodInfo? _invalidateMethod;

    public AvaloniaNativeSurfaceProvider(ITopLevelImpl topLevel)
    {
        _topLevel = topLevel ?? throw new ArgumentNullException(nameof(topLevel));

        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException(
                "AvaloniaNativeSurfaceProvider can only be used on macOS.");
        }
    }

    public SurfaceHandle CreateSurfaceHandle()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return CreateSurfaceHandleCore();
        }

        return Dispatcher.UIThread
            .InvokeAsync(CreateSurfaceHandleCore, DispatcherPriority.Send)
            .GetAwaiter()
            .GetResult();
    }

    public PixelSize SurfacePixelSize => Dispatcher.UIThread.CheckAccess()
        ? GetCurrentPixelSize()
        : Dispatcher.UIThread
            .InvokeAsync(GetCurrentPixelSize, DispatcherPriority.Send)
            .GetAwaiter()
            .GetResult();

    public double RenderScaling => Dispatcher.UIThread.CheckAccess()
        ? _topLevel.RenderScaling
        : Dispatcher.UIThread
            .InvokeAsync(() => _topLevel.RenderScaling, DispatcherPriority.Send)
            .GetAwaiter()
            .GetResult();

    public void PrePresent()
    {
        // Avalonia.Native handles swapchain presentation internally.
    }

    public void RequestRedraw()
    {
        void InvokeInvalidate()
        {
            var invalidate = GetInvalidateMethod();
            invalidate?.Invoke(_topLevel, null);
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            InvokeInvalidate();
        }
        else
        {
            Dispatcher.UIThread.Post(InvokeInvalidate, DispatcherPriority.Render);
        }
    }

    private SurfaceHandle CreateSurfaceHandleCore()
    {
        var handle = _topLevel.Handle ?? throw new InvalidOperationException(
            "Avalonia Native toplevel handle is not available.");

        if (handle is not IMacOSTopLevelPlatformHandle macHandle)
        {
            var descriptor = string.IsNullOrEmpty(handle.HandleDescriptor)
                ? "<unknown>"
                : handle.HandleDescriptor;
            throw new NotSupportedException(
                $"Unsupported Avalonia Native handle descriptor '{descriptor}'.");
        }

        var nsView = macHandle.NSView;
        if (nsView == IntPtr.Zero)
        {
            throw new InvalidOperationException("Avalonia Native NSView handle is not available.");
        }

        return SurfaceHandle.FromAppKit(nsView);
    }

    private PixelSize GetCurrentPixelSize()
    {
        var clientSize = _topLevel.ClientSize;
        var scaling = _topLevel.RenderScaling;

        var width = Math.Max(1, (int)Math.Ceiling(clientSize.Width * scaling));
        var height = Math.Max(1, (int)Math.Ceiling(clientSize.Height * scaling));

        return new PixelSize(width, height);
    }

    private MethodInfo? GetInvalidateMethod()
    {
        if (_invalidateMethod is not null)
        {
            return _invalidateMethod;
        }

        lock (_invalidateSync)
        {
            _invalidateMethod ??= _topLevel
                .GetType()
                .GetMethod("Invalidate", BindingFlags.Instance | BindingFlags.Public);
        }

        return _invalidateMethod;
    }
}
