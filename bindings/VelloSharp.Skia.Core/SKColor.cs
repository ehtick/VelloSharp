using VelloSharp;

namespace SkiaSharp;

public readonly struct SKColor : IEquatable<SKColor>
{
    public SKColor(byte red, byte green, byte blue, byte alpha = 255)
    {
        Red = red;
        Green = green;
        Blue = blue;
        Alpha = alpha;
    }

    public byte Red { get; }
    public byte Green { get; }
    public byte Blue { get; }
    public byte Alpha { get; }

    public static SKColor FromArgb(byte alpha, byte red, byte green, byte blue) => new(red, green, blue, alpha);

    public RgbaColor ToRgbaColor() => RgbaColor.FromBytes(Red, Green, Blue, Alpha);

    public bool Equals(SKColor other) =>
        Red == other.Red &&
        Green == other.Green &&
        Blue == other.Blue &&
        Alpha == other.Alpha;

    public override bool Equals(object? obj) => obj is SKColor color && Equals(color);

    public override int GetHashCode() => HashCode.Combine(Red, Green, Blue, Alpha);

    public static bool operator ==(SKColor left, SKColor right) => left.Equals(right);

    public static bool operator !=(SKColor left, SKColor right) => !left.Equals(right);

    public override string ToString() => $"#{Alpha:X2}{Red:X2}{Green:X2}{Blue:X2}";

    public static SKColor Empty { get; } = new(0, 0, 0, 0);
}

public static class SKColors
{
    public static SKColor White { get; } = new(255, 255, 255);
    public static SKColor Black { get; } = new(0, 0, 0);
    public static SKColor Transparent { get; } = new(0, 0, 0, 0);
    public static SKColor LightGray { get; } = new(211, 211, 211);
    public static SKColor Gray { get; } = new(128, 128, 128);
    public static SKColor DarkGray { get; } = new(169, 169, 169);
    public static SKColor Red { get; } = new(255, 0, 0);
    public static SKColor Green { get; } = new(0, 128, 0);
    public static SKColor Blue { get; } = new(0, 0, 255);
}
