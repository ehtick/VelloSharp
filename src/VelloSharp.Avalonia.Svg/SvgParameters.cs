using System.Collections.Generic;

namespace VelloSharp.Avalonia.Svg;

/// <summary>
/// Minimal parameter bag mirroring the legacy Skia-style signature.
/// </summary>
public sealed class SvgParameters
{
    public SvgParameters(Dictionary<string, string>? entities = null, string? css = null)
    {
        Entities = entities;
        Css = css;
    }

    public Dictionary<string, string>? Entities { get; }

    public string? Css { get; }
}
