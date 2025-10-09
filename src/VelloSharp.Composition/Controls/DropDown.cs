using System;
using System.Collections.Generic;

namespace VelloSharp.Composition.Controls;

public class DropDown : ContentControl
{
    private readonly List<CompositionElement> _items = new();
    private int _selectedIndex = -1;
    private string? _placeholderText;
    private LayoutThickness _padding = new(10, 6, 10, 6);
    private CompositionColor _background = new(0.16f, 0.19f, 0.26f, 0.95f);
    private CompositionColor _borderBrush = new(0.32f, 0.38f, 0.52f, 1f);
    private LayoutThickness _borderThickness = new(1, 1, 1, 1);
    private CompositionColor _foreground = new(0.90f, 0.95f, 0.99f, 1f);
    private CompositionColor _placeholderForeground = new(0.55f, 0.62f, 0.72f, 1f);
    private float _fontSize = 14f;

    public IList<CompositionElement> Items => _items;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var coerced = Math.Clamp(value, -1, _items.Count - 1);
            if (_selectedIndex == coerced)
            {
                return;
            }

            _selectedIndex = coerced;
            IsTemplateApplied = false;
        }
    }

    public CompositionElement? SelectedItem =>
        _selectedIndex >= 0 && _selectedIndex < _items.Count
            ? _items[_selectedIndex]
            : null;

    public string? PlaceholderText
    {
        get => _placeholderText;
        set
        {
            if (string.Equals(_placeholderText, value, StringComparison.Ordinal))
            {
                return;
            }

            _placeholderText = value;
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

    public CompositionColor PlaceholderForeground
    {
        get => _placeholderForeground;
        set
        {
            if (_placeholderForeground.Equals(value))
            {
                return;
            }

            _placeholderForeground = value;
            if (_selectedIndex < 0)
            {
                IsTemplateApplied = false;
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
            IsTemplateApplied = false;
        }
    }

    protected override CompositionElement? BuildContent()
    {
        CompositionElement? display = SelectedItem;
        display ??= base.BuildContent();

        if (display is null && !string.IsNullOrEmpty(_placeholderText))
        {
            display = new TextBlock
            {
                Text = _placeholderText,
                FontSize = _fontSize,
                Foreground = _placeholderForeground,
            };
        }

        if (display is null)
        {
            return null;
        }

        var panel = new Panel
        {
            Orientation = LayoutOrientation.Horizontal,
            Spacing = 8,
            Padding = _padding,
            CrossAlignment = LayoutAlignment.Center,
        };
        panel.Children.Add(display);

        var border = new Border
        {
            Background = _background,
            BorderBrush = _borderBrush,
            BorderThickness = _borderThickness,
            Child = panel,
        };

        return border;
    }
}
