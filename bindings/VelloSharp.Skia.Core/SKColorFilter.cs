using System;

namespace SkiaSharp;

public sealed class SKColorFilter : IDisposable
{
    public const int ColorMatrixSize = 20;
    public const int TableMaxLength = 256;

    private bool _disposed;

    private SKColorFilter()
    {
    }

    public static SKColorFilter CreateSrgbToLinearGamma() =>
        ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateSrgbToLinearGamma)}", "gamma conversion");

    public static SKColorFilter CreateLinearToSrgbGamma() =>
        ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateLinearToSrgbGamma)}", "gamma conversion");

    public static SKColorFilter CreateBlendMode(SKColor color, SKBlendMode mode) =>
        ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateBlendMode)}", mode.ToString());

    public static SKColorFilter CreateLighting(SKColor mul, SKColor add) =>
        ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateLighting)}");

    public static SKColorFilter CreateCompose(SKColorFilter outer, SKColorFilter inner)
    {
        ArgumentNullException.ThrowIfNull(outer);
        ArgumentNullException.ThrowIfNull(inner);
        return ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateCompose)}");
    }

    public static SKColorFilter CreateLerp(float weight, SKColorFilter filter0, SKColorFilter filter1)
    {
        ArgumentNullException.ThrowIfNull(filter0);
        ArgumentNullException.ThrowIfNull(filter1);
        return ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateLerp)}");
    }

    public static SKColorFilter CreateColorMatrix(float[] matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        return CreateColorMatrix(matrix.AsSpan());
    }

    public static SKColorFilter CreateColorMatrix(ReadOnlySpan<float> matrix)
    {
        if (matrix.Length != ColorMatrixSize)
        {
            throw new ArgumentException($"Matrix must contain {ColorMatrixSize} values.", nameof(matrix));
        }

        return ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateColorMatrix)}", "color matrix");
    }

    public static SKColorFilter CreateHslaColorMatrix(ReadOnlySpan<float> matrix)
    {
        if (matrix.Length != ColorMatrixSize)
        {
            throw new ArgumentException($"Matrix must contain {ColorMatrixSize} values.", nameof(matrix));
        }

        return ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateHslaColorMatrix)}", "HSLA matrix");
    }

    public static SKColorFilter CreateLumaColor() =>
        ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateLumaColor)}");

    public static SKColorFilter CreateTable(byte[] table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return CreateTable(table.AsSpan());
    }

    public static SKColorFilter CreateTable(ReadOnlySpan<byte> table)
    {
        if (table.Length != TableMaxLength)
        {
            throw new ArgumentException($"Table must contain {TableMaxLength} entries.", nameof(table));
        }

        return ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateTable)}", "single table");
    }

    public static SKColorFilter CreateTable(byte[] tableA, byte[] tableR, byte[] tableG, byte[] tableB)
    {
        ArgumentNullException.ThrowIfNull(tableA);
        ArgumentNullException.ThrowIfNull(tableR);
        ArgumentNullException.ThrowIfNull(tableG);
        ArgumentNullException.ThrowIfNull(tableB);
        return CreateTable(tableA.AsSpan(), tableR.AsSpan(), tableG.AsSpan(), tableB.AsSpan());
    }

    public static SKColorFilter CreateTable(ReadOnlySpan<byte> tableA, ReadOnlySpan<byte> tableR, ReadOnlySpan<byte> tableG, ReadOnlySpan<byte> tableB)
    {
        ValidateTable(tableA, nameof(tableA));
        ValidateTable(tableR, nameof(tableR));
        ValidateTable(tableG, nameof(tableG));
        ValidateTable(tableB, nameof(tableB));
        return ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateTable)}", "ARGB tables");
    }

    public static SKColorFilter CreateHighContrast(SKHighContrastConfig config)
    {
        if (!config.IsValid)
        {
            throw new ArgumentException("High contrast configuration is invalid.", nameof(config));
        }

        return ThrowNotSupported($"{nameof(SKColorFilter)}.{nameof(CreateHighContrast)}");
    }

    public static SKColorFilter CreateHighContrast(bool grayscale, SKHighContrastConfigInvertStyle invertStyle, float contrast) =>
        CreateHighContrast(new SKHighContrastConfig(grayscale, invertStyle, contrast));

    public static void EnsureStaticInstanceAreInitialized()
    {
        // Included for API parity with SkiaSharp; nothing to do until native filters are available.
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

    private static void ValidateTable(ReadOnlySpan<byte> table, string argumentName)
    {
        if (table.Length != TableMaxLength)
        {
            throw new ArgumentException($"Table must contain {TableMaxLength} entries.", argumentName);
        }
    }

    private static SKColorFilter ThrowNotSupported(string memberName, string? details = null)
    {
        ShimNotImplemented.Throw(memberName, details);
        throw new NotSupportedException($"TODO: {memberName}{(string.IsNullOrWhiteSpace(details) ? string.Empty : $" ({details})")}");
    }
}
