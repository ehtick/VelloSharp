using System;
using Avalonia.Media;
using VelloSharp.Avalonia.Vello.Geometry;

namespace VelloSharp.Avalonia.Vello;

internal sealed class VelloPathGeometryImpl : VelloGeometryImplBase
{
    public VelloPathGeometryImpl(VelloPathData data, global::Avalonia.Media.FillRule fillRule = global::Avalonia.Media.FillRule.NonZero)
        : base(data ?? throw new ArgumentNullException(nameof(data)), fillRule)
    {
    }
}
