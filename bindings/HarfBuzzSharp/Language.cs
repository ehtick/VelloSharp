using System;
using System.Globalization;

namespace HarfBuzzSharp;

public sealed class Language : IEquatable<Language>
{
    public Language(string name)
    {
        Name = Normalize(name);
    }

    public Language(CultureInfo culture)
        : this(culture?.Name ?? string.Empty)
    {
    }

    public string Name { get; }

    public static Language FromBcp47(string name) => new(name);

    public override string ToString() => Name;

    public bool Equals(Language? other) => other is not null && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => obj is Language language && Equals(language);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);

    private static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "und";
        }

        return name.Replace('_', '-');
    }
}
