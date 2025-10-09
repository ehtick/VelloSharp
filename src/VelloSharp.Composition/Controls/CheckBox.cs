using System;

namespace VelloSharp.Composition.Controls;

public class CheckBox : ContentControl
{
    private bool? _isChecked;
    private LayoutThickness _padding = new(4, 2, 4, 2);
    private CompositionColor _indicatorFill = new(0.34f, 0.65f, 0.87f, 1f);
    private CompositionColor _indicatorBorder = new(0.46f, 0.55f, 0.68f, 1f);
    private CompositionColor _foreground = new(0.90f, 0.95f, 0.99f, 1f);
    private float _fontSize = 14f;
    private string? _text;

    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
            {
                return;
            }

            _isChecked = value;
            IsTemplateApplied = false;
        }
    }

    public LayoutThickness Padding
    {
        get => _padding;
        set
        {
            if (_padding.Equals(value))
            {
                return;
            }

            _padding = value;
            IsTemplateApplied = false;
        }
    }

    public CompositionColor IndicatorFill
    {
        get => _indicatorFill;
        set
        {
            if (_indicatorFill.Equals(value))
            {
                return;
            }

            _indicatorFill = value;
            IsTemplateApplied = false;
        }
    }

    public CompositionColor IndicatorBorder
    {
        get => _indicatorBorder;
        set
        {
            if (_indicatorBorder.Equals(value))
            {
                return;
            }

            _indicatorBorder = value;
            IsTemplateApplied = false;
        }
    }

    public CompositionColor Foreground
    {
        get => _foreground;
        set
        {
            if (_foreground.Equals(value))
            {
                return;
            }

            _foreground = value;
            if (Content is TextBlock block)
            {
                block.Foreground = value;
            }
        }
    }

    public float FontSize
    {
        get => _fontSize;
        set
        {
            if (value <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (Math.Abs(_fontSize - value) < float.Epsilon)
            {
                return;
            }

            _fontSize = value;
            if (Content is TextBlock block)
            {
                block.FontSize = value;
            }
        }
    }

    public string? Text
    {
        get => _text;
        set
        {
            if (string.Equals(_text, value, StringComparison.Ordinal))
            {
                return;
            }

            _text = value;
            if (value is null)
            {
                return;
            }

            Content = new TextBlock
            {
                Text = value,
                FontSize = _fontSize,
                Foreground = _foreground,
            };
        }
    }

    protected override CompositionElement? BuildContent()
    {
        var label = base.BuildContent();
        var panel = new Panel
        {
            Orientation = LayoutOrientation.Horizontal,
            Spacing = 6,
            Padding = _padding,
            CrossAlignment = LayoutAlignment.Center,
        };

        var indicator = new Rectangle
        {
            Width = 14,
            Height = 14,
            Stroke = _indicatorBorder,
            StrokeThickness = 1.0,
            Fill = _isChecked == true ? _indicatorFill : default,
        };

        panel.Children.Add(indicator);

        if (label is null && _text is not null)
        {
            label = new TextBlock
            {
                Text = _text,
                Foreground = _foreground,
                FontSize = _fontSize,
            };
        }

        if (label is not null)
        {
            panel.Children.Add(label);
        }

        return panel;
    }
}
