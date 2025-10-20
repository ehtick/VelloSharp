using System;
using System.Buffers;
using System.Runtime.InteropServices;
using VelloSharp;

namespace SkiaSharp;

internal static class KurboPathEffects
{
    private const int StackallocThreshold = 128;

    public static SKPath ApplyDash(SKPath source, ReadOnlySpan<double> intervals, double phase)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (intervals.IsEmpty)
        {
            return new SKPath(source);
        }

        var builder = source.ToPathBuilder();
        var elements = builder.AsSpan();
        if (elements.IsEmpty)
        {
            return new SKPath(source);
        }

        var nativeElements = elements.AsKurboSpan();

        nint inputHandle;
        unsafe
        {
            fixed (KurboPathElement* elementPtr = nativeElements)
            {
                inputHandle = KurboNativeMethods.kurbo_bez_path_from_elements(elementPtr, (nuint)nativeElements.Length);
            }
        }

        NativeHelpers.ThrowIfNull(inputHandle, "kurbo_bez_path_from_elements failed", KurboNativeMethods.kurbo_last_error_message);

        try
        {
            SKPath? result = null;
            unsafe
            {
                fixed (double* dashPtr = intervals)
                {
                    nint outputHandle;
                    var status = KurboNativeMethods.kurbo_bez_path_dash(
                        inputHandle,
                        phase,
                        dashPtr,
                        (nuint)intervals.Length,
                        out outputHandle);
                    NativeHelpers.ThrowOnError(status, "kurbo_bez_path_dash failed");
                    NativeHelpers.ThrowIfNull(outputHandle, "kurbo_bez_path_dash returned null", KurboNativeMethods.kurbo_last_error_message);

                    try
                    {
                        result = CreatePathFromHandle(outputHandle, source.FillType);
                    }
                    finally
                    {
                        KurboNativeMethods.kurbo_bez_path_destroy(outputHandle);
                    }
                }
            }

            return result ?? new SKPath(source);
        }
        finally
        {
            KurboNativeMethods.kurbo_bez_path_destroy(inputHandle);
        }
    }

    internal static SKPath CreatePathFromHandle(nint handle, SKPathFillType fillType)
    {
        NativeHelpers.ThrowOnError(KurboNativeMethods.kurbo_bez_path_len(handle, out var length), "kurbo_bez_path_len failed");
        if (length == 0)
        {
            return new SKPath
            {
                FillType = fillType,
            };
        }

        Span<KurboPathElement> span = length <= StackallocThreshold
            ? stackalloc KurboPathElement[(int)length]
            : default;

        KurboPathElement[]? rented = null;
        if (span.IsEmpty)
        {
            rented = ArrayPool<KurboPathElement>.Shared.Rent((int)length);
            span = rented.AsSpan(0, (int)length);
        }

        try
        {
            nuint written;
            unsafe
            {
                fixed (KurboPathElement* ptr = span)
                {
                    NativeHelpers.ThrowOnError(
                        KurboNativeMethods.kurbo_bez_path_copy_elements(handle, ptr, (nuint)span.Length, out written),
                        "kurbo_bez_path_copy_elements failed");
                }
            }

            var valid = span[..checked((int)written)];
            return CreatePath(valid, fillType);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<KurboPathElement>.Shared.Return(rented);
            }
        }
    }

    private static SKPath CreatePath(ReadOnlySpan<KurboPathElement> elements, SKPathFillType fillType)
    {
        var path = new SKPath
        {
            FillType = fillType,
        };

        foreach (var element in elements)
        {
            switch ((PathVerb)element.Verb)
            {
                case PathVerb.MoveTo:
                    path.MoveTo((float)element.X0, (float)element.Y0);
                    break;
                case PathVerb.LineTo:
                    path.LineTo((float)element.X0, (float)element.Y0);
                    break;
                case PathVerb.QuadTo:
                    path.QuadTo(
                        new SKPoint((float)element.X0, (float)element.Y0),
                        new SKPoint((float)element.X1, (float)element.Y1));
                    break;
                case PathVerb.CubicTo:
                    path.CubicTo(
                        new SKPoint((float)element.X0, (float)element.Y0),
                        new SKPoint((float)element.X1, (float)element.Y1),
                        new SKPoint((float)element.X2, (float)element.Y2));
                    break;
                case PathVerb.Close:
                    path.Close();
                    break;
            }
        }

        return path;
    }
}
