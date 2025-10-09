using System;

namespace VelloSharp.Composition.Controls;

public class TextBlock : CompositionElement
{
    private string? _text;
    private float _fontSize = 14f;
    private CompositionColor _foreground = new(0.88f, 0.92f, 0.98f, 1f);

    public virtual string? Text
    {
        get => _text;
        set
        {
            if (string.Equals(_text, value, StringComparison.Ordinal))
            {
                return;
            }

            _text = value;
        }
    }

    public float FontSize
    {
        get => _fontSize;
        set
        {
            if (value <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Font size must be positive.");
            }

            if (Math.Abs(_fontSize - value) < float.Epsilon)
            {
                return;
            }

            _fontSize = value;
        }
    }

    public CompositionColor Foreground
    {
        get => _foreground;
        set => _foreground = value;
    }

    protected virtual string? GetTextForRendering() => _text;

    public override void Measure(in LayoutConstraints constraints)
    {
        base.Measure(constraints);

        var text = GetTextForRendering();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var metrics = CompositionInterop.MeasureLabel(text, _fontSize);
        if (metrics.IsEmpty)
        {
            return;
        }

        double width = metrics.Width;
        double height = metrics.Height;
        width = Math.Clamp(width, constraints.Width.Min, constraints.Width.Max);
        height = Math.Clamp(height, constraints.Height.Min, constraints.Height.Max);
        DesiredSize = new LayoutSize(width, height);
    }
}
