using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using VelloSharp;

namespace Avalonia.Winit;

internal sealed class WinitWindowImpl : IWindowImpl, INativePlatformHandleSurface, IVelloWinitSurfaceProvider
{
    private static readonly PlatformHandle s_nullPlatformHandle = new PlatformHandle(IntPtr.Zero, "Winit");
    private readonly WinitDispatcher _dispatcher;
    private readonly object _stateLock = new();
    private readonly object[] _surfaces;
    private readonly IMouseDevice _mouseDevice;
    private readonly IKeyboardDevice _keyboardDevice;
    private readonly WinitScreenManager _screenManager;

    private WinitWindow? _window;
    private nint _nativeHandle;

    internal string? Title => _title;
    private PlatformHandle? _platformHandle;
    private VelloWindowHandle _cachedVelloHandle;
    private bool _hasCachedVelloHandle;
    private Size _clientSize = new Size(1, 1);
    private PixelSize _surfaceSize = new PixelSize(1, 1);
    private double _renderScaling = 1.0;
    private double _desktopScaling = 1.0;
    private bool _isVisible;
    private WindowState _windowState = WindowState.Normal;
    private IInputRoot? _inputRoot;
    private RawInputModifiers _modifiers;
    private WindowTransparencyLevel _transparencyLevel = WindowTransparencyLevel.None;
    private IReadOnlyList<WindowTransparencyLevel>? _transparencyHint;
    private PlatformThemeVariant _themeVariant = PlatformThemeVariant.Light;
    private Point _pointerPosition = new Point(0, 0);
    private bool _isClosing;
    private RawInputModifiers _keyboardModifiers;
    private RawInputModifiers _mouseButtonModifiers;
    private readonly object _loopActionLock = new();
    private List<Action>? _pendingLoopActions;
    private bool _windowReady;
    private WinitWindowImpl? _parentWindow;
    private bool _isDialogWindow;
    private bool _canMinimize = true;
    private bool _canMaximize = true;
    private bool _isEnabled = true;
    private bool _isTopmost;
    private bool _isResizable = true;
    private string? _title;
    private readonly WinitAccessKitManager _accessKit;

    public WinitWindowImpl(WinitDispatcher dispatcher, WinitScreenManager screenManager, WinitWindowOptions options)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _screenManager = screenManager ?? throw new ArgumentNullException(nameof(screenManager));
        _surfaces = new object[] { this };
        _accessKit = new WinitAccessKitManager(this, _dispatcher);
        _title = options.Title;
        _mouseDevice = AvaloniaLocator.Current.GetService<IMouseDevice>() ?? new MouseDevice();
        _keyboardDevice = AvaloniaLocator.Current.GetService<IKeyboardDevice>() ?? new KeyboardDevice();
        InitializeWindow(options);
        WinitWindowZOrderTracker.Register(this);
    }

    public void Dispose()
    {
        _accessKit.Dispose();
        DestroyNativeWindow();
    }

    public Size? FrameSize => null;

    public void Show(bool activate, bool isDialog)
    {
        _isDialogWindow = isDialog;
        if (_isClosing)
        {
            return;
        }

        _isVisible = true;
        InvokeOnLoop(() => _window?.SetVisible(true));

        if (activate)
        {
            Activate();
        }
    }

    public void Hide()
    {
        if (_isClosing)
        {
            return;
        }

        _isVisible = false;
        InvokeOnLoop(() => _window?.SetVisible(false));
    }

    public PixelPoint Position => default;

    public Action<PixelPoint>? PositionChanged { get; set; }

    public IScreenImpl Screen => _screenManager;

    public void Activate()
    {
        if (_isClosing)
        {
            return;
        }

        InvokeOnLoop(() => _window?.Focus());
    }

    public Action? Deactivated { get; set; }

    public Action? Activated { get; set; }

    public WindowState WindowState
    {
        get => _windowState;
        set
        {
            if (_windowState == value)
            {
                return;
            }

            _windowState = value;
            ApplyWindowState();
            WindowStateChanged?.Invoke(value);
        }
    }

    public Action<WindowState>? WindowStateChanged { get; set; }

    public void SetTitle(string? title)
    {
        if (_isClosing)
        {
            return;
        }

        _title = title;
        InvokeOnLoop(() => _window?.SetTitle(title ?? string.Empty));
    }

    public void SetParent(IWindowImpl? parent)
    {
        if (ReferenceEquals(parent, this))
        {
            parent = null;
        }

        _parentWindow = parent as WinitWindowImpl;

        if (_isClosing)
        {
            return;
        }

        InvokeOnLoop(UpdateParentRelationship);
    }

    public void SetEnabled(bool enable)
    {
        _isEnabled = enable;

        if (_isClosing)
        {
            return;
        }

        InvokeOnLoop(() => _window?.SetEnabled(enable));
    }

    public Action? GotInputWhenDisabled { get; set; }

    public void SetSystemDecorations(SystemDecorations enabled)
    {
        if (_isClosing)
        {
            return;
        }

        InvokeOnLoop(() => _window?.SetDecorations(enabled != SystemDecorations.None));
    }

    public void SetIcon(IWindowIconImpl? icon)
    {
        if (_isClosing)
        {
            return;
        }

        byte[]? iconData = null;

        if (icon is WinitIconImpl winitIcon)
        {
            iconData = winitIcon.GetBytes();
        }
        else if (icon is not null)
        {
            using var stream = new MemoryStream();
            icon.Save(stream);
            iconData = stream.ToArray();
        }

        var payload = iconData;
        InvokeOnLoop(() =>
        {
            var window = _window;
            if (window is null)
            {
                return;
            }

            if (payload is { Length: > 0 })
            {
                window.SetWindowIcon(payload);
            }
            else
            {
                window.ClearWindowIcon();
            }
        });
    }

    public void ShowTaskbarIcon(bool value)
    {
        if (_isClosing)
        {
            return;
        }

        InvokeOnLoop(() => _window?.SetSkipTaskbar(!value));
    }

    public void CanResize(bool value)
    {
        _isResizable = value;

        if (_isClosing)
        {
            return;
        }

        InvokeOnLoop(() => _window?.SetResizable(value));
        ApplyWindowButtons();
    }

    public void SetCanMinimize(bool value)
    {
        if (_canMinimize == value)
        {
            return;
        }

        _canMinimize = value;
        ApplyWindowButtons();
    }

    public void SetCanMaximize(bool value)
    {
        if (_canMaximize == value)
        {
            return;
        }

        _canMaximize = value;
        ApplyWindowButtons();
    }

    public Func<WindowCloseReason, bool>? Closing { get; set; }

    public bool IsClientAreaExtendedToDecorations => false;

    public Action<bool>? ExtendClientAreaToDecorationsChanged { get; set; }

    public bool NeedsManagedDecorations => false;

    public Thickness ExtendedMargins => new Thickness(0);

    public Thickness OffScreenMargin => new Thickness(0);

    public void BeginMoveDrag(PointerPressedEventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        var point = e.GetCurrentPoint(null);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Pointer.Capture(null);

        InvokeOnLoop(() =>
        {
            var window = _window;
            if (window is null)
            {
                return;
            }

            var status = window.TryBeginMoveDrag();
            if (status == WinitStatus.Unsupported)
            {
                // Platform does not support programmatic move drags.
            }
        });
    }

    public void BeginResizeDrag(WindowEdge edge, PointerPressedEventArgs e)
    {
        if (_isClosing || !_isResizable)
        {
            return;
        }

        if (!TryGetResizeDirection(edge, out var direction))
        {
            return;
        }

        var point = e.GetCurrentPoint(null);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Pointer.Capture(null);

        InvokeOnLoop(() =>
        {
            var window = _window;
            if (window is null)
            {
                return;
            }

            var status = window.TryBeginResizeDrag(direction);
            if (status == WinitStatus.Unsupported)
            {
                // Resize drags are unsupported on this platform.
            }
        });
    }

    public void Resize(Size clientSize, WindowResizeReason reason = WindowResizeReason.Application)
    {
        var scale = EffectiveScaling;
        var widthValue = double.IsNaN(clientSize.Width) ? 0 : clientSize.Width * scale;
        var heightValue = double.IsNaN(clientSize.Height) ? 0 : clientSize.Height * scale;
        var width = (uint)Math.Max(1, Math.Round(Math.Max(widthValue, 1)));
        var height = (uint)Math.Max(1, Math.Round(Math.Max(heightValue, 1)));
        if (_isClosing)
        {
            return;
        }

        InvokeOnLoop(() => _window?.SetInnerSize(width, height));
    }

    public void Move(PixelPoint point)
    {
        if (_isClosing)
        {
            return;
        }

        InvokeOnLoop(() => _window?.SetOuterPosition(point.X, point.Y));
    }

    public void SetMinMaxSize(Size minSize, Size maxSize)
    {
        var minWidth = ConvertMinSize(minSize.Width);
        var minHeight = ConvertMinSize(minSize.Height);
        var maxWidth = ConvertMaxSize(maxSize.Width);
        var maxHeight = ConvertMaxSize(maxSize.Height);

        if (_isClosing)
        {
            return;
        }

        InvokeOnLoop(() =>
        {
            _window?.SetMinInnerSize(minWidth, minHeight);
            _window?.SetMaxInnerSize(maxWidth, maxHeight);
        });
    }

    public void SetExtendClientAreaToDecorationsHint(bool extendIntoClientAreaHint)
    {
    }

    public void SetExtendClientAreaChromeHints(ExtendClientAreaChromeHints hints)
    {
    }

    public void SetExtendClientAreaTitleBarHeightHint(double titleBarHeight)
    {
    }


    void IWindowImpl.GetWindowsZOrder(Span<Window> windows, Span<long> zOrder)
    {
        WinitWindowZOrderTracker.FillZOrder(windows, zOrder);
    }

    public Size MaxAutoSizeHint => Size.Infinity;

    public void SetTopmost(bool value)
    {
        _isTopmost = value;

        if (_isClosing)
        {
            return;
        }

        var level = value ? WinitWindowLevel.AlwaysOnTop : WinitWindowLevel.Normal;
        InvokeOnLoop(() => _window?.SetWindowLevel(level));
    }

    public double DesktopScaling => _desktopScaling;

    public IPlatformHandle Handle => _platformHandle ?? s_nullPlatformHandle;

    public Size ClientSize => _clientSize;

    public double RenderScaling => _renderScaling;

    public IEnumerable<object> Surfaces => _surfaces;

    public Action<RawInputEventArgs>? Input { get; set; }

    public Action<Rect>? Paint { get; set; }

    public Action<Size, WindowResizeReason>? Resized { get; set; }

    public Action<double>? ScalingChanged { get; set; }

    public Action<WindowTransparencyLevel>? TransparencyLevelChanged { get; set; }

    public Compositor Compositor => WinitPlatform.Compositor;

    public void SetInputRoot(IInputRoot inputRoot)
    {
        _inputRoot = inputRoot;
        AttachAutomationPeer(inputRoot);
    }

    private void AttachAutomationPeer(IInputRoot inputRoot)
    {
        if (inputRoot is Control control)
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(control);
            _accessKit.AttachAutomationPeer(peer as WindowAutomationPeer);
        }
        else
        {
            _accessKit.AttachAutomationPeer(null);
        }
    }

    public Point PointToClient(PixelPoint point)
    {
        var scale = EffectiveScaling;
        return new Point(point.X / scale, point.Y / scale);
    }

    public PixelPoint PointToScreen(Point point)
    {
        return PixelPoint.FromPoint(point, EffectiveScaling);
    }

    public void SetCursor(ICursorImpl? cursor)
    {
        if (_isClosing)
        {
            return;
        }

        if (cursor is WinitCursorFactory.WinitCursorImpl winitCursor)
        {
            var icon = winitCursor.Icon;
            var hidden = winitCursor.IsHidden;

            InvokeOnLoop(() =>
            {
                var window = _window;
                if (window is null)
                {
                    return;
                }

                if (hidden)
                {
                    window.SetCursorVisible(false);
                }
                else
                {
                    window.SetCursor(icon);
                    window.SetCursorVisible(true);
                }
            });
        }
        else
        {
            InvokeOnLoop(() =>
            {
                var window = _window;
                if (window is null)
                {
                    return;
                }

                window.SetCursor(WinitCursorIcon.Default);
                window.SetCursorVisible(true);
            });
        }
    }

    public Action? Closed { get; set; }

    public Action? LostFocus { get; set; }

    public IPopupImpl? CreatePopup() => null;

    public void SetTransparencyLevelHint(IReadOnlyList<WindowTransparencyLevel> transparencyLevels)
    {
        _transparencyHint = transparencyLevels;
    }

    public WindowTransparencyLevel TransparencyLevel => _transparencyLevel;

    public AcrylicPlatformCompensationLevels AcrylicCompensationLevels => default;

    public void SetFrameThemeVariant(PlatformThemeVariant themeVariant)
    {
        _themeVariant = themeVariant;
    }

    public object? TryGetFeature(Type featureType)
    {
        if (featureType == typeof(IScreenImpl))
        {
            return _screenManager;
        }

        if (featureType == typeof(INativePlatformHandleSurface))
        {
            return this;
        }

        if (featureType == typeof(IVelloWinitSurfaceProvider))
        {
            return this;
        }

        return null;
    }

    PixelSize INativePlatformHandleSurface.Size => _surfaceSize;

    double INativePlatformHandleSurface.Scaling => _renderScaling;

    SurfaceHandle IVelloWinitSurfaceProvider.CreateSurfaceHandle()
    {
        if (_window is null)
        {
            throw new InvalidOperationException("Native window handle is not available.");
        }

        if (_dispatcher.CurrentThreadIsLoopThread)
        {
            var loopHandle = _window.GetVelloWindowHandle();
            CacheVelloHandle(loopHandle);
            return SurfaceHandle.FromVelloHandle(loopHandle);
        }

        if (Volatile.Read(ref _hasCachedVelloHandle) && _cachedVelloHandle.Kind != VelloWindowHandleKind.None)
        {
            return SurfaceHandle.FromVelloHandle(_cachedVelloHandle);
        }

        VelloWindowHandle? result = null;
        Exception? capturedException = null;

        using (var completion = new ManualResetEventSlim(false))
        {
            _dispatcher.Post(context =>
            {
                try
                {
                    var loopWindow = _window;
                    if (loopWindow is null)
                    {
                        capturedException = new InvalidOperationException("Native window handle is not available.");
                        return;
                    }

                    result = loopWindow.GetVelloWindowHandle();
                    if (result.HasValue)
                    {
                        CacheVelloHandle(result.Value);
                    }
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                }
                finally
                {
                    completion.Set();
                }
            });

            completion.Wait();
        }

        if (capturedException is not null)
        {
            ExceptionDispatchInfo.Capture(capturedException).Throw();
        }

        if (!result.HasValue)
        {
            throw new InvalidOperationException("Failed to acquire a native window handle for the Vello surface.");
        }

        return SurfaceHandle.FromVelloHandle(result.Value);
    }

    PixelSize IVelloWinitSurfaceProvider.SurfacePixelSize => _surfaceSize;

    double IVelloWinitSurfaceProvider.RenderScaling => EffectiveScaling;

    void IVelloWinitSurfaceProvider.PrePresent()
    {
        if (_window is null)
        {
            return;
        }

        InvokeOnLoop(() => _window?.PrePresentNotify());
    }

    void IVelloWinitSurfaceProvider.RequestRedraw()
    {
        if (_window is null)
        {
            return;
        }

        InvokeOnLoop(() => _window?.RequestRedraw());
    }

    private void InitializeWindow(WinitWindowOptions options)
    {
        WinitStatus CreateWindowCore(WinitEventLoopContext context)
        {
            var descriptor = options.ToNative(out var titlePtr);
            descriptor.Visible = false;
            try
            {
                var result = WinitNativeMethods.winit_context_create_window(context.Handle, ref descriptor, out var handle);
                if (result == WinitStatus.Success && handle != nint.Zero)
                {
                    _window = new WinitWindow(handle);
                    _dispatcher.RegisterWindow(handle, this);
                    _nativeHandle = handle;
                    UpdatePlatformHandle();
                    _isVisible = options.Visible;
                }

                return result;
            }
            finally
            {
                if (titlePtr != nint.Zero)
                {
                    unsafe
                    {
                        NativeMemory.Free((void*)titlePtr);
                    }
                }
            }
        }

        void EnsureCreated(WinitStatus result)
        {
            NativeHelpers.ThrowOnError(result, "winit_context_create_window");

            if (_window is null)
            {
                throw new InvalidOperationException("Failed to initialize winit window.");
            }
        }

        if (_dispatcher.TryGetCurrentContext(out var currentContext))
        {
            var immediateStatus = CreateWindowCore(currentContext);
            EnsureCreated(immediateStatus);
            return;
        }

        var waitForCompletion = !_dispatcher.RunsOnMainThread || _dispatcher.IsLoopRunning;
        ManualResetEventSlim? completion = waitForCompletion ? new ManualResetEventSlim(false) : null;
        var status = WinitStatus.Success;

        _dispatcher.Post(context =>
        {
            try
            {
                status = CreateWindowCore(context);

                if (!waitForCompletion)
                {
                    EnsureCreated(status);
                }
            }
            finally
            {
                completion?.Set();
            }
        });

        if (waitForCompletion)
        {
            completion!.Wait();
            completion.Dispose();

            EnsureCreated(status);
        }
    }

    private void MarkWindowReady()
    {
        List<Action>? pending = null;
        lock (_loopActionLock)
        {
            if (_windowReady)
            {
                return;
            }

            _windowReady = true;
            if (_pendingLoopActions is { Count: > 0 })
            {
                pending = _pendingLoopActions;
                _pendingLoopActions = null;
            }
        }

        if (pending is null)
        {
            ApplyWindowButtons();
            return;
        }

        foreach (var action in pending)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"WinitWindowImpl pending loop action failed: {ex}");
            }
        }

        ApplyWindowButtons();
    }

    private void DestroyNativeWindow(WinitEventLoopContext? context = null)
    {
        var handle = _nativeHandle;
        if (_isClosing && handle == nint.Zero)
        {
            return;
        }

        if (handle == nint.Zero)
        {
            return;
        }

        _isClosing = true;

        void DestroyCore(WinitEventLoopContext ctx)
        {
            NativeHelpers.ThrowOnError(WinitNativeMethods.winit_context_destroy_window(ctx.Handle, handle), "winit_context_destroy_window");
            CompleteDestroy();
        }

        if (context.HasValue)
        {
            DestroyCore(context.Value);
            return;
        }

        if (_dispatcher.CurrentThreadIsLoopThread)
        {
            _dispatcher.Post(ctx =>
            {
                DestroyCore(ctx);
            });
            return;
        }

        using var completion = new ManualResetEventSlim(false);
        _dispatcher.Post(ctx =>
        {
            try
            {
                DestroyCore(ctx);
            }
            finally
            {
                completion.Set();
            }
        });

        completion.Wait();
    }

    private void CompleteDestroy()
    {
        var handle = _nativeHandle;
        if (handle == nint.Zero)
        {
            return;
        }

        _dispatcher.UnregisterWindow(handle);
        _nativeHandle = nint.Zero;
        _window = null;
        _platformHandle = null;
        _cachedVelloHandle = default;
        Volatile.Write(ref _hasCachedVelloHandle, false);
        lock (_loopActionLock)
        {
            _windowReady = false;
            _pendingLoopActions = null;
        }
        _parentWindow = null;
        _isClosing = false;
        WinitWindowZOrderTracker.Unregister(this);
        Closed?.Invoke();
    }

    private void ApplyWindowState()
    {
        if (_isClosing)
        {
            return;
        }

        InvokeOnLoop(() =>
        {
            if (_window is null)
            {
                return;
            }

            switch (_windowState)
            {
                case WindowState.Normal:
                    _window.SetMinimized(false);
                    _window.SetMaximized(false);
                    break;
                case WindowState.Minimized:
                    _window.SetMinimized(true);
                    break;
                case WindowState.Maximized:
                    _window.SetMinimized(false);
                    _window.SetMaximized(true);
                    break;
                case WindowState.FullScreen:
                    _window.SetMinimized(false);
                    _window.SetMaximized(true);
                    break;
            }
        });
    }

    private void InvokeOnLoop(Action action)
    {
        if (_dispatcher.CurrentThreadIsLoopThread)
        {
            ExecuteOrQueueLoopAction(action);
        }
        else
        {
            _dispatcher.Post(() => ExecuteOrQueueLoopAction(action));
        }
    }

    private void UpdateParentRelationship()
    {
        var handle = _nativeHandle;
        if (handle == nint.Zero)
        {
            return;
        }

        var parentHandle = _parentWindow?._nativeHandle ?? nint.Zero;
        if (handle == parentHandle)
        {
            parentHandle = nint.Zero;
        }

        NativeHelpers.ThrowOnError(
            WinitNativeMethods.winit_window_set_owner(handle, parentHandle),
            "winit_window_set_owner");
    }

    private void ApplyWindowButtons()
    {
        if (_isClosing)
        {
            return;
        }

        InvokeOnLoop(() =>
        {
            var window = _window;
            if (window is null)
            {
                return;
            }

            var buttons = WinitWindowButtons.Close;

            if (_canMinimize)
            {
                buttons |= WinitWindowButtons.Minimize;
            }

            if (_canMaximize && _isResizable)
            {
                buttons |= WinitWindowButtons.Maximize;
            }

            window.SetEnabledButtons(buttons);
        });
    }

    private static bool TryGetResizeDirection(WindowEdge edge, out WinitResizeDirection direction)
    {
        direction = edge switch
        {
            WindowEdge.NorthWest => WinitResizeDirection.NorthWest,
            WindowEdge.North => WinitResizeDirection.North,
            WindowEdge.NorthEast => WinitResizeDirection.NorthEast,
            WindowEdge.West => WinitResizeDirection.West,
            WindowEdge.East => WinitResizeDirection.East,
            WindowEdge.SouthWest => WinitResizeDirection.SouthWest,
            WindowEdge.South => WinitResizeDirection.South,
            WindowEdge.SouthEast => WinitResizeDirection.SouthEast,
            _ => default,
        };

        return edge is WindowEdge.NorthWest or WindowEdge.North or WindowEdge.NorthEast or WindowEdge.West or WindowEdge.East or WindowEdge.SouthWest or WindowEdge.South or WindowEdge.SouthEast;
    }

    private void ExecuteOrQueueLoopAction(Action action)
    {
        if (_windowReady)
        {
            action();
            return;
        }

        lock (_loopActionLock)
        {
            if (_windowReady)
            {
                action();
                return;
            }

            _pendingLoopActions ??= new List<Action>();
            _pendingLoopActions.Add(action);
        }
    }

    private void UpdatePlatformHandle()
    {
        if (_window is null)
        {
            _platformHandle = null;
            return;
        }

        var velloHandle = _window.GetVelloWindowHandle();
        CacheVelloHandle(velloHandle);
        _platformHandle = CreatePlatformHandle(in velloHandle);
    }

    private void CacheVelloHandle(VelloWindowHandle handle)
    {
        if (handle.Kind == VelloWindowHandleKind.None)
        {
            return;
        }

        _cachedVelloHandle = handle;
        Volatile.Write(ref _hasCachedVelloHandle, true);
    }

    private static PlatformHandle CreatePlatformHandle(in VelloWindowHandle handle)
    {
        return handle.Kind switch
        {
            VelloWindowHandleKind.Win32 => new PlatformHandle((nint)handle.Payload.Win32.Hwnd, "HWND"),
            VelloWindowHandleKind.AppKit => new PlatformHandle(handle.Payload.AppKit.NsView, "NSView"),
            VelloWindowHandleKind.Wayland => new PlatformHandle(handle.Payload.Wayland.Surface, "WaylandSurface"),
            VelloWindowHandleKind.Xlib => new PlatformHandle((nint)handle.Payload.Xlib.Window, "X11Window"),
            _ => new PlatformHandle(IntPtr.Zero, "Winit"),
        };
    }

    internal void OnWindowCreated(WinitEventLoopContext context, uint width, uint height, double scale)
    {
        _surfaceSize = CreateSurfaceSize(width, height);
        var normalizedScale = NormalizeScale(scale);
        _renderScaling = normalizedScale;
        _desktopScaling = normalizedScale;
        _clientSize = CreateClientSize(_surfaceSize, normalizedScale);
        Resized?.Invoke(_clientSize, WindowResizeReason.Layout);
        ScalingChanged?.Invoke(normalizedScale);
        _screenManager.UpdateFromWindow(_surfaceSize, normalizedScale);

        if (_window is { } window)
        {
            var accessKitInitialized = _accessKit.InitializeNative(context, window);
            if (accessKitInitialized)
            {
                _accessKit.InvalidateTree();
            }
            window.SetVisible(_isVisible);
        }

        MarkWindowReady();
    }

    internal void OnWindowResized(uint width, uint height, double scale)
    {
        _surfaceSize = CreateSurfaceSize(width, height);
        _clientSize = CreateClientSize(_surfaceSize, EffectiveScaling);
        Resized?.Invoke(_clientSize, WindowResizeReason.Layout);
        _screenManager.UpdateFromWindow(_surfaceSize, EffectiveScaling);
    }

    internal void OnScaleFactorChanged(double scale)
    {
        var normalizedScale = NormalizeScale(scale);
        if (AreClose(_renderScaling, normalizedScale))
        {
            return;
        }

        _renderScaling = normalizedScale;
        _desktopScaling = normalizedScale;
        _clientSize = CreateClientSize(_surfaceSize, normalizedScale);
        ScalingChanged?.Invoke(normalizedScale);
        Resized?.Invoke(_clientSize, WindowResizeReason.Layout);
        _screenManager.UpdateFromWindow(_surfaceSize, normalizedScale);
    }

    internal void OnRedrawRequested()
    {
        Dispatcher.UIThread.RunJobs(DispatcherPriority.UiThreadRender);
        Paint?.Invoke(new Rect(_clientSize));
    }

    internal void OnCloseRequested(WinitEventLoopContext context)
    {
        var cancel = Closing?.Invoke(WindowCloseReason.WindowClosing) ?? false;
        if (cancel)
        {
            return;
        }
        DestroyNativeWindow(context);
    }

    internal void OnDestroyed()
    {
        CompleteDestroy();
    }

    internal void OnActivated()
    {
        WinitWindowZOrderTracker.NotifyActivated(this);
        Activated?.Invoke();
    }

    internal void OnDeactivated()
    {
        ClearMouseButtonModifiers();
        Deactivated?.Invoke();
        LostFocus?.Invoke();
    }

    internal void OnCursorMoved(double x, double y, WinitModifiers modifiers)
    {
        var mods = ConvertModifiers(modifiers);
        var position = CreatePointerPosition(x, y);
        _pointerPosition = position;
        var evt = CreatePointerEvent(RawPointerEventType.Move, position, mods);
        if (evt is not null)
        {
            Input?.Invoke(evt);
            _mouseDevice?.ProcessRawEvent(evt);
        }
    }

    internal void OnCursorEntered()
    {
        var evt = CreatePointerEvent(RawPointerEventType.Move, _pointerPosition, _modifiers);
        if (evt is not null)
        {
            Input?.Invoke(evt);
            _mouseDevice?.ProcessRawEvent(evt);
        }
    }

    internal void OnCursorLeft()
    {
        var evt = CreatePointerEvent(RawPointerEventType.LeaveWindow, _pointerPosition, _modifiers);
        if (evt is not null)
        {
            Input?.Invoke(evt);
            _mouseDevice?.ProcessRawEvent(evt);
        }
    }

    internal void OnMouseInput(WinitMouseButton button, WinitElementState state, WinitModifiers modifiers)
    {
        UpdateMouseButtonModifiers(button, state);
        var mods = ConvertModifiers(modifiers);
        var type = button switch
        {
            WinitMouseButton.Left => state == WinitElementState.Pressed ? RawPointerEventType.LeftButtonDown : RawPointerEventType.LeftButtonUp,
            WinitMouseButton.Right => state == WinitElementState.Pressed ? RawPointerEventType.RightButtonDown : RawPointerEventType.RightButtonUp,
            WinitMouseButton.Middle => state == WinitElementState.Pressed ? RawPointerEventType.MiddleButtonDown : RawPointerEventType.MiddleButtonUp,
            WinitMouseButton.Back => state == WinitElementState.Pressed ? RawPointerEventType.XButton1Down : RawPointerEventType.XButton1Up,
            WinitMouseButton.Forward => state == WinitElementState.Pressed ? RawPointerEventType.XButton2Down : RawPointerEventType.XButton2Up,
            _ => state == WinitElementState.Pressed ? RawPointerEventType.Move : RawPointerEventType.Move,
        };

        var evt = CreatePointerEvent(type, _pointerPosition, mods);
        if (evt is not null)
        {
            Input?.Invoke(evt);
            _mouseDevice?.ProcessRawEvent(evt);
        }
    }

    internal void OnMouseWheel(double deltaX, double deltaY, WinitMouseScrollDeltaKind kind, WinitModifiers modifiers)
    {
        var mods = ConvertModifiers(modifiers);
        var root = _inputRoot;
        if (root is null)
        {
            return;
        }

        var position = _pointerPosition;
        var delta = kind == WinitMouseScrollDeltaKind.LineDelta
            ? new Vector(deltaX * 120, deltaY * 120)
            : new Vector(deltaX / EffectiveScaling, deltaY / EffectiveScaling);
        var args = new RawMouseWheelEventArgs(_mouseDevice, (ulong)Now, root, position, delta, mods);
        Input?.Invoke(args);
        _mouseDevice?.ProcessRawEvent(args);
    }

    internal void OnKeyboardInput(uint keyCode, string? keyCodeName, WinitElementState state, WinitModifiers modifiers, WinitKeyLocation location, bool repeat, string? text)
    {
        var mods = ConvertModifiers(modifiers);
        var root = _inputRoot;
        if (root is null)
        {
            return;
        }

        var keyboardDevice = _keyboardDevice ?? throw new InvalidOperationException("Keyboard device is not available.");
        var physical = WinitKeyCodeMapper.MapPhysicalKey(keyCode, keyCodeName);
        var key = physical.ToQwertyKey();
        var args = new RawKeyEventArgs(
            keyboardDevice,
            (ulong)Now,
            root,
            state == WinitElementState.Pressed ? RawKeyEventType.KeyDown : RawKeyEventType.KeyUp,
            key,
            mods,
            physical,
            keySymbol: null)
        {
            KeyDeviceType = KeyDeviceType.Keyboard
        };

        Input?.Invoke(args);
        keyboardDevice.ProcessRawEvent(args);

        if (state == WinitElementState.Pressed && !string.IsNullOrEmpty(text))
        {
            var textArgs = new RawTextInputEventArgs(
                keyboardDevice,
                (ulong)Now,
                root,
                text!);
            Input?.Invoke(textArgs);
            keyboardDevice.ProcessRawEvent(textArgs);
        }
    }

    internal void OnModifiersChanged(WinitModifiers modifiers)
    {
        ConvertModifiers(modifiers);
    }

    internal void OnTouch(WinitEventArgs args)
    {
    }

    internal void OnAccessKitEvent(WinitEventArgs args)
    {
        _accessKit.HandleAccessKitEvent(args);
    }

    internal void OnTextInput(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var root = _inputRoot;
        if (root is null)
        {
            return;
        }

        var keyboardDevice = _keyboardDevice ?? throw new InvalidOperationException("Keyboard device is not available.");
        var textArgs = new RawTextInputEventArgs(
            keyboardDevice,
            (ulong)Now,
            root,
            text);
        Input?.Invoke(textArgs);
        keyboardDevice.ProcessRawEvent(textArgs);
    }

    private RawPointerEventArgs? CreatePointerEvent(RawPointerEventType type, Point position, RawInputModifiers modifiers)
    {
        if (_inputRoot is not IInputRoot root)
        {
            return null;
        }

        return new RawPointerEventArgs(_mouseDevice, (ulong)Now, root, type, position, modifiers);
    }

    private void UpdateMouseButtonModifiers(WinitMouseButton button, WinitElementState state)
    {
        var flag = button switch
        {
            WinitMouseButton.Left => RawInputModifiers.LeftMouseButton,
            WinitMouseButton.Right => RawInputModifiers.RightMouseButton,
            WinitMouseButton.Middle => RawInputModifiers.MiddleMouseButton,
            WinitMouseButton.Back => RawInputModifiers.XButton1MouseButton,
            WinitMouseButton.Forward => RawInputModifiers.XButton2MouseButton,
            _ => RawInputModifiers.None,
        };

        if (flag == RawInputModifiers.None)
        {
            return;
        }

        if (state == WinitElementState.Pressed)
        {
            _mouseButtonModifiers |= flag;
        }
        else
        {
            _mouseButtonModifiers &= ~flag;
        }

        UpdateModifiers();
    }

    private void ClearMouseButtonModifiers()
    {
        if (_mouseButtonModifiers == RawInputModifiers.None)
        {
            return;
        }

        _mouseButtonModifiers = RawInputModifiers.None;
        UpdateModifiers();
    }

    private void UpdateModifiers()
    {
        _modifiers = _keyboardModifiers | _mouseButtonModifiers;
    }

    private RawInputModifiers ConvertModifiers(WinitModifiers modifiers)
    {
        var result = RawInputModifiers.None;
        if ((modifiers & WinitModifiers.Shift) != 0)
        {
            result |= RawInputModifiers.Shift;
        }

        if ((modifiers & WinitModifiers.Control) != 0)
        {
            result |= RawInputModifiers.Control;
        }

        if ((modifiers & WinitModifiers.Alt) != 0)
        {
            result |= RawInputModifiers.Alt;
        }

        if ((modifiers & WinitModifiers.Meta) != 0)
        {
            result |= RawInputModifiers.Meta;
        }

        _keyboardModifiers = result;
        UpdateModifiers();
        return _modifiers;
    }

    private Point CreatePointerPosition(double x, double y)
    {
        var scale = EffectiveScaling;
        return new Point(x / scale, y / scale);
    }

    private double EffectiveScaling => _renderScaling > 0 ? _renderScaling : 1.0;

    private static double NormalizeScale(double scale) => scale > 0 ? scale : 1.0;

    private static bool AreClose(double a, double b) => Math.Abs(a - b) < 0.0001;

    private static PixelSize CreateSurfaceSize(uint width, uint height)
    {
        return new PixelSize(ClampDimension(width), ClampDimension(height));
    }

    private static Size CreateClientSize(PixelSize surfaceSize, double scale)
    {
        var effectiveScale = scale > 0 ? scale : 1.0;
        return new Size(surfaceSize.Width / effectiveScale, surfaceSize.Height / effectiveScale);
    }

    private static int ClampDimension(uint value)
    {
        if (value == 0)
        {
            return 1;
        }

        return value >= int.MaxValue ? int.MaxValue : (int)value;
    }

    private uint ConvertMinSize(double value)
    {
        if (double.IsNaN(value) || value <= 0)
        {
            return 0;
        }

        var scaled = Math.Round(value * EffectiveScaling);
        if (scaled <= 0)
        {
            return 1;
        }

        if (scaled >= uint.MaxValue)
        {
            return uint.MaxValue;
        }

        return (uint)scaled;
    }

    private uint ConvertMaxSize(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            return 0;
        }

        var scaled = Math.Round(value * EffectiveScaling);
        if (scaled <= 0)
        {
            return 0;
        }

        if (scaled >= uint.MaxValue)
        {
            return uint.MaxValue;
        }

        return (uint)scaled;
    }

    private long Now => _dispatcher.Now;

    nint IPlatformHandle.Handle => _platformHandle?.Handle ?? IntPtr.Zero;

    string? IPlatformHandle.HandleDescriptor => _platformHandle?.HandleDescriptor;
}

internal static class WinitWindowZOrderTracker
{
    private static readonly object s_lock = new();
    private static readonly Dictionary<WinitWindowImpl, long> s_orders = new();
    private static long s_nextOrder;

    public static void Register(WinitWindowImpl window)
    {
        if (window is null)
        {
            throw new ArgumentNullException(nameof(window));
        }

        lock (s_lock)
        {
            if (!s_orders.ContainsKey(window))
            {
                s_orders[window] = ++s_nextOrder;
            }
        }
    }

    public static void Unregister(WinitWindowImpl window)
    {
        if (window is null)
        {
            throw new ArgumentNullException(nameof(window));
        }

        lock (s_lock)
        {
            s_orders.Remove(window);
        }
    }

    public static void NotifyActivated(WinitWindowImpl window)
    {
        if (window is null)
        {
            throw new ArgumentNullException(nameof(window));
        }

        lock (s_lock)
        {
            s_orders[window] = ++s_nextOrder;
        }
    }

    public static void FillZOrder(Span<Window> windows, Span<long> zOrder)
    {
        lock (s_lock)
        {
            for (var i = 0; i < windows.Length; i++)
            {
                if (windows[i].PlatformImpl is WinitWindowImpl impl && s_orders.TryGetValue(impl, out var order))
                {
                    zOrder[i] = order;
                }
                else
                {
                    zOrder[i] = 0;
                }
            }
        }
    }
}
