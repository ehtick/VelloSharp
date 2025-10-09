using System;
using System.IO;
using Avalonia.Platform;
using SkiaSharp;

namespace SkiaGallery.SharedScenes;

internal static class SceneResources
{
    private const string LogoAsset = "avalonia-32.png";
    private static readonly object s_sync = new();
    private static SKImage? s_logoImage;
    private static byte[]? s_logoBytes;

    public static SKImage GetLogoImage()
    {
        lock (s_sync)
        {
            if (s_logoImage is { } image && image is not null)
            {
                return image;
            }

            using var stream = OpenAssetStream(LogoAsset);
            using var data = SKData.Create(stream);
            s_logoImage = SKImage.FromEncodedData(data) ?? throw new InvalidOperationException("Failed to decode logo image asset.");
            return s_logoImage;
        }
    }

    public static byte[] GetLogoBytes()
    {
        lock (s_sync)
        {
            if (s_logoBytes is { Length: > 0 })
            {
                return s_logoBytes;
            }

            using var stream = OpenAssetStream(LogoAsset);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            s_logoBytes = memory.ToArray();
            return s_logoBytes;
        }
    }

    public static Stream OpenAssetStream(string assetName)
    {
        var assemblyName = typeof(SceneResources).Assembly.GetName().Name
            ?? throw new InvalidOperationException("Missing assembly name for gallery scene resources.");
        var uri = new Uri($"avares://{assemblyName}/Assets/{assetName}");
        return AssetLoader.Open(uri);
    }
}
