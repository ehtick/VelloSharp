using VelloSharp.Composition.Input;

namespace VelloSharp.Maui.Controls;

/// <summary>
/// Contract implemented by platform-specific MAUI handlers to expose their composition input source.
/// </summary>
public interface IVelloViewHandler
{
    ICompositionInputSource? CompositionInputSource { get; }
}
