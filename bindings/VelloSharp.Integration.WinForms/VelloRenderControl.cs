using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VelloSharp;
using VelloSharp.Windows;
using VelloSharp.WinForms;

namespace VelloSharp.WinForms.Integration;

public class VelloRenderControl : Control, IWindowsSurfaceSource
{
    private const int WmSize = 0x0005;
    private const int WmPaint = 0x000F;
    private const int WmEraseBkgnd = 0x0014;
    private const int WmDpiChanged = 0x02E0;

    private readonly object _renderLock = new();

    private VelloGraphicsDevice? _device;
    private VelloGraphicsDeviceOptions _options = VelloGraphicsDeviceOptions.Default;
    private WindowsGpuContextLease? _gpuLease;
    private WindowsSwapChainSurface? _swapChain;
    private RenderLoop? _renderLoop;
    private readonly Stopwatch _frameStopwatch = new();
    private TimeSpan _lastFrameTimestamp;
    private long _frameId;
    private bool _handlingSizeMessage;
    private bool _isDisposed;
    private VelloRenderMode _renderMode = VelloRenderMode.OnDemand;

    private VelloRenderBackend _backend = VelloRenderBackend.Gpu;
    private Bitmap? _cpuBitmap;
    private byte[]? _cpuBuffer;
    private GCHandle _cpuBufferHandle;
    private bool _cpuBufferPinned;
    private int _cpuBufferWidth;
    private int _cpuBufferHeight;
    private int _cpuBufferStride;

    public VelloRenderControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.Opaque |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);
        DoubleBuffered = false;
        TabStop = false;
    }

    [Category("Behavior")]
    [Description("Occurs when the control is ready to record drawing commands into the current Vello scene.")]
    public event EventHandler<VelloPaintSurfaceEventArgs>? PaintSurface;

    public event EventHandler<VelloSurfaceRenderEventArgs>? RenderSurface;

    [Category("Behavior")]
    public VelloGraphicsDeviceOptions DeviceOptions
    {
        get => _options;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (_options == value)
            {
                return;
            }

            _options = value;
            ResetDevice();
        }
    }

    [Category("Behavior")]
    [DefaultValue(VelloRenderMode.OnDemand)]
    [Description("Determines whether the control renders only when invalidated or runs a continuous render loop.")]
    public VelloRenderMode RenderMode
    {
        get => _renderMode;
        set
        {
            if (_renderMode == value)
            {
                return;
            }

            _renderMode = value;
            UpdateRenderLoop();
            Invalidate();
        }
    }
    [Category("Behavior")]
    [DefaultValue(VelloRenderBackend.Gpu)]
    [Description("Selects whether the control renders via GPU swapchain or CPU rasterisation.")]
    public VelloRenderBackend PreferredBackend
    {
        get => _backend;
        set
        {
            if (!Enum.IsDefined(typeof(VelloRenderBackend), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (_backend == value)
            {
                return;
            }

            _backend = value;

            lock (_renderLock)
            {
                if (_backend == VelloRenderBackend.Gpu)
                {
                    ReleaseCpuResources();
                }
                else
                {
                    ResetGpuResources();
                }
            }

            if (IsHandleCreated && !IsDesignModeActive)
            {
                Invalidate();
            }

            UpdateRenderLoop();
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (IsDesignModeActive)
        {
            return;
        }

        lock (_renderLock)
        {
            EnsureDevice();
            if (_backend == VelloRenderBackend.Gpu)
            {
                TryEnsureGpuResources(ClientSize);
            }
        }

        UpdateRenderLoop();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        StopRenderLoop();

        lock (_renderLock)
        {
            ResetGpuResources();
            ReleaseCpuResources();
        }

        base.OnHandleDestroyed(e);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        UpdateRenderLoop();
    }

    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        UpdateRenderLoop();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);

        if (_handlingSizeMessage)
        {
            return;
        }

        HandleSizeChange();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);

        if (IsDesignModeActive)
        {
            return;
        }

        lock (_renderLock)
        {
            ResetGpuResources();
            ReleaseCpuResources();
        }

        Invalidate();
        UpdateRenderLoop();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (IsDesignModeActive)
        {
            RenderDesignTimePlaceholder(e);
            return;
        }

        RenderFrame(e);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // Avoid background erase; the GPU render path clears the surface explicitly.
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_isDisposed)
        {
            StopRenderLoop();

            lock (_renderLock)
            {
                ResetGpuResources();
                ReleaseCpuResources();
                _device?.Dispose();
                _device = null;
            }

            _isDisposed = true;
        }

        base.Dispose(disposing);
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WmEraseBkgnd:
                m.Result = IntPtr.Zero;
                return;

            case WmSize:
                _handlingSizeMessage = true;
                base.WndProc(ref m);
                _handlingSizeMessage = false;
                HandleSizeChange();
                return;

            case WmDpiChanged:
                base.WndProc(ref m);
                HandleDpiChangedMessage();
                return;

            default:
                base.WndProc(ref m);

                if (m.Msg == WmPaint && _renderMode == VelloRenderMode.Continuous)
                {
                    UpdateRenderLoop();
                }

                return;
        }
    }

    protected virtual void OnPaintSurface(VelloPaintSurfaceEventArgs args)
        => PaintSurface?.Invoke(this, args);

    protected virtual void OnRenderSurface(VelloSurfaceRenderEventArgs args)
        => RenderSurface?.Invoke(this, args);

    private void RenderFrame(PaintEventArgs e)
    {
        if (!IsHandleCreated || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        lock (_renderLock)
        {
            EnsureDevice();

            var logicalSize = ClientSize;
            var (pixelWidth, pixelHeight) = GetPixelSize(logicalSize);

            if (_backend == VelloRenderBackend.Gpu)
            {
                if (!TryEnsureGpuResources(logicalSize))
                {
                    e.Graphics.Clear(BackColor);
                    return;
                }
            }

            if (_device is null)
            {
                return;
            }

            var sessionWidth = _backend == VelloRenderBackend.Gpu
                ? (uint)Math.Max(1, logicalSize.Width)
                : Math.Max(pixelWidth, 1u);
            var sessionHeight = _backend == VelloRenderBackend.Gpu
                ? (uint)Math.Max(1, logicalSize.Height)
                : Math.Max(pixelHeight, 1u);

            using var session = _device.BeginSession(sessionWidth, sessionHeight);

            var (timestamp, delta) = NextFrameTiming();
            var frameId = ++_frameId;
            var isAnimationFrame = _renderMode == VelloRenderMode.Continuous;

            var args = new VelloPaintSurfaceEventArgs(session, timestamp, delta, frameId, isAnimationFrame);
            OnPaintSurface(args);

            if (_backend == VelloRenderBackend.Gpu)
            {
                if (!RenderToSwapChain(session, logicalSize, timestamp, delta, frameId, isAnimationFrame))
                {
                    session.Complete();
                }
            }
            else
            {
                RenderToCpu(session, logicalSize, e, pixelWidth, pixelHeight);
            }
        }
    }

    private void HandleSizeChange()
    {
        if (IsDesignModeActive || !IsHandleCreated)
        {
            return;
        }

        lock (_renderLock)
        {
            if (_backend == VelloRenderBackend.Gpu && _swapChain is not null)
            {
                TryEnsureGpuResources(ClientSize);
            }
            else if (_backend == VelloRenderBackend.Cpu)
            {
                ReleaseCpuResources();
            }
        }

        if (_renderMode == VelloRenderMode.OnDemand)
        {
            Invalidate();
        }
    }

    private void HandleDpiChangedMessage()
    {
        if (IsDesignModeActive)
        {
            return;
        }

        lock (_renderLock)
        {
            ResetGpuResources();
            ReleaseCpuResources();
        }

        Invalidate();
        UpdateRenderLoop();
    }

    private void EnsureDevice()
    {
        if (_device is not null || IsDesignModeActive)
        {
            return;
        }

        var logicalSize = ClientSize;
        var width = (uint)Math.Max(1, logicalSize.Width);
        var height = (uint)Math.Max(1, logicalSize.Height);

        _device = new VelloGraphicsDevice(width, height, _options);
    }

    private bool TryEnsureGpuResources(Size logicalSize)
    {
        if (!IsHandleCreated || logicalSize.Width <= 0 || logicalSize.Height <= 0)
        {
            return false;
        }

        try
        {
            EnsureGpuResources(logicalSize);
            return true;
        }
        catch (DllNotFoundException ex)
        {
            ResetGpuResources(ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VelloRenderControl] Failed to initialise GPU swap chain: {ex}");
            ResetGpuResources(ex.Message);
            return false;
        }
    }

    private void EnsureGpuResources(Size logicalSize)
    {
        var (pixelWidth, pixelHeight) = GetPixelSize(logicalSize);
        if (pixelWidth == 0 || pixelHeight == 0)
        {
            return;
        }

        _gpuLease ??= WindowsGpuContext.Acquire(_options);
        var size = new WindowsSurfaceSize(pixelWidth, pixelHeight);
        _swapChain = WindowsSurfaceFactory.EnsureSwapChainSurface(_gpuLease, this, _swapChain, size);
    }

    private bool RenderToSwapChain(VelloGraphicsSession session, Size logicalSize, TimeSpan timestamp, TimeSpan delta, long frameId, bool isAnimationFrame)
    {
        if (_gpuLease is null || _swapChain is null)
        {
            return false;
        }

        var (pixelWidth, pixelHeight) = GetPixelSize(logicalSize);
        if (pixelWidth == 0 || pixelHeight == 0)
        {
            return false;
        }

        try
        {
            _swapChain.Configure(pixelWidth, pixelHeight);

            using var surfaceTexture = _swapChain.AcquireNextTexture();
            using var textureView = surfaceTexture.CreateView();

            var renderParams = new RenderParams(
                pixelWidth,
                pixelHeight,
                _options.BackgroundColor,
                _options.GetAntialiasingMode(),
                _options.Format);

            var renderParamsToUse = renderParams;
            if (RenderSurface is not null)
            {
                var surfaceArgs = new VelloSurfaceRenderEventArgs(
                    _gpuLease,
                    session.Scene,
                    textureView,
                    _swapChain.Format,
                    renderParams,
                    new WindowsSurfaceSize(pixelWidth, pixelHeight),
                    timestamp,
                    delta,
                    frameId,
                    isAnimationFrame);
                OnRenderSurface(surfaceArgs);
                renderParamsToUse = surfaceArgs.RenderParams;
                if (!surfaceArgs.Handled)
                {
                    _gpuLease.Context.Renderer.RenderSurface(session.Scene, textureView, renderParamsToUse, _swapChain.Format);
                }
            }
            else
            {
                _gpuLease.Context.Renderer.RenderSurface(session.Scene, textureView, renderParamsToUse, _swapChain.Format);
            }

            surfaceTexture.Present();
            _gpuLease.Context.RecordPresentation();
            session.Complete();
            return true;
        }
        catch (DllNotFoundException ex)
        {
            ResetGpuResources(ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VelloRenderControl] GPU render failed, falling back to CPU path: {ex}");
            ResetGpuResources(ex.Message);
            return false;
        }
    }

    private void RenderToCpu(VelloGraphicsSession session, Size logicalSize, PaintEventArgs paintArgs, uint pixelWidth, uint pixelHeight)
    {
        if (pixelWidth == 0 || pixelHeight == 0)
        {
            session.Complete();
            return;
        }

        EnsureCpuBuffer(pixelWidth, pixelHeight);

        if (_cpuBuffer is null || _cpuBitmap is null || _cpuBufferStride <= 0)
        {
            session.Complete();
            return;
        }

        var bufferLength = _cpuBufferStride * _cpuBufferHeight;
        var span = _cpuBuffer.AsSpan(0, bufferLength);
        session.Submit(span, _cpuBufferStride);

        var graphics = paintArgs.Graphics;
        var originalCompositingMode = graphics.CompositingMode;
        var originalInterpolationMode = graphics.InterpolationMode;
        var originalPixelOffsetMode = graphics.PixelOffsetMode;

        try
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.None;
            graphics.DrawImage(_cpuBitmap, new Rectangle(Point.Empty, logicalSize));
        }
        finally
        {
            graphics.CompositingMode = originalCompositingMode;
            graphics.InterpolationMode = originalInterpolationMode;
            graphics.PixelOffsetMode = originalPixelOffsetMode;
        }
    }

    private void EnsureCpuBuffer(uint pixelWidth, uint pixelHeight)
    {
        var width = (int)Math.Clamp(pixelWidth, 1u, (uint)int.MaxValue);
        var height = (int)Math.Clamp(pixelHeight, 1u, (uint)int.MaxValue);

        if (_cpuBuffer is not null && _cpuBitmap is not null &&
            _cpuBufferWidth == width && _cpuBufferHeight == height)
        {
            return;
        }

        ReleaseCpuResources();

        var stride = checked(width * 4);
        var bufferSize = checked(stride * height);
        _cpuBuffer = new byte[bufferSize];
        _cpuBufferHandle = GCHandle.Alloc(_cpuBuffer, GCHandleType.Pinned);
        _cpuBufferPinned = true;
        _cpuBitmap = new Bitmap(width, height, stride, PixelFormat.Format32bppPArgb, _cpuBufferHandle.AddrOfPinnedObject());
        _cpuBufferWidth = width;
        _cpuBufferHeight = height;
        _cpuBufferStride = stride;
    }

    private void ReleaseCpuResources()
    {
        if (_cpuBufferPinned)
        {
            _cpuBufferHandle.Free();
            _cpuBufferPinned = false;
        }

        _cpuBitmap?.Dispose();
        _cpuBitmap = null;
        _cpuBuffer = null;
        _cpuBufferWidth = 0;
        _cpuBufferHeight = 0;
        _cpuBufferStride = 0;
    }

    private (uint Width, uint Height) GetPixelSize(Size logicalSize)
    {
        if (logicalSize.Width <= 0 || logicalSize.Height <= 0)
        {
            return (0, 0);
        }

        var dpi = DeviceDpi > 0 ? DeviceDpi : 96;
        var scale = Math.Max(dpi / 96f, 0.5f);
        var width = (uint)Math.Max(1, Math.Round(logicalSize.Width * scale));
        var height = (uint)Math.Max(1, Math.Round(logicalSize.Height * scale));
        return (width, height);
    }

    private void ResetDevice()
    {
        lock (_renderLock)
        {
            ResetGpuResources();
            ReleaseCpuResources();
            _device?.Dispose();
            _device = null;
        }

        if (IsHandleCreated && !IsDesignModeActive)
        {
            Invalidate();
            UpdateRenderLoop();
        }
    }

    private void ResetGpuResources(string? deviceLossReason = null)
    {
        if (_swapChain is not null)
        {
            WindowsSurfaceFactory.ReleaseSwapChain(this, _swapChain);
            _swapChain = null;
        }

        if (_gpuLease is { } lease)
        {
            if (deviceLossReason is not null)
            {
                WindowsSurfaceFactory.HandleDeviceLoss(lease.Context, this, deviceLossReason);
            }
            else
            {
                lease.Context.RecordDeviceReset();
            }

            lease.Dispose();
            _gpuLease = null;
        }

        ResetTiming();
    }

    private void ResetTiming()
    {
        _frameStopwatch.Reset();
        _lastFrameTimestamp = TimeSpan.Zero;
        _frameId = 0;
    }

    private (TimeSpan Timestamp, TimeSpan Delta) NextFrameTiming()
    {
        if (!_frameStopwatch.IsRunning)
        {
            _frameStopwatch.Start();
            _lastFrameTimestamp = TimeSpan.Zero;
            return (TimeSpan.Zero, TimeSpan.Zero);
        }

        var timestamp = _frameStopwatch.Elapsed;
        var delta = timestamp - _lastFrameTimestamp;

        if (delta < TimeSpan.Zero)
        {
            delta = TimeSpan.Zero;
        }

        _lastFrameTimestamp = timestamp;
        return (timestamp, delta);
    }

    private void UpdateRenderLoop()
    {
        if (ShouldRenderContinuously)
        {
            _renderLoop ??= new RenderLoop(this);
        }
        else
        {
            StopRenderLoop();
        }
    }

    private bool ShouldRenderContinuously
        => _renderMode == VelloRenderMode.Continuous
           && !IsDesignModeActive
           && !_isDisposed
           && IsHandleCreated
           && Visible;

    private void StopRenderLoop()
    {
        _renderLoop?.Dispose();
        _renderLoop = null;
    }

    private void RenderDesignTimePlaceholder(PaintEventArgs e)
    {
        var rect = ClientRectangle;
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        e.Graphics.Clear(SystemColors.ControlLight);

        rect.Width -= 1;
        rect.Height -= 1;
        if (rect.Width > 0 && rect.Height > 0)
        {
            using var borderPen = new Pen(SystemColors.ControlDark);
            e.Graphics.DrawRectangle(borderPen, rect);
        }

        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
        };

        using var brush = new SolidBrush(SystemColors.ControlText);
        var text = "VelloRenderControl (design mode)";
        e.Graphics.DrawString(text, Font ?? Control.DefaultFont, brush, ClientRectangle, format);
    }

    private bool IsDesignModeActive
    {
        get
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                return true;
            }

            if (Site?.DesignMode == true)
            {
                return true;
            }

            return DesignMode;
        }
    }

    WindowsSurfaceSize IWindowsSurfaceSource.GetSurfaceSize()
    {
        var (width, height) = GetPixelSize(ClientSize);
        return new WindowsSurfaceSize(width, height);
    }

    nint IWindowsSurfaceSource.WindowHandle => Handle;

    string? IWindowsSurfaceSource.DiagnosticsLabel => _options.DiagnosticsLabel;

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
        // no-op; WinForms path will attempt to recreate resources on demand.
    }

    private sealed class RenderLoop : IDisposable
    {
        private readonly VelloRenderControl _owner;
        private bool _disposed;

        public RenderLoop(VelloRenderControl owner)
        {
            _owner = owner;
            Application.Idle += OnApplicationIdle;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Application.Idle -= OnApplicationIdle;
        }

        private void OnApplicationIdle(object? sender, EventArgs e)
        {
            if (_disposed || !_owner.ShouldRenderContinuously)
            {
                return;
            }

            if (!NativeMethods.AppStillIdle())
            {
                return;
            }

            if (_owner.Visible && _owner.IsHandleCreated)
            {
                _owner.Invalidate();
            }
        }
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage
        {
            public IntPtr Hwnd;
            public uint Msg;
            public UIntPtr WParam;
            public IntPtr LParam;
            public uint Time;
            public NativePoint Point;
        }

        [DllImport("user32.dll")]
        private static extern bool PeekMessage(out NativeMessage lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        public static bool AppStillIdle()
        {
            NativeMessage message;
            return !PeekMessage(out message, IntPtr.Zero, 0, 0, 0);
        }
    }
}









