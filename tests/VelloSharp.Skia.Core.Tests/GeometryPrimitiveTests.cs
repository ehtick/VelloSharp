using System.Linq;
using SkiaSharp;
using Xunit;

namespace VelloSharp.Skia.Core.Tests;

public sealed class GeometryPrimitiveTests
{
    [Fact]
    public void SKPoint_AllowsMutation()
    {
        var point = new SKPoint(1, 2);

        point.X = 3;
        point.Y = 4;

        Assert.Equal(3f, point.X);
        Assert.Equal(4f, point.Y);
    }

    [Fact]
    public void SKRect_InflateExpandsBounds()
    {
        var rect = new SKRect(0, 0, 10, 10);

        rect.Inflate(5, 2);

        Assert.Equal(-5f, rect.Left);
        Assert.Equal(-2f, rect.Top);
        Assert.Equal(15f, rect.Right);
        Assert.Equal(12f, rect.Bottom);
    }

    [Fact]
    public void SKRect_OffsetMovesRect()
    {
        var rect = SKRect.Create(0, 0, 5, 5);

        rect.Offset(2, -3);

        Assert.Equal(2f, rect.Left);
        Assert.Equal(-3f, rect.Top);
        Assert.Equal(7f, rect.Right);
        Assert.Equal(2f, rect.Bottom);
    }

    [Fact]
    public void SKRect_UnionExtendsOutward()
    {
        var rect = new SKRect(0, 0, 10, 10);

        rect.Union(new SKRect(5, -2, 12, 8));

        Assert.Equal(0f, rect.Left);
        Assert.Equal(-2f, rect.Top);
        Assert.Equal(12f, rect.Right);
        Assert.Equal(10f, rect.Bottom);
    }

    [Fact]
    public void SKRect_DeflateShrinks()
    {
        var rect = new SKRect(-5, -5, 5, 5);

        rect.Deflate(1, 2);

        Assert.Equal(-4f, rect.Left);
        Assert.Equal(-3f, rect.Top);
        Assert.Equal(4f, rect.Right);
        Assert.Equal(3f, rect.Bottom);
    }

    [Fact]
    public void SKRoundRect_SetRectRadiiCopiesValues()
    {
        var rect = SKRect.Create(0, 0, 10, 10);
        var radii = new[]
        {
            new SKPoint(1, 2),
            new SKPoint(3, 4),
            new SKPoint(5, 6),
            new SKPoint(7, 8),
        };

        using var roundRect = new SKRoundRect();

        roundRect.SetRectRadii(rect, radii);

        radii[0] = new SKPoint(9, 9);

        var stored = roundRect.Radii;

        Assert.Equal(rect, roundRect.Rect);
        Assert.Equal(new SKPoint(1, 2), stored[0]);
        Assert.Equal(new SKPoint(7, 8), stored[3]);
    }

    [Fact]
    public void SKRoundRect_InflateModifiesRect()
    {
        using var roundRect = new SKRoundRect(SKRect.Create(10, 10, 20, 20));

        roundRect.Inflate(2, 3);

        var rect = roundRect.Rect;
        Assert.Equal(8f, rect.Left);
        Assert.Equal(7f, rect.Top);
        Assert.Equal(32f, rect.Right);
        Assert.Equal(33f, rect.Bottom);
    }

    [Fact]
    public void SKRoundRect_DeflateModifiesRect()
    {
        using var roundRect = new SKRoundRect(SKRect.Create(-5, -5, 15, 15));

        roundRect.Deflate(2, 1);

        var rect = roundRect.Rect;
        Assert.Equal(-3f, rect.Left);
        Assert.Equal(-4f, rect.Top);
        Assert.Equal(8f, rect.Right);
        Assert.Equal(9f, rect.Bottom);
    }

    [Fact]
    public void SKRoundRect_SetEmptyResetsState()
    {
        using var roundRect = new SKRoundRect(SKRect.Create(0, 0, 5, 5));
        roundRect.SetRectRadii(roundRect.Rect, new[]
        {
            new SKPoint(1, 1),
            new SKPoint(1, 1),
            new SKPoint(1, 1),
            new SKPoint(1, 1),
        });

        roundRect.SetEmpty();

        Assert.True(roundRect.IsEmpty);
       Assert.Equal(default, roundRect.Rect);
       Assert.True(roundRect.Radii.All(r => r == default));
    }

    [Fact]
    public void SKPath_CopyConstructorClonesGeometry()
    {
        var path = new SKPath();
        path.MoveTo(0, 0);
        path.LineTo(10, 0);
        path.LineTo(10, 10);
        path.Close();

        var clone = new SKPath(path);

        path.LineTo(20, 20);

        Assert.Equal(new SKRect(0, 0, 10, 10), clone.TightBounds);
        Assert.Equal(SKPathFillType.Winding, clone.FillType);
    }

    [Fact]
    public void SKPath_AddRectProducesMatchingBounds()
    {
        var path = new SKPath();
        var rect = SKRect.Create(-5, -2, 10, 4);

        path.AddRect(rect);

        Assert.Equal(rect, path.TightBounds);
    }

    [Fact]
    public void SKPath_AddOvalProducesMatchingBounds()
    {
        var path = new SKPath();
        var rect = SKRect.Create(2, 4, 16, 10);

        path.AddOval(rect, SKPathDirection.CounterClockwise);

        Assert.Equal(rect, path.TightBounds);
    }

    [Fact]
    public void SKPath_AddPathWithOffsetTranslatesGeometry()
    {
        var basePath = new SKPath();
        basePath.MoveTo(0, 0);
        basePath.LineTo(5, 5);

        var path = new SKPath();
        path.AddPath(basePath, 10, -2);

        var bounds = path.TightBounds;
        Assert.Equal(10f, bounds.Left);
        Assert.Equal(-2f, bounds.Top);
        Assert.Equal(15f, bounds.Right);
        Assert.Equal(3f, bounds.Bottom);
    }

    [Fact]
    public void SKPath_FloatOverloadsAppendCommands()
    {
        var path = new SKPath();
        path.MoveTo(0, 0);
        path.QuadTo(1, 2, 3, 4);
        path.CubicTo(4, 5, 6, 7, 8, 9);

        var bounds = path.TightBounds;
        Assert.Equal(0f, bounds.Left);
        Assert.Equal(0f, bounds.Top);
        Assert.Equal(8f, bounds.Right);
        Assert.Equal(9f, bounds.Bottom);
    }

    [Fact]
    public void SKPath_ArcToProducesFiniteGeometry()
    {
        using var path = new SKPath();
        path.MoveTo(-25f, -10f);
        path.ArcTo(10f, 10f, 0f, SKPathArcSize.Small, SKPathDirection.Clockwise, 25f, -10f);

        var bounds = path.TightBounds;

        Assert.False(float.IsNaN(bounds.Left) || float.IsNaN(bounds.Top) || float.IsNaN(bounds.Right) || float.IsNaN(bounds.Bottom));
        Assert.False(float.IsInfinity(bounds.Left) || float.IsInfinity(bounds.Top) || float.IsInfinity(bounds.Right) || float.IsInfinity(bounds.Bottom));
        Assert.True(bounds.Left <= bounds.Right);
        Assert.True(bounds.Top <= bounds.Bottom);

        using var clone = new SKPath(path);
        var cloneBounds = clone.TightBounds;
        Assert.Equal(bounds, cloneBounds);
    }

    [Fact]
    public void SKPath_ArcToHandlesDegenerateInputs()
    {
        using var path = new SKPath();
        path.MoveTo(0f, 0f);

        path.ArcTo(0f, 0f, 0f, SKPathArcSize.Small, SKPathDirection.Clockwise, 10f, 0f);

        var bounds = path.TightBounds;
        Assert.Equal(0f, bounds.Top);
        Assert.Equal(0f, bounds.Bottom);
        Assert.Equal(0f, bounds.Left);
        Assert.Equal(10f, bounds.Right);
    }
}
