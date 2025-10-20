using System;
using System.Numerics;
using SkiaSharp;
using VelloSharp;
using Xunit;

namespace VelloSharp.Skia.Core.Tests;

public sealed class PathEffectTests
{
    [Fact]
    public void DashEffectProducesNonEmptyFillPath()
    {
        using var source = new SKPath();
        source.MoveTo(0, 0);
        source.LineTo(100, 0);

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4f,
            PathEffect = SKPathEffect.CreateDash(new[] { 10f, 5f }, 0f),
        };

        using var destination = new SKPath();
        var success = paint.GetFillPath(source, destination);

        Assert.True(success);
        Assert.False(destination.IsEmpty);
        Assert.True(destination.ToPathBuilder().Count > source.ToPathBuilder().Count);
    }

    [Fact]
    public void CanvasDrawPathAcceptsDashPathEffect()
    {
        using var scene = new Scene();
        using var canvas = new SKCanvas(scene, 256, 256, ownsScene: false, initialTransform: Matrix3x2.Identity);

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            PathEffect = SKPathEffect.CreateDash(new[] { 5f, 5f }, 0f),
        };

        using var path = new SKPath();
        path.MoveTo(0, 0);
        path.LineTo(50, 0);

        var exception = Record.Exception(() => canvas.DrawPath(path, paint));
        Assert.Null(exception);
    }
}
