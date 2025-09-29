using System;
using System.IO;
using System.Reflection;
using VelloSharp;

namespace SkiaSharp;

public sealed class SKTypeface : IDisposable
{
    private readonly Font _font;
    private readonly bool _ownsFont;

    private SKTypeface(Font font, bool ownsFont)
    {
        _font = font ?? throw new ArgumentNullException(nameof(font));
        _ownsFont = ownsFont;
    }

    public static SKTypeface Default => s_default.Value;

    private static readonly Lazy<SKTypeface> s_default = new(CreateDefault);

    public void Dispose()
    {
        if (_ownsFont)
        {
            _font.Dispose();
        }
    }

    internal Font Font => _font;

    public static SKTypeface FromStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var font = Font.Load(ms.ToArray());
        return new SKTypeface(font, ownsFont: true);
    }

    internal static SKTypeface FromFont(Font font, bool ownsFont = false) => new(font, ownsFont);

    private static SKTypeface CreateDefault()
    {
        var assembly = typeof(SKTypeface).Assembly;
        using var stream = assembly.GetManifestResourceStream("VelloSharp.Skia.Fonts.Roboto-Regular.ttf")
            ?? throw new InvalidOperationException("Embedded default font 'Roboto-Regular.ttf' was not found.");

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var font = Font.Load(ms.ToArray());
        return new SKTypeface(font, ownsFont: true);
    }
}
