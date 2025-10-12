using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Diagnostics;
using AvaloniaVelloHarfBuzzSample.Rendering;

namespace AvaloniaVelloHarfBuzzSample.Diagnostics;

public sealed class ShapeCaptureRecorder
{
    private readonly object _gate = new();
    private readonly string _root;
    private int _counter;

    public ShapeCaptureRecorder(string? rootDirectory = null)
    {
        _root = string.IsNullOrWhiteSpace(rootDirectory)
            ? Path.Combine(Environment.CurrentDirectory, "artifacts", "samples", "harfbuzz")
            : Path.GetFullPath(rootDirectory);

        Directory.CreateDirectory(_root);
    }

    public ShapeCaptureResult Capture(GlyphRunScene scene, string? captureName = null)
    {
        Guard.IsNotNull(scene);

        lock (_gate)
        {
            var name = captureName ?? $"capture-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}-{_counter++:D4}";
            var directory = Path.Combine(_root, name);
            Directory.CreateDirectory(directory);

            var textPath = Path.Combine(directory, "glyphs.hbtext");
            File.WriteAllText(textPath, scene.SerializedGlyphsText);

            var jsonPath = Path.Combine(directory, "glyphs.hbjson");
            File.WriteAllText(jsonPath, scene.SerializedGlyphsJson);

            var metadata = CreateMetadata(scene, name);
            var metadataPath = Path.Combine(directory, "metadata.json");
            var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            File.WriteAllText(metadataPath, metadataJson);

            return new ShapeCaptureResult(directory, textPath, jsonPath, metadataPath);
        }
    }

    private static ShapeCaptureMetadata CreateMetadata(GlyphRunScene scene, string name)
    {
        var features = new List<ShapeCaptureFeature>();
        foreach (var feature in scene.Features)
        {
            features.Add(new ShapeCaptureFeature(
                feature.Tag.ToString(),
                unchecked((int)feature.Value),
                feature.Start,
                feature.End));
        }

        return new ShapeCaptureMetadata(
            name,
            scene.Text,
            scene.FontReference.AssetKey,
            scene.FontReference.FaceIndex,
            scene.FontSize,
            scene.UnitsPerEmScale,
            scene.Direction.ToString(),
            scene.Language.ToString(),
            scene.Script.ToString(),
            features,
            scene.Metrics,
            scene.Timestamp);
    }
}

public readonly record struct ShapeCaptureResult(
    string Directory,
    string GlyphsTextPath,
    string GlyphsJsonPath,
    string MetadataPath);

public sealed record ShapeCaptureMetadata(
    string Name,
    string Text,
    string FontAsset,
    int FaceIndex,
    float FontSize,
    float UnitsPerEm,
    string Direction,
    string Language,
    string Script,
    IReadOnlyList<ShapeCaptureFeature> Features,
    GlyphRunMetrics Metrics,
    DateTimeOffset Timestamp);

public readonly record struct ShapeCaptureFeature(string Tag, int Value, uint Start, uint End);
