using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VelloSharp;
using VelloSharp.WinForms;
using VelloSharp.WinForms.Integration;
using VelloSharp.Windows;
using WinFormsMotionMarkShim.Rendering;

namespace WinFormsMotionMarkShim.Controls;

internal sealed class FastPathRendererTabPage : TabPage
{
    private readonly MotionMarkEngine _engine;
    private readonly VelloRenderControl _renderControl;

    private int _lastTarget;
    private bool _isAnimationEnabled = true;

    public FastPathRendererTabPage(MotionMarkEngine engine)
    {
        _engine = engine;

        Text = "Vello Fast Path";

        _renderControl = new VelloRenderControl
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            RenderMode = VelloRenderMode.Continuous,
            PreferredBackend = VelloRenderBackend.Gpu,
        };

        _renderControl.RenderSurface += OnRenderSurface;
        _renderControl.PaintSurface += OnPaintSurface;

        Controls.Add(_renderControl);
    }

    public VelloFont? OverlayFont { get; set; }

    public Func<MotionMarkOverlayRequest, string>? OverlayTextProvider { get; set; }

    public event EventHandler<MotionMarkFrameEventArgs>? FrameRendered;

    public VelloRenderBackend PreferredBackend
    {
        get => _renderControl.PreferredBackend;
        set => _renderControl.PreferredBackend = value;
    }

    public void SetAnimationEnabled(bool enabled)
    {
        _isAnimationEnabled = enabled;
        _renderControl.RenderMode = enabled ? VelloRenderMode.Continuous : VelloRenderMode.OnDemand;
    }

    public void RequestRender()
    {
        if (_renderControl.RenderMode == VelloRenderMode.Continuous)
        {
            return;
        }

        _renderControl.Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _renderControl.RenderSurface -= OnRenderSurface;
            _renderControl.PaintSurface -= OnPaintSurface;
        }

        base.Dispose(disposing);
    }

    private void OnRenderSurface(object? sender, VelloSurfaceRenderEventArgs e)
    {
        if (_renderControl.PreferredBackend != VelloRenderBackend.Gpu)
        {
            return;
        }

        var width = (float)e.PixelSize.Width;
        var height = (float)e.PixelSize.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        e.Scene.Reset();
        var target = _engine.PopulateScene(e.Scene, width, height);
        _lastTarget = target;

        FrameRendered?.Invoke(
            this,
            new MotionMarkFrameEventArgs(e.Delta, e.IsAnimationFrame, target, isFastPathActive: true));

        e.RenderScene(e.Scene);
    }

    private void OnPaintSurface(object? sender, VelloPaintSurfaceEventArgs e)
    {
        var width = (float)e.Session.Width;
        var height = (float)e.Session.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var usesFastPath = _renderControl.PreferredBackend == VelloRenderBackend.Gpu;
        if (!usesFastPath)
        {
            var scene = e.Session.Scene;
            scene.Reset();

            var target = _engine.PopulateScene(scene, width, height);
            _lastTarget = target;

            FrameRendered?.Invoke(
                this,
                new MotionMarkFrameEventArgs(e.Delta, e.IsAnimationFrame && _isAnimationEnabled, target, isFastPathActive: false));
        }

        var graphics = e.GetGraphics();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Black);

        DrawOverlay(graphics, _lastTarget, usesFastPath);

        if (!usesFastPath)
        {
            e.Session.Complete();
        }
    }

    private void DrawOverlay(VelloGraphics graphics, int target, bool usesFastPath)
    {
        var overlayFont = OverlayFont;
        if (overlayFont is null)
        {
            return;
        }

        var overlayProvider = OverlayTextProvider;
        if (overlayProvider is null)
        {
            return;
        }

        var text = overlayProvider(new MotionMarkOverlayRequest(target, usesFastPath));
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        graphics.DrawString(text, overlayFont, Color.White, new PointF(16f, 16f));
    }
}
