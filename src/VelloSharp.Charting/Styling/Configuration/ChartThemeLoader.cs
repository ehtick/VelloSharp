using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using VelloSharp.Charting.Styling;

namespace VelloSharp.Charting.Styling.Configuration;

public static class ChartThemeLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static ChartTheme LoadFromJson(string json)
    {
        var themes = LoadManyFromJson(json);
        if (themes.Count == 0)
        {
            throw new InvalidOperationException("No theme definitions were found in the provided JSON.");
        }

        return themes[0];
    }

    public static IReadOnlyList<ChartTheme> LoadManyFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON cannot be null or whitespace.", nameof(json));
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => document.RootElement.EnumerateArray()
                .Select(ParseDefinition)
                .ToList(),
            JsonValueKind.Object => new[] { ParseDefinition(document.RootElement) },
            _ => throw new InvalidOperationException("Theme JSON must be an object or an array of objects."),
        };
    }

    public static ChartTheme LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
        }

        var json = File.ReadAllText(path);
        return LoadFromJson(json);
    }

    public static IReadOnlyList<ChartTheme> LoadManyFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
        }

        var json = File.ReadAllText(path);
        return LoadManyFromJson(json);
    }

    private static ChartTheme ParseDefinition(JsonElement element)
    {
        var definition = element.Deserialize<ChartThemeDefinition>(SerializerOptions);
        if (definition is null)
        {
            throw new InvalidOperationException("Failed to deserialize chart theme definition.");
        }

        return definition.ToTheme();
    }
}
