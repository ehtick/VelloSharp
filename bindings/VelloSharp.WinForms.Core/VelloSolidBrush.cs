using System.Drawing;
using VelloSharp;

namespace VelloSharp.WinForms;

public sealed class VelloSolidBrush : VelloBrush
{
    public VelloSolidBrush(Color color)
    {
        Color = color;
    }

    public Color Color { get; set; }

    protected override Brush CreateCoreBrushCore()
    {
        return new SolidColorBrush(VelloColorHelpers.ToRgba(Color));
    }
}
