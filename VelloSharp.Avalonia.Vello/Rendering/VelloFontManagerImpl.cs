using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using Avalonia.Media;
using Avalonia.Platform;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal sealed class VelloFontManagerImpl : IFontManagerImpl
{
    private const string DefaultFamilyName = "Roboto";
    private static readonly string[] s_installedFamilies = { DefaultFamilyName };
    private readonly byte[] _defaultFontData;

    public VelloFontManagerImpl()
    {
        _defaultFontData = LoadEmbeddedFont();
    }

    public string GetDefaultFontFamilyName() => DefaultFamilyName;

    public string[] GetInstalledFontFamilyNames(bool checkForUpdates = false) => s_installedFamilies;

    public bool TryMatchCharacter(int codepoint, FontStyle fontStyle, FontWeight fontWeight, FontStretch fontStretch, CultureInfo? culture, out Typeface typeface)
    {
        typeface = new Typeface(DefaultFamilyName, fontStyle, fontWeight, fontStretch);
        return true;
    }

    public bool TryCreateGlyphTypeface(string familyName, FontStyle style, FontWeight weight, FontStretch stretch, [NotNullWhen(true)] out IGlyphTypeface? glyphTypeface)
    {
        if (!string.Equals(familyName, DefaultFamilyName, StringComparison.OrdinalIgnoreCase))
        {
            glyphTypeface = null;
            return false;
        }

        glyphTypeface = new VelloGlyphTypeface(DefaultFamilyName, style, weight, stretch, _defaultFontData);
        return true;
    }

    public bool TryCreateGlyphTypeface(Stream stream, FontSimulations fontSimulations, out IGlyphTypeface glyphTypeface)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var fontData = ms.ToArray();

        var style = fontSimulations.HasFlag(FontSimulations.Oblique) ? FontStyle.Italic : FontStyle.Normal;
        var weight = fontSimulations.HasFlag(FontSimulations.Bold) ? FontWeight.Bold : FontWeight.Normal;

        glyphTypeface = new VelloGlyphTypeface(DefaultFamilyName, style, weight, FontStretch.Normal, fontData);
        return true;
    }

    private static byte[] LoadEmbeddedFont()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "VelloSharp.Avalonia.Vello.Assets.Fonts.Roboto-Regular.ttf";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded font resource '{resourceName}' not found.");

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
