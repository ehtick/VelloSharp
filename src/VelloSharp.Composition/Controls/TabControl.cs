using System;
using System.Collections.Generic;

namespace VelloSharp.Composition.Controls;

public class TabControl : ContentControl
{
    private readonly List<TabItem> _items = new();
    private int _selectedIndex;
    private LayoutThickness _padding = new(8, 4, 8, 4);
    private CompositionColor _background = new(0.14f, 0.17f, 0.24f, 0.95f);
    private CompositionColor _tabBackground = new(0.18f, 0.22f, 0.30f, 0.95f);
    private CompositionColor _tabForeground = new(0.88f, 0.93f, 0.98f, 1f);
    private CompositionColor _tabInactiveForeground = new(0.60f, 0.66f, 0.74f, 1f);
    private CompositionColor _borderBrush = new(0.28f, 0.34f, 0.48f, 1f);
    private LayoutThickness _borderThickness = new(1, 1, 1, 1);
    private float _fontSize = 14f;

    public IList<TabItem> Items => _items;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var coerced = Math.Clamp(value, 0, Math.Max(0, _items.Count - 1));
            if (_selectedIndex == coerced)
            {
                return;
            }

            _selectedIndex = coerced;
            IsTemplateApplied = false;
        }
    }

    public TabItem? SelectedItem =>
        (_selectedIndex >= 0 && _selectedIndex < _items.Count) ? _items[_selectedIndex] : null;

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

    public CompositionColor TabBackground
    {
        get => _tabBackground;
        set
        {
            if (_tabBackground.Equals(value))
            {
                return;
            }

            _tabBackground = value;
            IsTemplateApplied = false;
        }
    }

    public CompositionColor TabForeground
    {
        get => _tabForeground;
        set
        {
            if (_tabForeground.Equals(value))
            {
                return;
            }

            _tabForeground = value;
            IsTemplateApplied = false;
        }
    }

    public CompositionColor TabInactiveForeground
    {
        get => _tabInactiveForeground;
        set
        {
            if (_tabInactiveForeground.Equals(value))
            {
                return;
            }

            _tabInactiveForeground = value;
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
            IsTemplateApplied = false;
        }
    }

    protected override CompositionElement? BuildContent()
    {
        var root = new Panel
        {
            Orientation = LayoutOrientation.Vertical,
            Spacing = 6,
            Padding = _padding,
        };

        if (_items.Count > 0)
        {
            var tabStrip = new Panel
            {
                Orientation = LayoutOrientation.Horizontal,
                Spacing = 4,
                CrossAlignment = LayoutAlignment.Center,
            };

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                var headerBorder = new Border
                {
                    Background = i == _selectedIndex ? _tabBackground : new CompositionColor(0.12f, 0.15f, 0.21f, 0.85f),
                    BorderBrush = _borderBrush,
                    BorderThickness = new LayoutThickness(1, 1, 1, i == _selectedIndex ? 0 : 1),
                    Padding = new LayoutThickness(10, 6, 10, 6),
                    Child = new TextBlock
                    {
                        Text = item.Header,
                        FontSize = _fontSize,
                        Foreground = i == _selectedIndex ? _tabForeground : _tabInactiveForeground,
                    },
                };

                tabStrip.Children.Add(headerBorder);
            }

            root.Children.Add(tabStrip);
        }

        var content = SelectedItem?.Content ?? base.BuildContent();
        if (content is not null)
        {
            var contentBorder = new Border
            {
                Background = _background,
                BorderBrush = _borderBrush,
                BorderThickness = _borderThickness,
                Child = content,
            };
            root.Children.Add(contentBorder);
        }

        return root;
    }
}

public sealed class TabItem
{
    public string Header { get; set; } = string.Empty;

    public CompositionElement? Content { get; set; }
}
