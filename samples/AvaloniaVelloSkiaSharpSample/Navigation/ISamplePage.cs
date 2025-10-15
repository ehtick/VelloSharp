using System;
using System.Collections.Generic;
using Avalonia.Controls;

namespace AvaloniaVelloSkiaSharpSample.Navigation;

public sealed record DocumentationLink(string Title, Uri Url);

public interface ISamplePage
{
    string Title { get; }

    string Description { get; }

    string? Icon { get; }

    Func<Control> ContentFactory { get; }

    IReadOnlyList<DocumentationLink> DocumentationLinks { get; }
}
