namespace VelloSharp;

internal static class GlyphNativeExtensions
{
    public static VelloGlyph ToNative(this Glyph glyph) => new()
    {
        Id = glyph.Id,
        X = glyph.X,
        Y = glyph.Y,
    };
}
