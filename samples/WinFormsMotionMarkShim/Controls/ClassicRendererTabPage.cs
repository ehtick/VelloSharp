using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WinFormsMotionMarkShim.Rendering;

namespace WinFormsMotionMarkShim.Controls;

internal sealed class ClassicRendererTabPage : TabPage
{
    private readonly ClassicCanvas _canvas;
    private readonly MotionMarkEngine _engine;
    private readonly Stopwatch _stopwatch = new();

    private bool _isAnimationEnabled;
    private TimeSpan _lastTimestamp;
    private long _frameId;

    public ClassicRendererTabPage(MotionMarkEngine engine)
    {
        _engine = engine;

        Text = "Classic Renderer";

        _canvas = new ClassicCanvas
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
        };
        _canvas.Paint += OnCanvasPaint;

        Controls.Add(_canvas);
    }

    public Font? OverlayFont { get; set; }

    public Func<MotionMarkOverlayRequest, string>? OverlayTextProvider { get; set; }

    public event EventHandler<MotionMarkFrameEventArgs>? FrameRendered;

    public bool IsAnimationEnabled => _isAnimationEnabled;

    public void SetAnimationEnabled(bool enabled)
    {
        if (_isAnimationEnabled == enabled)
        {
            return;
        }

        _isAnimationEnabled = enabled;

        if (!enabled)
        {
            _stopwatch.Reset();
            _lastTimestamp = TimeSpan.Zero;
            _frameId = 0;
        }
        else
        {
            _stopwatch.Restart();
            _lastTimestamp = TimeSpan.Zero;
            _frameId = 0;
        }
    }

    public void ResetAnimationState()
    {
        _stopwatch.Reset();
        _lastTimestamp = TimeSpan.Zero;
        _frameId = 0;
    }

    public void InvalidateCanvas()
        => _canvas.Invalidate();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _canvas.Paint -= OnCanvasPaint;
        }

        base.Dispose(disposing);
    }

    private void OnCanvasPaint(object? sender, PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Black);

        var clientSize = _canvas.ClientSize;
        if (clientSize.Width <= 0 || clientSize.Height <= 0)
        {
            return;
        }

        var delta = AdvanceTiming();
        var target = _engine.PrepareFrame();

        _engine.RenderClassic(graphics, clientSize.Width, clientSize.Height);

        FrameRendered?.Invoke(
            this,
            new MotionMarkFrameEventArgs(delta, _isAnimationEnabled, target, isFastPathActive: false));

        DrawOverlay(graphics, target);
    }

    private TimeSpan AdvanceTiming()
    {
        if (!_isAnimationEnabled)
        {
            return TimeSpan.Zero;
        }

        if (!_stopwatch.IsRunning)
        {
            _stopwatch.Start();
            _lastTimestamp = TimeSpan.Zero;
            _frameId = 0;
        }

        var timestamp = _stopwatch.Elapsed;
        var delta = _frameId == 0 ? TimeSpan.Zero : timestamp - _lastTimestamp;
        _lastTimestamp = timestamp;
        _frameId++;
        return delta;
    }

    private void DrawOverlay(Graphics graphics, int target)
    {
        if (OverlayFont is null)
        {
            return;
        }

        var overlayProvider = OverlayTextProvider;
        if (overlayProvider is null)
        {
            return;
        }

        var text = overlayProvider(new MotionMarkOverlayRequest(target, isFastPathActive: false));
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        using var brush = new SolidBrush(Color.White);
        graphics.DrawString(text, OverlayFont, brush, new PointF(16f, 16f));
    }

    private sealed class ClassicCanvas : Panel
    {
        public ClassicCanvas()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }
}
