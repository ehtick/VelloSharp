using System;

namespace SkiaSharp;

public sealed class SKColorSpace : IDisposable
{
    private bool _disposed;

    private SKColorSpace()
    {
    }

    public static SKColorSpace CreateSrgb() =>
        ThrowNotSupported<SKColorSpace>($"{nameof(SKColorSpace)}.{nameof(CreateSrgb)}");

    public static SKColorSpace CreateSrgbLinear() =>
        ThrowNotSupported<SKColorSpace>($"{nameof(SKColorSpace)}.{nameof(CreateSrgbLinear)}");

    public static SKColorSpace CreateIcc(byte[] input, long length)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (length < 0 || length > input.LongLength)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return CreateIcc(input.AsSpan(0, (int)Math.Min(length, input.LongLength)));
    }

    public static SKColorSpace CreateIcc(byte[] input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return CreateIcc(input.AsSpan());
    }

    public static SKColorSpace CreateIcc(ReadOnlySpan<byte> input)
    {
        if (input.IsEmpty)
        {
            throw new ArgumentException("ICC profile data must not be empty.", nameof(input));
        }

        return ThrowNotSupported<SKColorSpace>($"{nameof(SKColorSpace)}.{nameof(CreateIcc)}", "ICC profile");
    }

    public static SKColorSpace CreateIcc(SKData input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.IsEmpty)
        {
            throw new ArgumentException("ICC profile data must not be empty.", nameof(input));
        }

        return ThrowNotSupported<SKColorSpace>($"{nameof(SKColorSpace)}.{nameof(CreateIcc)}", "ICC data");
    }

    public static SKColorSpace CreateIcc(SKColorSpaceIccProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return ThrowNotSupported<SKColorSpace>($"{nameof(SKColorSpace)}.{nameof(CreateIcc)}", "ICC profile object");
    }

    public static SKColorSpace CreateRgb(SKColorSpaceTransferFn transferFn, SKColorSpaceXyz toXyzD50) =>
        ThrowNotSupported<SKColorSpace>($"{nameof(SKColorSpace)}.{nameof(CreateRgb)}");

    public static bool Equal(SKColorSpace left, SKColorSpace right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return ThrowNotSupported<bool>($"{nameof(SKColorSpace)}.{nameof(Equal)}");
    }

    public static void EnsureStaticInstanceAreInitialized()
    {
        // Present for API parity. No initialization required yet.
    }

    public bool GammaIsCloseToSrgb => ThrowNotSupported<bool>($"{nameof(SKColorSpace)}.{nameof(GammaIsCloseToSrgb)}");

    public bool GammaIsLinear => ThrowNotSupported<bool>($"{nameof(SKColorSpace)}.{nameof(GammaIsLinear)}");

    public bool IsSrgb => ThrowNotSupported<bool>($"{nameof(SKColorSpace)}.{nameof(IsSrgb)}");

    public bool IsNumericalTransferFunction => GetNumericalTransferFunction(out _);

    public SKColorSpaceTransferFn GetNumericalTransferFunction() =>
        ThrowNotSupported<SKColorSpaceTransferFn>($"{nameof(SKColorSpace)}.{nameof(GetNumericalTransferFunction)}");

    public bool GetNumericalTransferFunction(out SKColorSpaceTransferFn fn)
    {
        fn = default;
        ThrowNotSupported<bool>($"{nameof(SKColorSpace)}.{nameof(GetNumericalTransferFunction)}", "out parameter");
        return false;
    }

    public SKColorSpaceIccProfile ToProfile() =>
        ThrowNotSupported<SKColorSpaceIccProfile>($"{nameof(SKColorSpace)}.{nameof(ToProfile)}");

    public bool ToColorSpaceXyz(out SKColorSpaceXyz toXyzD50)
    {
        toXyzD50 = SKColorSpaceXyz.Empty;
        ThrowNotSupported<bool>($"{nameof(SKColorSpace)}.{nameof(ToColorSpaceXyz)}");
        return false;
    }

    public SKColorSpaceXyz ToColorSpaceXyz()
    {
        ThrowNotSupported<SKColorSpaceXyz>($"{nameof(SKColorSpace)}.{nameof(ToColorSpaceXyz)}");
        return SKColorSpaceXyz.Empty;
    }

    public SKColorSpace ToLinearGamma() =>
        ThrowNotSupported<SKColorSpace>($"{nameof(SKColorSpace)}.{nameof(ToLinearGamma)}");

    public SKColorSpace ToSrgbGamma() =>
        ThrowNotSupported<SKColorSpace>($"{nameof(SKColorSpace)}.{nameof(ToSrgbGamma)}");

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
