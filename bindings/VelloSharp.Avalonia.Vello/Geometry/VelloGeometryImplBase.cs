using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using VelloSharp.Avalonia.Vello.Geometry;

namespace VelloSharp.Avalonia.Vello;

internal abstract class VelloGeometryImplBase : IGeometryImpl
{
    protected VelloGeometryImplBase(VelloPathData data, global::Avalonia.Media.FillRule fillRule = global::Avalonia.Media.FillRule.NonZero)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        FillRule = fillRule;
    }

    protected VelloPathData Data { get; private set; }

    protected global::Avalonia.Media.FillRule FillRule { get; private set; }

    internal global::Avalonia.Media.FillRule EffectiveFillRule => FillRule;

    protected void ReplaceData(VelloPathData data)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    protected void SetFillRule(global::Avalonia.Media.FillRule fillRule)
    {
        FillRule = fillRule;
    }

    public virtual Rect Bounds => Data.Bounds;

    public virtual double ContourLength => Data.ContourLength;

    public virtual Rect GetRenderBounds(IPen? pen)
    {
        if (pen is null)
        {
            return Bounds;
        }

        var inflate = pen.Thickness / 2;
        return Bounds.Inflate(inflate);
    }

    public virtual IGeometryImpl GetWidenedGeometry(IPen pen) => this;

    public virtual bool FillContains(Point point) => Bounds.ContainsExclusive(point);

    public virtual IGeometryImpl? Intersect(IGeometryImpl geometry) => null;

    public virtual bool StrokeContains(IPen? pen, Point point)
    {
        if (pen is null)
        {
            return false;
        }

        var outer = Bounds.Inflate(pen.Thickness / 2);
        if (!outer.ContainsExclusive(point))
        {
            return false;
        }

        var inner = Bounds.Deflate(pen.Thickness / 2);
        return !inner.ContainsExclusive(point);
    }

    public virtual ITransformedGeometryImpl WithTransform(Matrix transform)
    {
        return new VelloTransformedGeometryImpl(this, transform);
    }

    public virtual bool TryGetPointAtDistance(double distance, out Point point)
    {
        point = default;
        return false;
    }

    public virtual bool TryGetPointAndTangentAtDistance(double distance, out Point point, out Point tangent)
    {
        point = default;
        tangent = default;
        return false;
    }

    public virtual bool TryGetSegment(
        double startDistance,
        double stopDistance,
        bool startOnBeginFigure,
        [NotNullWhen(true)] out IGeometryImpl? segmentGeometry)
    {
        segmentGeometry = null;
        return false;
    }

    internal VelloPathData.PathCommand[] GetCommandsSnapshot() => Data.Commands.ToArray();
}
