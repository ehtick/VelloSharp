using System;

namespace VelloSharp;

public sealed class ImageBrush : Brush
{
    public ImageBrush(Image image)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
    }

    public Image Image { get; }
    public ExtendMode XExtend { get; set; } = ExtendMode.Pad;
    public ExtendMode YExtend { get; set; } = ExtendMode.Pad;
    public ImageQuality Quality { get; set; } = ImageQuality.Medium;
    public float Alpha { get; set; } = 1f;

    internal VelloImageBrushParams ToNative() => new()
    {
        Image = Image.Handle,
        XExtend = (VelloExtendMode)XExtend,
        YExtend = (VelloExtendMode)YExtend,
        Quality = (VelloImageQualityMode)Quality,
        Alpha = Alpha,
    };
}
