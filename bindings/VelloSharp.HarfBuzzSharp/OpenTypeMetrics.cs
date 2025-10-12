using System;
using System.Buffers;
using System.Collections.Generic;

namespace HarfBuzzSharp;

public sealed class OpenTypeMetrics
{
    private readonly Font _font;

    internal OpenTypeMetrics(Font font)
    {
        _font = font ?? throw new ArgumentNullException(nameof(font));
    }

    public bool TryGetPosition(OpenTypeMetricsTag metricsTag, out int position)
    {
        position = 0;

        if (_font.Handle == IntPtr.Zero)
        {
            return false;
        }

        _font.GetScale(out var scaleX, out var scaleY);
        var variations = _font.GetVariationOptions();
        var rented = PrepareVariations(variations, out var span);

        try
        {
            unsafe
            {
                fixed (global::VelloSharp.VelloVariationAxisValueNative* variationPtr = span)
                {
                    var status = global::VelloSharp.NativeMethods.vello_font_get_ot_metric(
                        _font.Handle,
                        (uint)metricsTag,
                        scaleX,
                        scaleY,
                        variationPtr,
                        (nuint)span.Length,
                        out var nativePosition);

                    if (status != global::VelloSharp.VelloStatus.Success)
                    {
                        return false;
                    }

                    position = nativePosition;
                    return true;
                }
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<global::VelloSharp.VelloVariationAxisValueNative>.Shared.Return(rented);
            }
        }
    }

    public float GetVariation(OpenTypeMetricsTag metricsTag)
    {
        if (_font.Handle == IntPtr.Zero)
        {
            return 0f;
        }

        var variations = _font.GetVariationOptions();
        var rented = PrepareVariations(variations, out var span);

        try
        {
            unsafe
            {
                fixed (global::VelloSharp.VelloVariationAxisValueNative* variationPtr = span)
                {
                    var status = global::VelloSharp.NativeMethods.vello_font_get_ot_variation(
                        _font.Handle,
                        (uint)metricsTag,
                        variationPtr,
                        (nuint)span.Length,
                        out var delta);

                    return status == global::VelloSharp.VelloStatus.Success ? delta : 0f;
                }
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<global::VelloSharp.VelloVariationAxisValueNative>.Shared.Return(rented);
            }
        }
    }

    public int GetXVariation(OpenTypeMetricsTag metricsTag)
    {
        if (_font.Handle == IntPtr.Zero)
        {
            return 0;
        }

        _font.GetScale(out var scaleX, out _);
        var variations = _font.GetVariationOptions();
        var rented = PrepareVariations(variations, out var span);

        try
        {
            unsafe
            {
                fixed (global::VelloSharp.VelloVariationAxisValueNative* variationPtr = span)
                {
                    var status = global::VelloSharp.NativeMethods.vello_font_get_ot_variation_x(
                        _font.Handle,
                        (uint)metricsTag,
                        scaleX,
                        variationPtr,
                        (nuint)span.Length,
                        out var delta);

                    return status == global::VelloSharp.VelloStatus.Success ? delta : 0;
                }
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<global::VelloSharp.VelloVariationAxisValueNative>.Shared.Return(rented);
            }
        }
    }

    public int GetYVariation(OpenTypeMetricsTag metricsTag)
    {
        if (_font.Handle == IntPtr.Zero)
        {
            return 0;
        }

        _font.GetScale(out _, out var scaleY);
        var variations = _font.GetVariationOptions();
        var rented = PrepareVariations(variations, out var span);

        try
        {
            unsafe
            {
                fixed (global::VelloSharp.VelloVariationAxisValueNative* variationPtr = span)
                {
                    var status = global::VelloSharp.NativeMethods.vello_font_get_ot_variation_y(
                        _font.Handle,
                        (uint)metricsTag,
                        scaleY,
                        variationPtr,
                        (nuint)span.Length,
                        out var delta);

                    return status == global::VelloSharp.VelloStatus.Success ? delta : 0;
                }
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<global::VelloSharp.VelloVariationAxisValueNative>.Shared.Return(rented);
            }
        }
    }

    private static global::VelloSharp.VelloVariationAxisValueNative[]? PrepareVariations(
        IReadOnlyList<global::VelloSharp.Text.VelloVariationAxisValue>? axes,
        out ReadOnlySpan<global::VelloSharp.VelloVariationAxisValueNative> span)
    {
        if (axes is not { Count: > 0 })
        {
            span = ReadOnlySpan<global::VelloSharp.VelloVariationAxisValueNative>.Empty;
            return null;
        }

        var buffer = ArrayPool<global::VelloSharp.VelloVariationAxisValueNative>.Shared.Rent(axes.Count);
        var slice = buffer.AsSpan(0, axes.Count);
        for (var i = 0; i < axes.Count; i++)
        {
            slice[i] = new global::VelloSharp.VelloVariationAxisValueNative
            {
                Tag = EncodeTag(axes[i].Tag),
                Value = axes[i].Value,
            };
        }

        span = slice;
        return buffer;
    }

    private static uint EncodeTag(string tag)
    {
        if (tag.Length != 4)
        {
            throw new ArgumentException("OpenType tags must be exactly four characters long.", nameof(tag));
        }

        return ((uint)tag[0] << 24) | ((uint)tag[1] << 16) | ((uint)tag[2] << 8) | tag[3];
    }
}
