using System;

namespace VelloSharp.Composition.Controls;

public sealed class CompositionTemplate
{
    private readonly Func<TemplatedControl, CompositionElement?> _builder;

    public CompositionTemplate(Func<TemplatedControl, CompositionElement?> builder)
    {
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
    }

    public CompositionElement? Build(TemplatedControl owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return _builder(owner);
    }

    public static CompositionTemplate Create(Func<TemplatedControl, CompositionElement?> builder) =>
        new(builder);
}
