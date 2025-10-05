using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using VelloSharp;

namespace VelloSharp.WinForms;

public sealed class VelloLinearGradientBrush : VelloBrush
{
    private readonly List<GradientStop> _stops = new();

    public VelloLinearGradientBrush(PointF startPoint, PointF endPoint, Color startColor, Color endColor)
    {
        StartPoint = startPoint;
        EndPoint = endPoint;
        AddGradientStop(0f, startColor);
        AddGradientStop(1f, endColor);
    }

    public VelloLinearGradientBrush(PointF startPoint, PointF endPoint, IEnumerable<(float Offset, Color Color)> stops)
    {
        StartPoint = startPoint;
        EndPoint = endPoint;
        ArgumentNullException.ThrowIfNull(stops);
        foreach (var (offset, color) in stops)
        {
            AddGradientStop(offset, color);
        }

        if (_stops.Count < 2)
        {
            throw new ArgumentException("At least two gradient stops are required.", nameof(stops));
        }
    }

    public PointF StartPoint { get; set; }

    public PointF EndPoint { get; set; }

    public ExtendMode ExtendMode { get; set; } = ExtendMode.Pad;

    public IReadOnlyList<GradientStop> GradientStops => _stops;

    public void AddGradientStop(float offset, Color color)
    {
        if (!float.IsFinite(offset))
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var clamped = Math.Clamp(offset, 0f, 1f);
        var rgba = VelloColorHelpers.ToRgba(color);
        InsertOrReplaceStop(clamped, rgba);
    }

    public void ClearStops()
    {
        _stops.Clear();
    }

    private void InsertOrReplaceStop(float offset, RgbaColor color)
    {
        for (var i = 0; i < _stops.Count; i++)
        {
            if (Math.Abs(_stops[i].Offset - offset) < 0.0001f)
            {
                _stops[i] = new GradientStop(offset, color);
                return;
            }

            if (_stops[i].Offset > offset)
            {
                _stops.Insert(i, new GradientStop(offset, color));
                return;
            }
        }

        _stops.Add(new GradientStop(offset, color));
    }

    protected override Brush CreateCoreBrushCore()
    {
        if (_stops.Count == 0)
        {
            throw new InvalidOperationException("At least one gradient stop must be specified.");
        }

        var start = new Vector2(StartPoint.X, StartPoint.Y);
        var end = new Vector2(EndPoint.X, EndPoint.Y);
        return new LinearGradientBrush(start, end, _stops, ExtendMode);
    }
}
