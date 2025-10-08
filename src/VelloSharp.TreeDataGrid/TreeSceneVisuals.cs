using System;

namespace VelloSharp.TreeDataGrid;

public readonly record struct TreeColor(float R, float G, float B, float A)
{
    public static TreeColor FromRgb(float r, float g, float b, float a = 1f)
        => new(r, g, b, a);

    internal NativeMethods.VelloTdgColor ToNative()
        => new()
        {
            R = R,
            G = G,
            B = B,
            A = A,
        };
}

public readonly record struct TreeColumnSpan(double Offset, double Width, TreeFrozenKind Frozen);

public readonly record struct TreeRowVisual(
    double Width,
    double Height,
    uint Depth,
    double Indent,
    TreeColor Background,
    TreeColor HoverBackground,
    TreeColor SelectionFill,
    TreeColor Outline,
    float OutlineWidth,
    TreeColor Stripe,
    float StripeWidth,
    bool IsSelected,
    bool IsHovered)
{
    internal NativeMethods.VelloTdgRowVisual ToNative()
        => new()
        {
            Width = Width,
            Height = Height,
            Depth = Depth,
            Indent = Indent,
            Background = Background.ToNative(),
            HoverBackground = HoverBackground.ToNative(),
            SelectionFill = SelectionFill.ToNative(),
            Outline = Outline.ToNative(),
            OutlineWidth = OutlineWidth,
            Stripe = Stripe.ToNative(),
            StripeWidth = StripeWidth,
            IsSelected = IsSelected ? 1u : 0u,
            IsHovered = IsHovered ? 1u : 0u,
        };
}

public readonly record struct TreeGroupHeaderVisual(
    double Width,
    double Height,
    uint Depth,
    double Indent,
    TreeColor Background,
    TreeColor Accent,
    TreeColor Outline,
    float OutlineWidth)
{
    internal NativeMethods.VelloTdgGroupHeaderVisual ToNative()
        => new()
        {
            Width = Width,
            Height = Height,
            Depth = Depth,
            Indent = Indent,
            Background = Background.ToNative(),
            Accent = Accent.ToNative(),
            Outline = Outline.ToNative(),
            OutlineWidth = OutlineWidth,
        };
}

public readonly record struct TreeSummaryVisual(
    double Width,
    double Height,
    TreeColor Highlight,
    TreeColor Background,
    TreeColor Outline,
    float OutlineWidth)
{
    internal NativeMethods.VelloTdgSummaryVisual ToNative()
        => new()
        {
            Width = Width,
            Height = Height,
            Highlight = Highlight.ToNative(),
            Background = Background.ToNative(),
            Outline = Outline.ToNative(),
            OutlineWidth = OutlineWidth,
        };
}

public readonly record struct TreeChromeVisual(
    double Width,
    double Height,
    TreeColor GridColor,
    float GridWidth,
    uint FrozenLeading,
    uint FrozenTrailing,
    TreeColor FrozenFill)
{
    internal NativeMethods.VelloTdgRowChromeVisual ToNative()
        => new()
        {
            Width = Width,
            Height = Height,
            GridColor = GridColor.ToNative(),
            GridWidth = GridWidth,
            FrozenLeading = FrozenLeading,
            FrozenTrailing = FrozenTrailing,
            FrozenFill = FrozenFill.ToNative(),
        };
}
