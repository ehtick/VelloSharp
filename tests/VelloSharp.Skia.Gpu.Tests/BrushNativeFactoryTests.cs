using System;
using System.Numerics;
using SkiaSharp;
using VelloSharp;
using Xunit;

namespace VelloSharp.Skia.Gpu.Tests;

public class BrushNativeFactoryTests
{
    [Fact]
    public void SolidBrush_ConvertsToNativeSolid()
    {
        var color = RgbaColor.FromBytes(12, 34, 56, 255);
        var brush = new SolidColorBrush(color);

        using var native = BrushNativeFactory.Create(brush);

        Assert.Equal(VelloBrushKind.Solid, native.Brush.Kind);
        Assert.Equal(color.R, native.Brush.Solid.R);
        Assert.Equal(color.G, native.Brush.Solid.G);
        Assert.Equal(color.B, native.Brush.Solid.B);
        Assert.Equal(color.A, native.Brush.Solid.A);
        Assert.True(native.Stops.IsEmpty);
    }

    [Fact]
    public void LinearGradient_ProducesExpectedStops()
    {
        var stops = new[]
        {
            new GradientStop(0f, RgbaColor.FromBytes(255, 0, 0, 255)),
            new GradientStop(1f, RgbaColor.FromBytes(0, 0, 255, 255)),
        };
        var brush = new LinearGradientBrush(Vector2.Zero, Vector2.One, stops);

        using var native = BrushNativeFactory.Create(brush);

        Assert.Equal(VelloBrushKind.LinearGradient, native.Brush.Kind);
        Assert.Equal((nuint)stops.Length, native.Brush.Linear.StopCount);
        Assert.Equal(stops.Length, native.Stops.Length);
        Assert.Equal(stops[0].Offset, native.Stops[0].Offset, 3);
        Assert.Equal(stops[1].Color.B, native.Stops[1].Color.B);
    }
}
