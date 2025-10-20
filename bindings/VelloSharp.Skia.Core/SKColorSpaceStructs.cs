using System;
using VelloSharp;

namespace SkiaSharp;

public readonly struct SKColorSpacePrimaries
{
    public static SKColorSpacePrimaries Empty { get; } = new();

    public float RX { get; } = 1f;
    public float RY { get; } = 0f;
    public float GX { get; } = 0f;
    public float GY { get; } = 1f;
    public float BX { get; } = 0f;
    public float BY { get; } = 0f;
    public float WX { get; } = 0.3127f;
    public float WY { get; } = 0.3290f;

    public SKColorSpacePrimaries(float rx, float ry, float gx, float gy, float bx, float by, float wx, float wy)
    {
        RX = rx;
        RY = ry;
        GX = gx;
        GY = gy;
        BX = bx;
        BY = by;
        WX = wx;
        WY = wy;
    }

    public SKColorSpacePrimaries(float[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length != 8)
        {
            throw new ArgumentException("Primaries must contain exactly eight values.", nameof(values));
        }

        RX = values[0];
        RY = values[1];
        GX = values[2];
        GY = values[3];
        BX = values[4];
        BY = values[5];
        WX = values[6];
        WY = values[7];
    }

    public float[] Values => new[] { RX, RY, GX, GY, BX, BY, WX, WY };

    public bool ToColorSpaceXyz(out SKColorSpaceXyz toXyzD50)
    {
        toXyzD50 = SKColorSpaceXyz.Empty;
        ShimNotImplemented.Throw($"{nameof(SKColorSpacePrimaries)}.{nameof(ToColorSpaceXyz)}");
        return false;
    }

    public SKColorSpaceXyz ToColorSpaceXyz()
    {
        ShimNotImplemented.Throw($"{nameof(SKColorSpacePrimaries)}.{nameof(ToColorSpaceXyz)}");
        return SKColorSpaceXyz.Empty;
    }
}

public readonly struct SKColorSpaceTransferFn
{
    public static SKColorSpaceTransferFn Srgb { get; } =
        LoadTransferFn(
            PenikoNativeMethods.peniko_color_space_transfer_fn_srgb,
            nameof(PenikoNativeMethods.peniko_color_space_transfer_fn_srgb));
    public static SKColorSpaceTransferFn TwoDotTwo { get; } = new(2.2f, 1f, 0f, 0f, 0f, 0f, 0f);
    public static SKColorSpaceTransferFn Linear { get; } =
        LoadTransferFn(
            PenikoNativeMethods.peniko_color_space_transfer_fn_linear_srgb,
            nameof(PenikoNativeMethods.peniko_color_space_transfer_fn_linear_srgb));
    public static SKColorSpaceTransferFn Rec2020 { get; } = new(2.222f, 1f, 0f, 0f, 0f, 0f, 0f);
    public static SKColorSpaceTransferFn Pq { get; } = new(1f, 1f, 0f, 0f, 0f, 0f, 0f);
    public static SKColorSpaceTransferFn Hlg { get; } = new(1f, 1f, 0f, 0f, 0f, 0f, 0f);
    public static SKColorSpaceTransferFn Empty { get; } = default;

    public float G { get; }
    public float A { get; }
    public float B { get; }
    public float C { get; }
    public float D { get; }
    public float E { get; }
    public float F { get; }

    public SKColorSpaceTransferFn(float g, float a, float b, float c, float d, float e, float f)
    {
        G = g;
        A = a;
        B = b;
        C = c;
        D = d;
        E = e;
        F = f;
    }

    public SKColorSpaceTransferFn(float[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length != 7)
        {
            throw new ArgumentException("Transfer function values must contain exactly seven entries.", nameof(values));
        }

        G = values[0];
        A = values[1];
        B = values[2];
        C = values[3];
        D = values[4];
        E = values[5];
        F = values[6];
    }

    private delegate PenikoStatus TransferFnLoader(out PenikoColorSpaceTransferFn value);

    private static SKColorSpaceTransferFn LoadTransferFn(TransferFnLoader loader, string apiName)
    {
        PenikoColorSpaceTransferFn native;
        NativeHelpers.ThrowOnError(loader(out native), apiName);
        return new SKColorSpaceTransferFn(native.G, native.A, native.B, native.C, native.D, native.E, native.F);
    }

    public float[] Values => new[] { G, A, B, C, D, E, F };

    public SKColorSpaceTransferFn Invert()
    {
        ShimNotImplemented.Throw($"{nameof(SKColorSpaceTransferFn)}.{nameof(Invert)}");
        return this;
    }

    public float Transform(float x)
    {
        ShimNotImplemented.Throw($"{nameof(SKColorSpaceTransferFn)}.{nameof(Transform)}");
        return x;
    }
}

public readonly struct SKColorSpaceXyz
{
    public static SKColorSpaceXyz Empty { get; } = new();
    public static SKColorSpaceXyz Identity { get; } = new(
        1f, 0f, 0f,
        0f, 1f, 0f,
        0f, 0f, 1f,
        0f, 0f, 0f);

    public static SKColorSpaceXyz Srgb { get; } =
        LoadXyz(
            PenikoNativeMethods.peniko_color_space_xyz_linear_srgb,
            nameof(PenikoNativeMethods.peniko_color_space_xyz_linear_srgb));
    public static SKColorSpaceXyz AdobeRgb { get; } = Identity;
    public static SKColorSpaceXyz DisplayP3 { get; } =
        LoadXyz(
            PenikoNativeMethods.peniko_color_space_xyz_display_p3,
            nameof(PenikoNativeMethods.peniko_color_space_xyz_display_p3));
    public static SKColorSpaceXyz Rec2020 { get; } = Identity;
    public static SKColorSpaceXyz Xyz { get; } = Identity;

    public float A { get; }
    public float B { get; }
    public float C { get; }
    public float D { get; }
    public float E { get; }
    public float F { get; }
    public float G { get; }
    public float H { get; }
    public float I { get; }
    public float J { get; }
    public float K { get; }
    public float L { get; }

    public SKColorSpaceXyz(float a, float b, float c, float d, float e, float f, float g, float h, float i, float j, float k, float l)
    {
        A = a;
        B = b;
        C = c;
        D = d;
        E = e;
        F = f;
        G = g;
        H = h;
        I = i;
        J = j;
        K = k;
        L = l;
    }

    private delegate PenikoStatus XyzLoader(out PenikoColorSpaceXyz value);

    private static SKColorSpaceXyz LoadXyz(XyzLoader loader, string apiName)
    {
        PenikoColorSpaceXyz native;
        NativeHelpers.ThrowOnError(loader(out native), apiName);
        return new SKColorSpaceXyz(
            native.M00, native.M01, native.M02,
            native.M10, native.M11, native.M12,
            native.M20, native.M21, native.M22,
            0f, 0f, 0f);
    }

    public float[] Values => new[] { A, B, C, D, E, F, G, H, I, J, K, L };
}

public sealed class SKColorSpaceIccProfile : IDisposable
{
    private bool _disposed;

    public SKColorSpaceIccProfile()
    {
    }

    public static SKColorSpaceIccProfile Create(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            throw new ArgumentException("ICC data must not be empty.", nameof(data));
        }

        return ThrowNotSupported<SKColorSpaceIccProfile>($"{nameof(SKColorSpaceIccProfile)}.{nameof(Create)}", "from span");
    }

    public static SKColorSpaceIccProfile Create(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Create(data.AsSpan());
    }

    public static SKColorSpaceIccProfile Create(SKData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.IsEmpty)
        {
            throw new ArgumentException("ICC data must not be empty.", nameof(data));
        }

        return ThrowNotSupported<SKColorSpaceIccProfile>($"{nameof(SKColorSpaceIccProfile)}.{nameof(Create)}", "from SKData");
    }

    public static SKColorSpaceIccProfile Create(IntPtr data, long length)
    {
        if (data == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return ThrowNotSupported<SKColorSpaceIccProfile>($"{nameof(SKColorSpaceIccProfile)}.{nameof(Create)}", "from pointer");
    }

    public long Size => ThrowNotSupported<long>($"{nameof(SKColorSpaceIccProfile)}.{nameof(Size)}");

    public IntPtr Buffer => ThrowNotSupported<IntPtr>($"{nameof(SKColorSpaceIccProfile)}.{nameof(Buffer)}");

    public bool ToColorSpaceXyz(out SKColorSpaceXyz toXyzD50)
    {
        toXyzD50 = SKColorSpaceXyz.Empty;
        ThrowNotSupported<bool>($"{nameof(SKColorSpaceIccProfile)}.{nameof(ToColorSpaceXyz)}");
        return false;
    }

    public SKColorSpaceXyz ToColorSpaceXyz()
    {
        ThrowNotSupported<SKColorSpaceXyz>($"{nameof(SKColorSpaceIccProfile)}.{nameof(ToColorSpaceXyz)}");
        return SKColorSpaceXyz.Empty;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static T ThrowNotSupported<T>(string memberName, string? details = null)
    {
        ShimNotImplemented.Throw(memberName, details);
        throw new NotSupportedException($"TODO: {memberName}{(string.IsNullOrWhiteSpace(details) ? string.Empty : $" ({details})")}");
    }
}
