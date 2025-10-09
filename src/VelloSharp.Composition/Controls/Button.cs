using System;

namespace VelloSharp.Composition.Controls;

public class Button : ContentControl
{
    private CompositionColor _background = new(0.23f, 0.29f, 0.41f, 0.95f);
    private CompositionColor _borderBrush = new(0.35f, 0.43f, 0.58f, 1f);
    private CompositionColor _foreground = new(0.90f, 0.95f, 0.99f, 1f);
    private LayoutThickness _borderThickness = new(1, 1, 1, 1);
    private LayoutThickness _padding = new(12, 6, 12, 6);
    private double _cornerRadius = 4.0;
    private float _fontSize = 14f;
    private bool _isEnabled = true;
    private bool _isPressed;
    private string? _text;

    public CompositionColor Background
    {
        get => _background;
        set
        {
            if (_background.Equals(value))
            {
                return;
            }

            _background = value;
            IsTemplateApplied = false;
        }
    }

    public CompositionColor BorderBrush
    {
        get => _borderBrush;
        set
        {
            if (_borderBrush.Equals(value))
            {
                return;
            }

            _borderBrush = value;
            IsTemplateApplied = false;
        }
    }

    public LayoutThickness BorderThickness
    {
        get => _borderThickness;
        set
        {
            if (_borderThickness.Equals(value))
            {
                return;
            }

            _borderThickness = value;
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

    public double CornerRadius
    {
        get => _cornerRadius;
        set
        {
            if (Math.Abs(_cornerRadius - value) < double.Epsilon)
            {
                return;
            }

            _cornerRadius = Math.Max(0, value);
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
            if (Content is TextBlock textBlock)
            {
                textBlock.Foreground = value;
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
            if (Content is TextBlock textBlock)
            {
                textBlock.FontSize = value;
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

    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    public bool IsPressed
    {
        get => _isPressed;
        set => _isPressed = value;
    }

    protected override CompositionElement? BuildContent()
    {
        var inner = base.BuildContent();
        if (inner is null)
        {
            return null;
        }

        return new Border
        {
            Background = _background,
            BorderBrush = _borderBrush,
            BorderThickness = _borderThickness,
            Padding = _padding,
            CornerRadius = _cornerRadius,
            Child = inner,
        };
    }
}
