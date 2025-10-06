using System;
using System.Drawing;
using System.Windows.Forms;
using VelloSharp;
using VelloSharp.WinForms.Integration;

namespace WinFormsMotionMarkShim.Controls;

internal sealed class MotionMarkControlPanel : UserControl
{
    private readonly FlowLayoutPanel _layout;
    private readonly NumericUpDown _complexityInput;
    private readonly CheckBox _animateCheckBox;
    private readonly ComboBox _backendSelector;
    private readonly Label _backendLabel;
    private readonly Label _elementsLabel;
    private readonly Label _fpsLabel;
    private readonly Button _resetButton;

    private RenderBackendOption[] _backendOptions = Array.Empty<RenderBackendOption>();
    private bool _suppressEvents;

    public MotionMarkControlPanel()
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        _layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
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
            Value = 6,
            Width = 60,
            Margin = new Padding(0, 2, 12, 0),
        };
        _complexityInput.ValueChanged += OnComplexityInputChanged;

        _animateCheckBox = new CheckBox
        {
            AutoSize = true,
            Text = "Animate",
            Checked = true,
            Margin = new Padding(0, 4, 12, 0),
        };
        _animateCheckBox.CheckedChanged += OnAnimateToggled;

        _resetButton = new Button
        {
            Text = "Reset",
            AutoSize = true,
            Margin = new Padding(0, 2, 12, 0),
        };
        _resetButton.Click += (_, _) => ResetRequested?.Invoke(this, EventArgs.Empty);

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
        _backendSelector.SelectedIndexChanged += OnBackendSelected;

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

        _layout.Controls.Add(complexityLabel);
        _layout.Controls.Add(_complexityInput);
        _layout.Controls.Add(_animateCheckBox);
        _layout.Controls.Add(_resetButton);
        _layout.Controls.Add(rendererLabel);
        _layout.Controls.Add(_backendSelector);
        _layout.Controls.Add(_backendLabel);
        _layout.Controls.Add(_elementsLabel);
        _layout.Controls.Add(_fpsLabel);

        Controls.Add(_layout);
    }

    public event EventHandler<int>? ComplexityChanged;

    public event EventHandler<bool>? AnimationToggled;

    public event EventHandler? ResetRequested;

    public event EventHandler<VelloRenderBackend>? BackendChanged;

    public int Complexity
    {
        get => (int)_complexityInput.Value;
        set => SetComplexity(value);
    }

    public bool IsAnimationEnabled
    {
        get => _animateCheckBox.Checked;
        set => SetAnimationEnabled(value);
    }

    public VelloRenderBackend? SelectedBackend
        => _backendSelector.SelectedItem is RenderBackendOption option ? option.Backend : null;

    public void SetComplexity(int value)
    {
        _suppressEvents = true;
        _complexityInput.Value = Math.Max(_complexityInput.Minimum, Math.Min(_complexityInput.Maximum, value));
        _suppressEvents = false;
    }

    public void SetAnimationEnabled(bool enabled)
    {
        _suppressEvents = true;
        _animateCheckBox.Checked = enabled;
        _suppressEvents = false;
    }

    public void SetBackendOptions(RenderBackendOption[] options, VelloRenderBackend? selected = null)
    {
        _backendOptions = options ?? Array.Empty<RenderBackendOption>();

        _suppressEvents = true;
        _backendSelector.Items.Clear();
        if (_backendOptions.Length > 0)
        {
            foreach (var option in _backendOptions)
            {
                _backendSelector.Items.Add(option);
            }
        }

        if (selected is { } backend)
        {
            SelectBackend(backend);
        }
        else if (_backendSelector.Items.Count > 0)
        {
            _backendSelector.SelectedIndex = 0;
        }

        _suppressEvents = false;
    }

    public void SelectBackend(VelloRenderBackend backend)
    {
        if (_backendOptions.Length == 0)
        {
            return;
        }

        var index = Array.FindIndex(_backendOptions, option => option.Backend == backend);
        if (index >= 0 && _backendSelector.SelectedIndex != index)
        {
            _suppressEvents = true;
            _backendSelector.SelectedIndex = index;
            _suppressEvents = false;
        }
    }

    public void SetBackendStatus(string text)
        => _backendLabel.Text = text;

    public void SetElementsStatus(string text)
        => _elementsLabel.Text = text;

    public void SetFpsStatus(string text)
        => _fpsLabel.Text = text;

    private void OnComplexityInputChanged(object? sender, EventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        ComplexityChanged?.Invoke(this, (int)_complexityInput.Value);
    }

    private void OnAnimateToggled(object? sender, EventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        AnimationToggled?.Invoke(this, _animateCheckBox.Checked);
    }

    private void OnBackendSelected(object? sender, EventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        if (_backendSelector.SelectedItem is RenderBackendOption option)
        {
            BackendChanged?.Invoke(this, option.Backend);
        }
    }

    internal readonly record struct RenderBackendOption(string DisplayName, VelloRenderBackend Backend)
    {
        public override string ToString() => DisplayName;
    }
}
