using System;
using System.IO;
using System.Threading;
using Avalonia.Platform;
using VelloSharp;
using VelloSharp.Text;

namespace AvaloniaVelloPlayground.Services;

public static class PlaygroundAssets
{
    private static readonly Lazy<Font> s_interFont = new(CreateInterFont, LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Lazy<Image> s_noiseImage = new(CreateNoiseImage, LazyThreadSafetyMode.ExecutionAndPublication);

    public static Font InterFont => s_interFont.Value;

    public static Image NoiseImage => s_noiseImage.Value;

    public static Glyph[] ShapeText(string text, float fontSize = 48f, bool isRightToLeft = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<Glyph>();
        }

        var font = InterFont;
        var shaped = VelloTextShaperCore.ShapeUtf16(font.Handle, text.AsSpan(), fontSize, isRightToLeft);
        if (shaped.Count == 0)
        {
            return Array.Empty<Glyph>();
        }

        var glyphs = new Glyph[shaped.Count];
        var penX = 0f;
        for (var i = 0; i < shaped.Count; i++)
        {
            var glyph = shaped[i];
            glyphs[i] = new Glyph(glyph.GlyphId, penX + glyph.XOffset, glyph.YOffset);
            penX += glyph.XAdvance;
        }

        return glyphs;
    }

    private static Font CreateInterFont()
    {
        var uri = new Uri("avares://Avalonia.Fonts.Inter/Fonts/Inter/Inter-Regular.ttf");
        using var stream = AssetLoader.Open(uri);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return Font.Load(memory.ToArray());
    }

    private static Image CreateNoiseImage()
    {
        const int width = 256;
        const int height = 256;
        var buffer = new byte[width * height * 4];
        Span<byte> pixels = buffer;

        var rng = new Random(1234);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = (y * width + x) * 4;
                var value = (byte)rng.Next(20, 220);
                var gradient = (byte)(255 * x / (float)width);
                pixels[index + 0] = (byte)((value + gradient) % 255);
                pixels[index + 1] = (byte)((value * 2 + 50) % 255);
                pixels[index + 2] = (byte)(255 - value);
                pixels[index + 3] = 255;
            }
        }

        return Image.FromPixels(pixels, width, height, RenderFormat.Rgba8);
    }
}
