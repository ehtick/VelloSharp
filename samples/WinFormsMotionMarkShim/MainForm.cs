using System;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms;
using MotionMark.SceneShared;
using VelloSharp;
using VelloSharp.WinForms;
using VelloSharp.WinForms.Integration;

namespace WinFormsMotionMarkShim;

internal sealed class MainForm : Form
{
    private static readonly BackendOption[] s_backendOptions =
    {
        new("GPU (wgpu)", VelloRenderBackend.Gpu),
        new("CPU (sparse)", VelloRenderBackend.Cpu),
    };

    private readonly VelloRenderControl _renderControl;
    private MotionMarkScene _scene = new();
    private readonly PathBuilder _pathBuilder = new();
    private readonly StrokeStyle _strokeStyle = new()
    {
        LineJoin = VelloSharp.LineJoin.Bevel,
        StartCap = VelloSharp.LineCap.Round,
        EndCap = VelloSharp.LineCap.Round,
        MiterLimit = 4.0,
    };
    private readonly VelloFont _statusFont = new("Segoe UI", 20f);

    private readonly NumericUpDown _complexityInput;
    private readonly CheckBox _animateCheckBox;
    private readonly ComboBox _backendSelector;
    private readonly Label _backendLabel;
    private readonly Label _elementsLabel;
    private readonly Label _fpsLabel;

    private int _complexity = 6;
    private bool _animate = true;
    private int _lastElementTarget;
    private double _emaFps;
    private VelloRenderBackend _selectedBackend = VelloRenderBackend.Gpu;

    public MainForm()
    {
        Text = "WinForms MotionMark Shim";
        Icon = null;
        MinimumSize = new Size(900, 600);

        var controlsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Padding = new Padding(12, 12, 12, 8),
        };

        var complexityLabel = new Label
        {
            AutoSize = true,
            Text = "Complexity:",
            Margin = new Padding(0, 6, 6, 0),
        };

        _complexityInput = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 24,
            Value = _complexity,
            Width = 60,
            Margin = new Padding(0, 2, 12, 0),
        };
        _complexityInput.ValueChanged += OnComplexityChanged;

        _animateCheckBox = new CheckBox
        {
            AutoSize = true,
            Text = "Animate",
            Checked = _animate,
            Margin = new Padding(0, 4, 12, 0),
        };
        _animateCheckBox.CheckedChanged += OnAnimateChanged;

        var resetButton = new Button
        {
            Text = "Reset",
            AutoSize = true,
            Margin = new Padding(0, 2, 12, 0),
        };
        resetButton.Click += (_, _) => ResetScene();

        var rendererLabel = new Label
        {
            AutoSize = true,
            Text = "Renderer:",
            Margin = new Padding(0, 6, 6, 0),
        };

        _backendSelector = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 150,
            Margin = new Padding(0, 2, 12, 0),
        };
        _backendSelector.Items.AddRange(s_backendOptions);
        _backendSelector.SelectedIndex = 0;
        _backendSelector.SelectedIndexChanged += OnBackendSelectionChanged;

        _backendLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 6, 12, 0),
        };

        _elementsLabel = new Label
        {
            AutoSize = true,
            Text = "Elements: 0",
            Margin = new Padding(0, 6, 12, 0),
        };

        _fpsLabel = new Label
        {
            AutoSize = true,
            Text = "FPS: --",
            Margin = new Padding(0, 6, 0, 0),
        };

        controlsPanel.Controls.Add(complexityLabel);
        controlsPanel.Controls.Add(_complexityInput);
        controlsPanel.Controls.Add(_animateCheckBox);
        controlsPanel.Controls.Add(resetButton);
        controlsPanel.Controls.Add(rendererLabel);
        controlsPanel.Controls.Add(_backendSelector);
        controlsPanel.Controls.Add(_backendLabel);
        controlsPanel.Controls.Add(_elementsLabel);
        controlsPanel.Controls.Add(_fpsLabel);

        _renderControl = new VelloRenderControl
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            RenderMode = VelloRenderMode.Continuous,
        };
        _renderControl.PaintSurface += OnPaintSurface;

        SetBackend(VelloRenderBackend.Gpu, updateSelector: false);

        Controls.Add(_renderControl);
        Controls.Add(controlsPanel);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _renderControl.PaintSurface -= OnPaintSurface;
            _statusFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OnComplexityChanged(object? sender, EventArgs e)
    {
        _complexity = (int)_complexityInput.Value;
        if (!_animate)
        {
            _renderControl.Invalidate();
        }
    }

    private void OnAnimateChanged(object? sender, EventArgs e)
    {
        _animate = _animateCheckBox.Checked;
        _renderControl.RenderMode = _animate ? VelloRenderMode.Continuous : VelloRenderMode.OnDemand;
        if (!_animate)
        {
            _renderControl.Invalidate();
        }
    }

    private void OnBackendSelectionChanged(object? sender, EventArgs e)
    {
        if (_backendSelector.SelectedItem is BackendOption option)
        {
            SetBackend(option.Backend, updateSelector: false);
        }
    }

    private void SetBackend(VelloRenderBackend backend, bool updateSelector)
    {
        _selectedBackend = backend;
        _renderControl.PreferredBackend = backend;

        if (updateSelector)
        {
            var match = Array.FindIndex(s_backendOptions, option => option.Backend == backend);
            if (match >= 0 && _backendSelector.SelectedIndex != match)
            {
                _backendSelector.SelectedIndex = match;
            }
        }

        UpdateBackendLabel();

        if (!_animate)
        {
            _renderControl.Invalidate();
        }
    }

    private void UpdateBackendLabel()
    {
        _backendLabel.Text = $"Selected: {GetBackendDisplayName()}";
    }

    private void ResetScene()
    {
        _scene = new MotionMarkScene();
        _pathBuilder.Clear();
        _emaFps = 0;
        if (!_animate)
        {
            _renderControl.Invalidate();
        }
    }

    private void OnPaintSurface(object? sender, VelloPaintSurfaceEventArgs e)
    {
        var graphics = e.GetGraphics();
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Black);

        var target = _scene.PrepareFrame(_complexity);
        _lastElementTarget = target;
        _elementsLabel.Text = $"Elements: {target:N0}";

        UpdateFps(e);

        var width = (float)e.Session.Width;
        var height = (float)e.Session.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var transform = CreateMotionMarkTransform(width, height);
        var scene = e.Session.Scene;
        var elements = _scene.Elements;

        if (elements.Length > 0)
        {
            var builder = _pathBuilder;
            builder.Clear();

            for (var i = 0; i < elements.Length; i++)
            {
                ref readonly var element = ref elements[i];
                if (builder.Count == 0)
                {
                    builder.MoveTo(element.Start.X, element.Start.Y);
                }

                switch (element.Type)
                {
                    case MotionMarkScene.ElementType.Line:
                        builder.LineTo(element.End.X, element.End.Y);
                        break;
                    case MotionMarkScene.ElementType.Quadratic:
                        builder.QuadraticTo(element.Control1.X, element.Control1.Y, element.End.X, element.End.Y);
                        break;
                    case MotionMarkScene.ElementType.Cubic:
                        builder.CubicTo(
                            element.Control1.X,
                            element.Control1.Y,
                            element.Control2.X,
                            element.Control2.Y,
                            element.End.X,
                            element.End.Y);
                        break;
                }

                var strokeBreak = element.IsSplit || i == elements.Length - 1;
                if (strokeBreak)
                {
                    _strokeStyle.Width = Math.Max(0.5, element.Width);
                    scene.StrokePath(builder, _strokeStyle, transform, ToRgba(element.Color));
                    builder.Clear();
                }
            }
        }

        DrawOverlay(graphics, target);
    }

    private void DrawOverlay(VelloGraphics graphics, int target)
    {
        var fpsText = _emaFps > 0 ? $"{_emaFps:0.0}" : "--";
        _fpsLabel.Text = $"FPS: {fpsText}";

        var statusText = $"{GetBackendDisplayName()}  Complexity {_complexity}  Elements {target:N0}  FPS {fpsText}";
        graphics.DrawString(statusText, _statusFont, Color.White, new PointF(16f, 16f));
    }

    private void UpdateFps(VelloPaintSurfaceEventArgs e)
    {
        if (!e.IsAnimationFrame)
        {
            return;
        }

        if (e.Delta <= TimeSpan.Zero)
        {
            return;
        }

        var sample = 1.0 / e.Delta.TotalSeconds;
        if (double.IsFinite(sample))
        {
            _emaFps = _emaFps <= 0 ? sample : (_emaFps * 0.9) + (sample * 0.1);
        }
    }

    private static Matrix3x2 CreateMotionMarkTransform(float width, float height)
    {
        if (width <= 0 || height <= 0)
        {
            return Matrix3x2.Identity;
        }

        var scale = Math.Min(width / MotionMarkScene.CanvasWidth, height / MotionMarkScene.CanvasHeight);
        if (scale <= 0f)
        {
            scale = 1f;
        }

        var scaledWidth = MotionMarkScene.CanvasWidth * scale;
        var scaledHeight = MotionMarkScene.CanvasHeight * scale;
        var translate = new Vector2((width - scaledWidth) * 0.5f, (height - scaledHeight) * 0.5f);

        return Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(translate);
    }

    private static RgbaColor ToRgba(Color color)
        => RgbaColor.FromBytes(color.R, color.G, color.B, color.A);

    private string GetBackendDisplayName()
        => _selectedBackend == VelloRenderBackend.Gpu ? "GPU" : "CPU (sparse)";

    private sealed class BackendOption
    {
        public BackendOption(string name, VelloRenderBackend backend)
        {
            Name = name;
            Backend = backend;
        }

        public string Name { get; }
        public VelloRenderBackend Backend { get; }

        public override string ToString() => Name;
    }
}


