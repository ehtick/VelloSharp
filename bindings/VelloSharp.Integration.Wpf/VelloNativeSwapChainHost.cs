using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using VelloSharp.Windows;

namespace VelloSharp.Wpf.Integration;

public class VelloNativeSwapChainHost : HwndHost, IWindowsSurfaceSource
{
    private const string WindowClassName = "VelloNativeSwapChainHostWindow";
    private const int WM_DPICHANGED = 0x02E0;
    private const int WM_SIZE = 0x0005;

    private static readonly object ClassSync = new();
    private static bool _isClassRegistered;

    private nint _hwnd;
    private WindowsGpuContextLease? _gpuLease;
    private WindowsSwapChainSurface? _swapChain;
    private bool _isLoaded;
    private RenderLoopDriver _currentDriver = RenderLoopDriver.None;
    private bool _renderLoopEnabled;
    private EventHandler? _compositionRenderingHandler;
    private EventHandler? _threadIdleHandler;

    private VelloGraphicsDeviceOptions _deviceOptions = VelloGraphicsDeviceOptions.Default;

    public event EventHandler<SwapChainLeaseEventArgs>? SwapChainReady;

    public VelloGraphicsDeviceOptions DeviceOptions
    {
        get => _deviceOptions;
        set => _deviceOptions = value ?? throw new ArgumentNullException(nameof(value));
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        EnsureWindowClass();

        var hwnd = NativeMethods.CreateWindowEx(
            dwExStyle: 0,
            lpClassName: WindowClassName,
            lpWindowName: string.Empty,
            dwStyle: NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE,
            x: 0,
            y: 0,
            nWidth: 0,
            nHeight: 0,
            hWndParent: hwndParent.Handle,
            hMenu: IntPtr.Zero,
            hInstance: NativeMethods.GetModuleHandle(null),
            lpParam: IntPtr.Zero);

        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create swap chain host window.");
        }

        _hwnd = hwnd;
        return new HandleRef(this, hwnd);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        ConfigureRenderLoop(RenderLoopDriver.None, enable: false);
        ResetGpuResources();

        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
        MessageHook += OnMessageHook;
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        EnsureGpuResources();
        RequestRender();
    }

    public void ConfigureRenderLoop(RenderLoopDriver driver, bool enable)
    {
        if (_currentDriver == driver && _renderLoopEnabled == enable)
        {
            return;
        }

        DetachRenderLoop();

        _currentDriver = driver;
        _renderLoopEnabled = enable && driver != RenderLoopDriver.None;

        if (!_renderLoopEnabled)
        {
            return;
        }

        switch (driver)
        {
            case RenderLoopDriver.ComponentDispatcher:
                _threadIdleHandler = OnThreadIdle;
                ComponentDispatcher.ThreadIdle += _threadIdleHandler;
                break;
            case RenderLoopDriver.CompositionTarget:
                _compositionRenderingHandler = OnCompositionRendering;
                CompositionTarget.Rendering += _compositionRenderingHandler;
                break;
        }

        RequestRender();
    }

    public void RequestRender()
    {
        if (!_isLoaded)
        {
            return;
        }

        EnsureGpuResources();
        if (_gpuLease is null || _swapChain is null)
        {
            return;
        }

        var size = GetSurfaceSize();
        SwapChainReady?.Invoke(this, new SwapChainLeaseEventArgs(_gpuLease, _swapChain, size));
    }

    public void ReleaseGpuResources(string? reason = null)
        => ResetGpuResources(reason);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        EnsureGpuResources();
        RequestRender();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        ConfigureRenderLoop(RenderLoopDriver.None, enable: false);
        ResetGpuResources();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_isLoaded)
        {
            EnsureGpuResources();
            RequestRender();
        }
    }

    private IntPtr OnMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_DPICHANGED:
            case WM_SIZE:
                EnsureGpuResources();
                RequestRender();
                break;
        }

        return IntPtr.Zero;
    }

    private void OnCompositionRendering(object? sender, EventArgs e)
        => RequestRender();

    private void OnThreadIdle(object? sender, EventArgs e)
        => RequestRender();

    private void DetachRenderLoop()
    {
        if (_compositionRenderingHandler is not null)
        {
            CompositionTarget.Rendering -= _compositionRenderingHandler;
            _compositionRenderingHandler = null;
        }

        if (_threadIdleHandler is not null)
        {
            ComponentDispatcher.ThreadIdle -= _threadIdleHandler;
            _threadIdleHandler = null;
        }

        _renderLoopEnabled = false;
        _currentDriver = RenderLoopDriver.None;
    }

    private void EnsureGpuResources()
    {
        if (_hwnd == 0)
        {
            return;
        }

        var size = GetSurfaceSize();
        if (size.IsEmpty)
        {
            return;
        }

        _gpuLease ??= WindowsGpuContext.Acquire(_deviceOptions);
        _swapChain = WindowsSurfaceFactory.EnsureSwapChainSurface(_gpuLease, this, _swapChain, size);
    }

    private void ResetGpuResources(string? reason = null)
    {
        if (_swapChain is not null)
        {
            WindowsSurfaceFactory.ReleaseSwapChain(this, _swapChain);
            _swapChain = null;
        }

        if (_gpuLease is { } lease)
        {
            WindowsSurfaceFactory.HandleDeviceLoss(lease.Context, this, reason);
            lease.Dispose();
            _gpuLease = null;
        }
    }

    private WindowsSurfaceSize GetSurfaceSize()
    {
        if (_hwnd == 0)
        {
            return WindowsSurfaceSize.Empty;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var width = (uint)Math.Max(Math.Round(ActualWidth * dpi.DpiScaleX), 0);
        var height = (uint)Math.Max(Math.Round(ActualHeight * dpi.DpiScaleY), 0);
        return new WindowsSurfaceSize(width, height);
    }

    nint IWindowsSurfaceSource.WindowHandle => _hwnd;

    WindowsSurfaceSize IWindowsSurfaceSource.GetSurfaceSize() => GetSurfaceSize();

    string? IWindowsSurfaceSource.DiagnosticsLabel => _deviceOptions.DiagnosticsLabel;

    void IWindowsSurfaceSource.OnSwapChainCreated(WindowsSwapChainSurface surface)
    {
        _swapChain = surface;
    }

    void IWindowsSurfaceSource.OnSwapChainResized(WindowsSwapChainSurface surface, WindowsSurfaceSize size)
    {
        _swapChain = surface;
    }

    void IWindowsSurfaceSource.OnSwapChainDestroyed()
    {
        _swapChain = null;
    }

    void IWindowsSurfaceSource.OnDeviceLost(string? reason)
    {
        // Placeholder for diagnostics hook; callers can override DeviceOptions to supply labels.
    }

    private static void EnsureWindowClass()
    {
        lock (ClassSync)
        {
            if (_isClassRegistered)
            {
                return;
            }

            var wndClass = new NativeMethods.WNDCLASS
            {
                lpfnWndProc = NativeMethods.DefWindowProc,
                hInstance = NativeMethods.GetModuleHandle(null),
                lpszClassName = WindowClassName,
                hCursor = NativeMethods.LoadCursor(IntPtr.Zero, NativeMethods.IDC_ARROW),
            };

            if (NativeMethods.RegisterClass(ref wndClass) == 0)
            {
                throw new InvalidOperationException("Failed to register host window class.");
            }

            _isClassRegistered = true;
        }
    }

    private static class NativeMethods
    {
        internal const int WS_CHILD = 0x40000000;
        internal const int WS_VISIBLE = 0x10000000;
        internal const string IDC_ARROW = "#32512";

        internal delegate IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WNDCLASS
        {
            public uint style;
            public WndProc lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string? lpszClassName;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern ushort RegisterClass([In] ref WNDCLASS lpWndClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr CreateWindowEx(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr DefWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetModuleHandle(string? moduleName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr LoadCursor(IntPtr hInstance, string lpCursorName);
    }
}
