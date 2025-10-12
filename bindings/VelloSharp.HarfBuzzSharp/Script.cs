using System;
using System.Collections.Generic;

namespace HarfBuzzSharp;

public readonly partial struct Script : IEquatable<Script>
{
    private static readonly HashSet<Script> RightToLeftScripts = new()
    {
        new Script(new Tag('A', 'r', 'a', 'b')),
        new Script(new Tag('H', 'e', 'b', 'r')),
        new Script(new Tag('S', 'y', 'r', 'c')),
        new Script(new Tag('T', 'h', 'a', 'a')),
        new Script(new Tag('N', 'k', 'o', 'o')),
        new Script(new Tag('A', 'd', 'l', 'm')),
        new Script(new Tag('H', 'a', 'n', 'a')),
    };

    private readonly Tag _tag;

    private Script(Tag tag)
    {
        _tag = tag;
    }

    public Direction HorizontalDirection => RightToLeftScripts.Contains(this) ? Direction.RightToLeft : Direction.LeftToRight;

    public static Script Parse(string str)
        => new(Tag.Parse(str));

    public static bool TryParse(string str, out Script script)
    {
        script = Parse(str);
        return script != Unknown;
    }

    public override string ToString() => _tag.ToString();

    public static implicit operator uint(Script script) => script._tag;

    public static implicit operator Script(uint tag) => new(new Tag((char)(tag >> 24), (char)(tag >> 16), (char)(tag >> 8), (char)tag));

    public override bool Equals(object? obj) => obj is Script script && Equals(script);

    public bool Equals(Script other) => _tag.Equals(other._tag);

    public override int GetHashCode() => _tag.GetHashCode();

    public static readonly Script Invalid = new(Tag.None);
    public static readonly Script MaxValue = new(Tag.Max);
    public static readonly Script MaxValueSigned = new(Tag.MaxSigned);

    public static readonly Script Common = new(new Tag('Z', 'y', 'y', 'y'));
    public static readonly Script Inherited = new(new Tag('Z', 'i', 'n', 'h'));
    public static readonly Script Unknown = new(new Tag('Z', 'z', 'z', 'z'));

    public static readonly Script Latin = new(new Tag('L', 'a', 't', 'n'));
    public static readonly Script Greek = new(new Tag('G', 'r', 'e', 'k'));
    public static readonly Script Cyrillic = new(new Tag('C', 'y', 'r', 'l'));
    public static readonly Script Arabic = new(new Tag('A', 'r', 'a', 'b'));
    public static readonly Script Hebrew = new(new Tag('H', 'e', 'b', 'r'));
    public static readonly Script Syriac = new(new Tag('S', 'y', 'r', 'c'));
    public static readonly Script Thaana = new(new Tag('T', 'h', 'a', 'a'));
    public static readonly Script Hangul = new(new Tag('H', 'a', 'n', 'g'));
    public static readonly Script Han = new(new Tag('H', 'a', 'n', 'i'));
    public static readonly Script Devanagari = new(new Tag('D', 'e', 'v', 'a'));
    public static readonly Script Bengali = new(new Tag('B', 'e', 'n', 'g'));
    public static readonly Script Gurmukhi = new(new Tag('G', 'u', 'r', 'u'));
    public static readonly Script Gujarati = new(new Tag('G', 'u', 'j', 'r'));
    public static readonly Script Oriya = new(new Tag('O', 'r', 'y', 'a'));
    public static readonly Script Tamil = new(new Tag('T', 'a', 'm', 'l'));
    public static readonly Script Telugu = new(new Tag('T', 'e', 'l', 'u'));
    public static readonly Script Kannada = new(new Tag('K', 'n', 'd', 'a'));
    public static readonly Script Malayalam = new(new Tag('M', 'l', 'y', 'm'));
    public static readonly Script Sinhala = new(new Tag('S', 'i', 'n', 'h'));
    public static readonly Script Thai = new(new Tag('T', 'h', 'a', 'i'));
    public static readonly Script Lao = new(new Tag('L', 'a', 'o', 'o'));
    public static readonly Script Tibetan = new(new Tag('T', 'i', 'b', 't'));
    public static readonly Script Myanmar = new(new Tag('M', 'y', 'm', 'r'));
    public static readonly Script Georgian = new(new Tag('G', 'e', 'o', 'r'));
    public static readonly Script Armenian = new(new Tag('A', 'r', 'm', 'n'));
    public static readonly Script Ethiopic = new(new Tag('E', 't', 'h', 'i'));
    public static readonly Script Cherokee = new(new Tag('C', 'h', 'e', 'r'));
    public static readonly Script CanadianSyllabics = new(new Tag('C', 'a', 'n', 's'));
    public static readonly Script Ogham = new(new Tag('O', 'g', 'a', 'm'));
    public static readonly Script Runic = new(new Tag('R', 'u', 'n', 'r'));
    public static readonly Script Khmer = new(new Tag('K', 'h', 'm', 'r'));
    public static readonly Script Mongolian = new(new Tag('M', 'o', 'n', 'g'));
    public static readonly Script Yi = new(new Tag('Y', 'i', 'i', 'i'));
}
