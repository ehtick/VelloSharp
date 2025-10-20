using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace VelloSharp;

internal sealed class KurboPathHandle : SafeHandle
{
    public KurboPathHandle()
        : base(IntPtr.Zero, ownsHandle: true)
    {
    }

    internal static KurboPathHandle Create(nint handle)
    {
        var safeHandle = new KurboPathHandle();
        safeHandle.SetHandle(handle);
        return safeHandle;
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        KurboNativeMethods.kurbo_bez_path_destroy(handle);
        return true;
    }
}

public sealed class KurboPath : IDisposable
{
    private readonly KurboPathHandle _handle;

    public KurboPath()
    {
        var ptr = KurboNativeMethods.kurbo_bez_path_create();
        if (ptr == nint.Zero)
        {
            throw new InvalidOperationException("Failed to create Kurbo path.");
        }

        _handle = KurboPathHandle.Create(ptr);
    }

    public void Dispose()
    {
        _handle.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Clear()
    {
        NativeHelpers.ThrowOnError(KurboNativeMethods.kurbo_bez_path_clear(_handle.DangerousGetHandle()), "kurbo_bez_path_clear");
    }

    public void MoveTo(double x, double y)
    {
        NativeHelpers.ThrowOnError(KurboNativeMethods.kurbo_bez_path_move_to(_handle.DangerousGetHandle(), new KurboPoint(x, y)), "kurbo_bez_path_move_to");
    }

    public void LineTo(double x, double y)
    {
        NativeHelpers.ThrowOnError(KurboNativeMethods.kurbo_bez_path_line_to(_handle.DangerousGetHandle(), new KurboPoint(x, y)), "kurbo_bez_path_line_to");
    }

    public void QuadraticTo(double cx, double cy, double x, double y)
    {
        NativeHelpers.ThrowOnError(
            KurboNativeMethods.kurbo_bez_path_quad_to(
                _handle.DangerousGetHandle(),
                new KurboPoint(cx, cy),
                new KurboPoint(x, y)),
            "kurbo_bez_path_quad_to");
    }

    public void CubicTo(double c1x, double c1y, double c2x, double c2y, double x, double y)
    {
        NativeHelpers.ThrowOnError(
            KurboNativeMethods.kurbo_bez_path_cubic_to(
                _handle.DangerousGetHandle(),
                new KurboPoint(c1x, c1y),
                new KurboPoint(c2x, c2y),
                new KurboPoint(x, y)),
            "kurbo_bez_path_cubic_to");
    }

    public void Close()
    {
        NativeHelpers.ThrowOnError(KurboNativeMethods.kurbo_bez_path_close(_handle.DangerousGetHandle()), "kurbo_bez_path_close");
    }

    public void ApplyAffine(in KurboAffine affine)
    {
        NativeHelpers.ThrowOnError(KurboNativeMethods.kurbo_bez_path_apply_affine(_handle.DangerousGetHandle(), affine), "kurbo_bez_path_apply_affine");
    }

    public void Translate(double dx, double dy)
    {
        var offset = new KurboVec2(dx, dy);
        NativeHelpers.ThrowOnError(KurboNativeMethods.kurbo_bez_path_translate(_handle.DangerousGetHandle(), offset), "kurbo_bez_path_translate");
    }

    public KurboRect GetBounds()
    {
        NativeHelpers.ThrowOnError(KurboNativeMethods.kurbo_bez_path_bounds(_handle.DangerousGetHandle(), out var bounds), "kurbo_bez_path_bounds");
        return bounds;
    }

    public int Count
    {
        get
        {
            NativeHelpers.ThrowOnError(KurboNativeMethods.kurbo_bez_path_len(_handle.DangerousGetHandle(), out var length), "kurbo_bez_path_len");
            return (int)length;
        }
    }

    public KurboPathElement[] GetElements()
    {
        NativeHelpers.ThrowOnError(KurboNativeMethods.kurbo_bez_path_len(_handle.DangerousGetHandle(), out var length), "kurbo_bez_path_len");
        if (length == 0)
        {
            return Array.Empty<KurboPathElement>();
        }

        var elements = new KurboPathElement[length];
        unsafe
        {
            fixed (KurboPathElement* ptr = elements)
            {
                NativeHelpers.ThrowOnError(
                    KurboNativeMethods.kurbo_bez_path_copy_elements(
                        _handle.DangerousGetHandle(),
                        ptr,
                        length,
                        out var written),
                    "kurbo_bez_path_copy_elements");

                if (written != length)
                {
                    Array.Resize(ref elements, (int)written);
                }
            }
        }

        return elements;
    }

    internal nint DangerousGetHandle() => _handle.DangerousGetHandle();
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct KurboAffine
{
    public double M11 { get; }
    public double M12 { get; }
    public double M21 { get; }
    public double M22 { get; }
    public double Dx { get; }
    public double Dy { get; }

    public KurboAffine(double m11, double m12, double m21, double m22, double dx, double dy)
    {
        M11 = m11;
        M12 = m12;
        M21 = m21;
        M22 = m22;
        Dx = dx;
        Dy = dy;
    }

    public Matrix3x2 ToMatrix3x2() => new((float)M11, (float)M12, (float)M21, (float)M22, (float)Dx, (float)Dy);

    public static KurboAffine FromMatrix3x2(in Matrix3x2 matrix) =>
        new(matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32);

    public static KurboAffine Identity
    {
        get
        {
            NativeHelpers.ThrowOnError(KurboNativeMethods.kurbo_affine_identity(out var value), "kurbo_affine_identity");
            return value;
        }
    }

    public KurboAffine Multiply(KurboAffine other)
    {
        NativeHelpers.ThrowOnError(KurboNativeMethods.kurbo_affine_mul(this, other, out var result), "kurbo_affine_mul");
        return result;
    }

    public KurboAffine Invert()
    {
        NativeHelpers.ThrowOnError(KurboNativeMethods.kurbo_affine_invert(this, out var result), "kurbo_affine_invert");
        return result;
    }

    public KurboPoint TransformPoint(KurboPoint point)
    {
        NativeHelpers.ThrowOnError(KurboNativeMethods.kurbo_affine_transform_point(this, point, out var result), "kurbo_affine_transform_point");
        return result;
    }

    public KurboVec2 TransformVector(KurboVec2 vector)
    {
        NativeHelpers.ThrowOnError(KurboNativeMethods.kurbo_affine_transform_vec(this, vector, out var result), "kurbo_affine_transform_vec");
        return result;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct KurboPoint
{
    public double X { get; }
    public double Y { get; }

    public KurboPoint(double x, double y)
    {
        X = x;
        Y = y;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct KurboVec2
{
    public double X { get; }
    public double Y { get; }

    public KurboVec2(double x, double y)
    {
        X = x;
        Y = y;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct KurboRect
{
    public double X0 { get; }
    public double Y0 { get; }
    public double X1 { get; }
    public double Y1 { get; }

    public KurboRect(double x0, double y0, double x1, double y1)
    {
        X0 = x0;
        Y0 = y0;
        X1 = x1;
        Y1 = y1;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct KurboPathElement
{
    public KurboPathVerb Verb;
    private int _padding;
    public double X0;
    public double Y0;
    public double X1;
    public double Y1;
    public double X2;
    public double Y2;
}

public enum KurboPathVerb
{
    MoveTo = 0,
    LineTo = 1,
    QuadTo = 2,
    CubicTo = 3,
    Close = 4,
}

public enum KurboStrokeJoin
{
    Miter = 0,
    Round = 1,
    Bevel = 2,
}

public enum KurboStrokeCap
{
    Butt = 0,
    Round = 1,
    Square = 2,
}

[StructLayout(LayoutKind.Sequential)]
public struct KurboStrokeStyle
{
    public double Width;
    public double MiterLimit;
    public KurboStrokeCap StartCap;
    public KurboStrokeCap EndCap;
    public KurboStrokeJoin Join;
    public double DashOffset;
    public IntPtr DashPattern;
    public nuint DashLength;
}
