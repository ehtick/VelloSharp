using Avalonia.Controls;

namespace AvaloniaVelloHarfBuzzSample.Navigation;

public interface ISamplePage
{
    string Title { get; }

    string Subtitle { get; }

    string? Glyph { get; }

    UserControl GetOrCreateView();
}
