using System;
using System.Drawing.Drawing2D;
using VelloSharp;

namespace VelloSharp.WinForms;

public sealed class VelloTextureBrush : VelloBrush
{
    public VelloTextureBrush(VelloBitmap bitmap)
    {
        Bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
    }

    public VelloTextureBrush(VelloSharp.Image image)
    {
        Bitmap = VelloBitmap.Wrap(image ?? throw new ArgumentNullException(nameof(image)));
    }

    public VelloBitmap Bitmap { get; }

    public WrapMode WrapMode { get; set; } = WrapMode.Tile;

    public ImageQuality Quality { get; set; } = ImageQuality.Medium;

    public float Opacity { get; set; } = 1f;

    protected override Brush CreateCoreBrushCore()
    {
        var brush = new ImageBrush(Bitmap.Image)
        {
            Alpha = Math.Clamp(Opacity, 0f, 1f),
            Quality = Quality,
        };

        var (xExtend, yExtend) = ConvertWrapMode(WrapMode);
        brush.XExtend = xExtend;
        brush.YExtend = yExtend;
        return brush;
    }

    private static (ExtendMode X, ExtendMode Y) ConvertWrapMode(WrapMode wrapMode) => wrapMode switch
    {
        WrapMode.Clamp => (ExtendMode.Pad, ExtendMode.Pad),
        WrapMode.Tile => (ExtendMode.Repeat, ExtendMode.Repeat),
        WrapMode.TileFlipX => (ExtendMode.Reflect, ExtendMode.Repeat),
        WrapMode.TileFlipY => (ExtendMode.Repeat, ExtendMode.Reflect),
        WrapMode.TileFlipXY => (ExtendMode.Reflect, ExtendMode.Reflect),
        _ => (ExtendMode.Repeat, ExtendMode.Repeat),
    };
}
