using System;
using System.Collections.Generic;
using VelloSharp.Charting.Scales;

namespace VelloSharp.Charting.Ticks;

/// <summary>
/// Provides tick generator resolution based on scale kind and domain type.
/// </summary>
public sealed class AxisTickGeneratorRegistry
{
    private readonly Dictionary<(ScaleKind Kind, Type DomainType), object> _generators = new();

    public AxisTickGeneratorRegistry Register<T>(ScaleKind kind, IAxisTickGenerator<T> generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        _generators[(kind, typeof(T))] = generator;
        return this;
    }

    public IAxisTickGenerator<T> Get<T>(ScaleKind kind)
    {
        if (_generators.TryGetValue((kind, typeof(T)), out var generator) &&
            generator is IAxisTickGenerator<T> typed)
        {
            return typed;
        }

        throw new InvalidOperationException($"No tick generator registered for scale kind '{kind}' with domain type '{typeof(T)}'.");
    }

    public static AxisTickGeneratorRegistry CreateDefault()
    {
        return new AxisTickGeneratorRegistry()
            .Register(ScaleKind.Linear, new LinearTickGenerator())
            .Register(ScaleKind.Logarithmic, new LinearTickGenerator())
            .Register(ScaleKind.Time, new TimeTickGenerator())
            .Register(ScaleKind.Ordinal, new OrdinalTickGenerator<string>())
            .Register(ScaleKind.Ordinal, new OrdinalTickGenerator<int>())
            .Register(ScaleKind.Ordinal, new OrdinalTickGenerator<long>())
            .Register(ScaleKind.Ordinal, new OrdinalTickGenerator<uint>());
    }
}
