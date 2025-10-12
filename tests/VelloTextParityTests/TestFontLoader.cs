using System;
using System.Collections.Concurrent;
using System.IO;
using SkiaSharp;

namespace VelloTextParityTests;

internal static class TestFontLoader
{
    private const string ResourcePrefix = "VelloTextParityTests.Assets.";
    internal const string PrimaryFontAsset = "Roboto-Regular.ttf";

    private static readonly ConcurrentDictionary<string, byte[]> s_fontCache = new();
    private static readonly Lazy<byte[]> s_primaryFontData = new(() => LoadFontBytes(PrimaryFontAsset));
    private static readonly Lazy<int> s_primaryUnitsPerEm = new(() => CalculateUnitsPerEm(s_primaryFontData.Value));

    internal static byte[] PrimaryFontData => s_primaryFontData.Value;

    internal static int PrimaryUnitsPerEm => s_primaryUnitsPerEm.Value;

    internal static Stream OpenPrimaryFontStream() => new MemoryStream(PrimaryFontData, writable: false);

    internal static Stream OpenFontStream(string assetFileName)
        => new MemoryStream(LoadFontBytes(assetFileName), writable: false);

    internal static byte[] LoadFontBytes(string assetFileName)
        => s_fontCache.GetOrAdd(assetFileName, static name => LoadFontInternal(name));

    internal static int GetUnitsPerEm(string assetFileName)
        => CalculateUnitsPerEm(LoadFontBytes(assetFileName));

    internal static int CalculateUnitsPerEm(byte[] fontData)
    {
        using var typeface = SKTypeface.FromData(SKData.CreateCopy(fontData));
        return typeface.UnitsPerEm;
    }

    private static byte[] LoadFontInternal(string assetFileName)
    {
        var resourceName = ResourcePrefix + assetFileName.Replace('/', '.').Replace('\\', '.');
        using var stream = typeof(TestFontLoader).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded font '{resourceName}' not found.");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
