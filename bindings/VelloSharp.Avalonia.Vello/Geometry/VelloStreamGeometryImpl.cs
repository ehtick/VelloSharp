using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using AvaloniaFillRule = Avalonia.Media.FillRule;
using VelloPathBuilder = VelloSharp.PathBuilder;
using VelloSharp.Avalonia.Vello.Geometry;

namespace VelloSharp.Avalonia.Vello;

internal sealed class VelloStreamGeometryImpl : VelloGeometryImplBase, IStreamGeometryImpl
{
    private bool _open;

    public VelloStreamGeometryImpl()
        : base(new VelloPathData())
    {
    }

    private VelloStreamGeometryImpl(VelloPathData data, global::Avalonia.Media.FillRule fillRule)
        : base(data, fillRule)
    {
    }

    public IStreamGeometryImpl Clone() => new VelloStreamGeometryImpl(Data.Clone(), EffectiveFillRule);

    public IStreamGeometryContextImpl Open()
    {
        if (_open)
        {
            throw new InvalidOperationException("Stream geometry is already open.");
        }

        _open = true;
        return new StreamGeometryContext(this);
    }

    private void Complete(VelloPathBuilder builder, AvaloniaFillRule fillRule)
    {
        var data = new VelloPathData();
        data.Append(builder.AsSpan());
        ReplaceData(data);
        SetFillRule(fillRule == AvaloniaFillRule.EvenOdd
            ? global::Avalonia.Media.FillRule.EvenOdd
            : global::Avalonia.Media.FillRule.NonZero);
        _open = false;
    }

    private sealed class StreamGeometryContext : IStreamGeometryContextImpl
    {
        private readonly VelloStreamGeometryImpl _owner;
        private readonly VelloPathBuilder _builder = new();
        private AvaloniaFillRule _fillRule = AvaloniaFillRule.EvenOdd;
        private bool _figureOpen;

        public StreamGeometryContext(VelloStreamGeometryImpl owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            if (_owner._open)
            {
                _owner.Complete(_builder, _fillRule);
            }
        }

        public void ArcTo(Point point, Size size, double rotationAngle, bool isLargeArc, SweepDirection sweepDirection)
        {
            // Approximate the arc using cubic Bezier segments.
            var segments = GeometryUtilities.ArcToBezier(_currentPoint, point, size, rotationAngle, isLargeArc, sweepDirection);
            foreach (var segment in segments)
            {
                _builder.CubicTo(segment.ControlPoint1.X, segment.ControlPoint1.Y,
                    segment.ControlPoint2.X, segment.ControlPoint2.Y,
                    segment.EndPoint.X, segment.EndPoint.Y);
            }

            _currentPoint = point;
        }

        public void BeginFigure(Point startPoint, bool isFilled = true)
        {
            if (_figureOpen)
            {
                EndFigure(false);
            }

            _builder.MoveTo(startPoint.X, startPoint.Y);
            _currentPoint = startPoint;
            _figureOpen = true;
        }

        public void CubicBezierTo(Point controlPoint1, Point controlPoint2, Point endPoint)
        {
            _builder.CubicTo(controlPoint1.X, controlPoint1.Y,
                controlPoint2.X, controlPoint2.Y,
                endPoint.X, endPoint.Y);
            _currentPoint = endPoint;
        }

        public void QuadraticBezierTo(Point controlPoint, Point endPoint)
        {
            _builder.QuadraticTo(controlPoint.X, controlPoint.Y, endPoint.X, endPoint.Y);
            _currentPoint = endPoint;
        }

        public void LineTo(Point endPoint)
        {
            _builder.LineTo(endPoint.X, endPoint.Y);
            _currentPoint = endPoint;
        }

        public void EndFigure(bool isClosed)
        {
            if (!_figureOpen)
            {
                return;
            }

            if (isClosed)
            {
                _builder.Close();
            }

            _figureOpen = false;
        }

        void IGeometryContext.SetFillRule(AvaloniaFillRule fillRule)
        {
            _fillRule = fillRule;
        }

        private Point _currentPoint;
    }
}
