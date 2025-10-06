using System;
using System.Drawing;
using System.Windows.Forms;
using VelloSharp;
using VelloSharp.WinForms;
using VelloSharp.WinForms.Integration;
using WinFormsMotionMarkShim.Controls;
using WinFormsMotionMarkShim.Rendering;
using DrawingFont = System.Drawing.Font;
using FormsTimer = System.Windows.Forms.Timer;

namespace WinFormsMotionMarkShim;

internal sealed class MainForm : Form
{
    private static readonly MotionMarkControlPanel.RenderBackendOption[] s_backendOptions =
    {
        new("GPU (wgpu)", VelloRenderBackend.Gpu),
        new("CPU (sparse)", VelloRenderBackend.Cpu),
    };

    private readonly MotionMarkEngine _engine = new();
    private readonly MotionMarkControlPanel _controlsPanel;
    private readonly ClassicRendererTabPage _classicTab;
    private readonly FastPathRendererTabPage _fastPathTab;
    private readonly TabControl _tabControl;

    private readonly FormsTimer _animationTimer;
    private readonly VelloFont _statusFont = new("Segoe UI", 20f);
    private readonly DrawingFont _statusOverlayFont = new("Segoe UI", 20f, FontStyle.Regular, GraphicsUnit.Pixel);

    private bool _isAnimationEnabled = true;
    private double _emaFps;
    private int _lastElementTarget;
    private VelloRenderBackend _selectedBackend = VelloRenderBackend.Gpu;

    public MainForm()
    {
        Text = "WinForms MotionMark Shim";
        Icon = null;
        MinimumSize = new Size(900, 600);

        _controlsPanel = new MotionMarkControlPanel
        {
            Dock = DockStyle.Top,
        };
        _controlsPanel.SetBackendOptions(s_backendOptions, _selectedBackend);
        _controlsPanel.ComplexityChanged += OnComplexityChanged;
        _controlsPanel.AnimationToggled += OnAnimationToggled;
        _controlsPanel.ResetRequested += OnResetRequested;
        _controlsPanel.BackendChanged += OnBackendChanged;

        _engine.Complexity = _controlsPanel.Complexity;
        _isAnimationEnabled = _controlsPanel.IsAnimationEnabled;

        _classicTab = new ClassicRendererTabPage(_engine)
        {
            OverlayFont = _statusOverlayFont,
            OverlayTextProvider = GetOverlayText,
        };
        _classicTab.FrameRendered += OnFrameRendered;
        _classicTab.SetAnimationEnabled(_isAnimationEnabled);

        _fastPathTab = new FastPathRendererTabPage(_engine)
        {
            OverlayFont = _statusFont,
            OverlayTextProvider = GetOverlayText,
        };
        _fastPathTab.FrameRendered += OnFrameRendered;
        _fastPathTab.PreferredBackend = _selectedBackend;
        _fastPathTab.SetAnimationEnabled(_isAnimationEnabled);

        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
        };
        _tabControl.SelectedIndexChanged += OnTabSelectionChanged;
        _tabControl.TabPages.Add(_classicTab);
        _tabControl.TabPages.Add(_fastPathTab);

        Controls.Add(_tabControl);
        Controls.Add(_controlsPanel);

        _animationTimer = new FormsTimer { Interval = 16 };
        _animationTimer.Tick += OnAnimationTick;

        UpdateBackendStatusLabel();
        ResetScene();

        if (_isAnimationEnabled)
        {
            _animationTimer.Start();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer.Tick -= OnAnimationTick;
            _animationTimer.Stop();
            _animationTimer.Dispose();

            _controlsPanel.ComplexityChanged -= OnComplexityChanged;
            _controlsPanel.AnimationToggled -= OnAnimationToggled;
            _controlsPanel.ResetRequested -= OnResetRequested;
            _controlsPanel.BackendChanged -= OnBackendChanged;

            _classicTab.FrameRendered -= OnFrameRendered;
            _fastPathTab.FrameRendered -= OnFrameRendered;
            _tabControl.SelectedIndexChanged -= OnTabSelectionChanged;

            _statusOverlayFont.Dispose();
            _statusFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OnComplexityChanged(object? sender, int complexity)
    {
        _engine.Complexity = complexity;

        if (!_isAnimationEnabled)
        {
            _classicTab.InvalidateCanvas();
            _fastPathTab.RequestRender();
        }
    }

    private void OnAnimationToggled(object? sender, bool enabled)
    {
        _isAnimationEnabled = enabled;
        _classicTab.SetAnimationEnabled(enabled);
        _fastPathTab.SetAnimationEnabled(enabled);

        if (enabled)
        {
            _animationTimer.Start();
        }
        else
        {
            _animationTimer.Stop();
        }

        _classicTab.InvalidateCanvas();
        _fastPathTab.RequestRender();
    }

    private void OnResetRequested(object? sender, EventArgs e)
    {
        ResetScene();
    }

    private void OnBackendChanged(object? sender, VelloRenderBackend backend)
    {
        if (_selectedBackend == backend)
        {
            return;
        }

        _selectedBackend = backend;
        _fastPathTab.PreferredBackend = backend;
        UpdateBackendStatusLabel();

        _classicTab.InvalidateCanvas();
        _fastPathTab.RequestRender();
    }

    private void OnTabSelectionChanged(object? sender, EventArgs e)
    {
        UpdateBackendStatusLabel();

        if (_tabControl.SelectedTab == _classicTab)
        {
            _classicTab.InvalidateCanvas();
        }
        else
        {
            _fastPathTab.RequestRender();
        }
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        if (!_isAnimationEnabled)
        {
            return;
        }

        _classicTab.InvalidateCanvas();
    }

    private void OnFrameRendered(object? sender, MotionMarkFrameEventArgs e)
    {
        UpdateFps(e.Delta, e.IsAnimationFrame);

        _lastElementTarget = e.ElementTarget;
        _controlsPanel.SetElementsStatus($"Elements: {_lastElementTarget:N0}");
    }

    private void UpdateFps(TimeSpan delta, bool isAnimationFrame)
    {
        if (!isAnimationFrame || delta <= TimeSpan.Zero)
        {
            return;
        }

        var sample = 1.0 / delta.TotalSeconds;
        if (!double.IsFinite(sample))
        {
            return;
        }

        _emaFps = _emaFps <= 0 ? sample : (_emaFps * 0.9) + (sample * 0.1);
        _controlsPanel.SetFpsStatus($"FPS: {_emaFps:0.0}");
    }

    private string GetOverlayText(MotionMarkOverlayRequest request)
    {
        var fpsText = _emaFps > 0 ? $"{_emaFps:0.0}" : "--";
        var backendText = request.IsFastPathActive ? "GPU (fast path)" : GetBackendDisplayName();
        var elementsText = request.ElementTarget.ToString("N0");
        return $"{backendText}  Complexity {_engine.Complexity}  Elements {elementsText}  FPS {fpsText}";
    }

    private void ResetScene()
    {
        _engine.ResetScene();
        _classicTab.ResetAnimationState();
        _classicTab.SetAnimationEnabled(_isAnimationEnabled);
        _fastPathTab.SetAnimationEnabled(_isAnimationEnabled);

        _emaFps = 0;
        _lastElementTarget = 0;

        _controlsPanel.SetElementsStatus("Elements: 0");
        _controlsPanel.SetFpsStatus("FPS: --");

        if (_isAnimationEnabled)
        {
            _animationTimer.Stop();
            _animationTimer.Start();
        }
        else
        {
            _animationTimer.Stop();
        }

        _classicTab.InvalidateCanvas();
        _fastPathTab.RequestRender();
    }

    private void UpdateBackendStatusLabel()
    {
        var text = _tabControl.SelectedTab == _fastPathTab && _selectedBackend == VelloRenderBackend.Gpu
            ? "Selected: GPU (fast path)"
            : $"Selected: {GetBackendDisplayName()}";

        _controlsPanel.SetBackendStatus(text);
    }

    private string GetBackendDisplayName()
        => _selectedBackend == VelloRenderBackend.Gpu ? "GPU" : "CPU (sparse)";
}
