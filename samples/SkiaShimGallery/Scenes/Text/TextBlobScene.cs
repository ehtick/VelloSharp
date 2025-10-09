using System;
using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Text;

internal sealed class TextBlobScene : ISkiaGalleryScene
{
    public string Title => "Text: Glyph Runs";

    public string Description => "Builds an SKTextBlob from positioned glyphs using SKFont metrics.";

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(246, 248, 251));

        using var typeface = SKTypeface.Default;
        using var font = new SKFont(typeface, 28)
        {
            Subpixel = true,
            Hinting = SKFontHinting.Slight,
            Edging = SKFontEdging.SubpixelAntialias,
        };

        const string text = "Text blobs!";
        var glyphs = new ushort[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            glyphs[i] = text[i];
        }

        var bounds = new SKRect[glyphs.Length];
        var advances = new float[glyphs.Length];
        font.GetGlyphWidths(glyphs, advances, bounds.AsSpan());

        var positions = new SKPoint[glyphs.Length];
        var cursor = 0f;
        for (var i = 0; i < glyphs.Length; i++)
        {
            positions[i] = new SKPoint(cursor, 0);
            cursor += advances[i];
        }

        using var builder = new SKTextBlobBuilder();
        var run = builder.AllocatePositionedRun(font, glyphs.Length);
        run.SetGlyphs(glyphs);
        run.SetPositions(positions);
        using var blob = builder.Build()!;

        using var paint = new SKPaint
        {
            Color = new SKColor(52, 78, 140),
            IsAntialias = true,
        };

        canvas.DrawText(blob, 40, 110, paint);
        canvas.DrawText($"Glyph count: {glyphs.Length}", 40, 160, paint);
    }
}
