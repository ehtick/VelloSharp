using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Numerics;

namespace VelloSharp.WinForms;

public readonly struct VelloGraphicsState
{
    internal VelloGraphicsState(
        int stateId,
        Matrix3x2 transform,
        int stackIndex,
        int layerDepth,
        SmoothingMode smoothingMode,
        PixelOffsetMode pixelOffsetMode,
        InterpolationMode interpolationMode,
        CompositingMode compositingMode,
        CompositingQuality compositingQuality,
        TextRenderingHint textRenderingHint,
        VelloRegion? clip)
    {
        StateId = stateId;
        Transform = transform;
        StackIndex = stackIndex;
        LayerDepth = layerDepth;
        SmoothingMode = smoothingMode;
        PixelOffsetMode = pixelOffsetMode;
        InterpolationMode = interpolationMode;
        CompositingMode = compositingMode;
        CompositingQuality = compositingQuality;
        TextRenderingHint = textRenderingHint;
        Clip = clip;
    }

    internal int StateId { get; }

    internal Matrix3x2 Transform { get; }

    internal int StackIndex { get; }

    internal int LayerDepth { get; }

    internal SmoothingMode SmoothingMode { get; }

    internal PixelOffsetMode PixelOffsetMode { get; }

    internal InterpolationMode InterpolationMode { get; }

    internal CompositingMode CompositingMode { get; }

    internal CompositingQuality CompositingQuality { get; }

    internal TextRenderingHint TextRenderingHint { get; }

    internal VelloRegion? Clip { get; }
}
