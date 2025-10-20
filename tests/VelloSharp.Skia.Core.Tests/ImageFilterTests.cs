using System;
using System.Linq;
using SkiaSharp;
using VelloSharp;
using VelloSharp.Ffi.Gpu;
using Xunit;

namespace VelloSharp.Skia.Core.Tests;

public sealed class ImageFilterTests
{
    [Fact]
    public void CreateBlurFilterExportsNativeHandle()
    {
        using var filter = SKImageFilter.CreateBlur(2.5f, 3.25f);
        Assert.True(filter.TryGetBlur(out var managed));

        using var handle = filter.CloneNativeHandle();
        Assert.Equal(VelloFilterKind.Blur, handle.GetKind());

        var native = handle.GetBlur();
        Assert.Equal(managed.SigmaX, native.SigmaX, precision: 6);
        Assert.Equal(managed.SigmaY, native.SigmaY, precision: 6);
    }

    [Fact]
    public void CreateDropShadowFilterExportsNativeHandle()
    {
        var color = new SKColor(16, 32, 48, 128);
        using var filter = SKImageFilter.CreateDropShadow(4f, 6f, 3f, 5f, color);
        Assert.True(filter.TryGetDropShadow(out var managed));

        using var handle = filter.CloneNativeHandle();
        Assert.Equal(VelloFilterKind.DropShadow, handle.GetKind());

        var native = handle.GetDropShadow();
        Assert.Equal(managed.Dx, (float)native.Offset.X, precision: 6);
        Assert.Equal(managed.Dy, (float)native.Offset.Y, precision: 6);
        Assert.Equal(managed.SigmaX, native.SigmaX, precision: 6);
        Assert.Equal(managed.SigmaY, native.SigmaY, precision: 6);
        Assert.Equal(color.Red / 255f, native.Color.R, precision: 6);
        Assert.Equal(color.Green / 255f, native.Color.G, precision: 6);
        Assert.Equal(color.Blue / 255f, native.Color.B, precision: 6);
        Assert.Equal(color.Alpha / 255f, native.Color.A, precision: 6);
    }

    [Fact]
    public void CreateBlendModeFilterExportsNativeHandle()
    {
        using var filter = SKImageFilter.CreateBlendMode(SKBlendMode.Multiply, background: null);
        Assert.True(filter.TryGetBlend(out var managed));

        using var handle = filter.CloneNativeHandle();
        Assert.Equal(VelloFilterKind.Blend, handle.GetKind());

        var native = handle.GetBlend();
        Assert.Equal(managed.Mix, (LayerMix)native.Mix);
        Assert.Equal(managed.Compose, (LayerCompose)native.Compose);
    }

    [Fact]
    public void CreateColorMatrixFilterExportsNativeHandle()
    {
        var matrix = Enumerable.Range(0, SKColorFilter.ColorMatrixSize)
            .Select(i => i * 0.05f)
            .ToArray();

        using var colorFilter = SKColorFilter.CreateColorMatrix(matrix);
        using var filter = SKImageFilter.CreateColorFilter(colorFilter);

        Span<float> managed = stackalloc float[SKColorFilter.ColorMatrixSize];
        Assert.True(filter.TryCopyColorMatrix(managed));

        using var handle = filter.CloneNativeHandle();
        Assert.Equal(VelloFilterKind.ColorMatrix, handle.GetKind());

        var native = handle.GetColorMatrix();
        Assert.Equal(SKColorFilter.ColorMatrixSize, native.Length);

        for (var i = 0; i < SKColorFilter.ColorMatrixSize; i++)
        {
            Assert.Equal(managed[i], native[i], precision: 6);
        }
    }
}
