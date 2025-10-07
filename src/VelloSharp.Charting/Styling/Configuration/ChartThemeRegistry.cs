using System;
using System.Collections.Generic;
using System.Linq;
using VelloSharp.Charting.Styling;

namespace VelloSharp.Charting.Styling.Configuration;

public static class ChartThemeRegistry
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, ChartTheme> Themes = new(StringComparer.OrdinalIgnoreCase);

    static ChartThemeRegistry()
    {
        RegisterTheme(ChartTheme.Light);
        RegisterTheme(ChartTheme.Dark);
    }

    public static IReadOnlyList<ChartTheme> GetThemes()
    {
        lock (SyncRoot)
        {
            return Themes.Values.ToArray();
        }
    }

    public static IReadOnlyList<string> GetThemeNames()
    {
        lock (SyncRoot)
        {
            return Themes.Keys.ToArray();
        }
    }

    public static bool TryGetTheme(string name, out ChartTheme theme)
    {
        lock (SyncRoot)
        {
            return Themes.TryGetValue(name, out theme!);
        }
    }

    public static void RegisterTheme(ChartTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        lock (SyncRoot)
        {
            Themes[theme.Name] = theme;
        }
    }

    public static void RegisterThemes(IEnumerable<ChartTheme> themes)
    {
        ArgumentNullException.ThrowIfNull(themes);

        lock (SyncRoot)
        {
            foreach (var theme in themes)
            {
                if (theme is null)
                {
                    continue;
                }

                Themes[theme.Name] = theme;
            }
        }
    }
}
