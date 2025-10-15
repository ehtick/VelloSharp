using System;
using System.Collections;
using System.Collections.Generic;

namespace SkiaSharp;

public sealed class SKRuntimeEffect : IDisposable
{
    private static ISkiaRuntimeEffectBackend s_backend = UnsupportedRuntimeEffectBackend.Instance;

    private readonly ISkiaRuntimeEffectInstance _instance;
    private bool _disposed;

    private SKRuntimeEffect(string sksl, ISkiaRuntimeEffectInstance instance)
    {
        Sksl = sksl;
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
    }

    public string Sksl { get; }

    internal static void SetBackend(ISkiaRuntimeEffectBackend backend)
    {
        s_backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public static SKRuntimeEffect? CreateShader(string sksl, out string? errors)
    {
        if (string.IsNullOrWhiteSpace(sksl))
        {
            throw new ArgumentNullException(nameof(sksl));
        }

        var instance = s_backend.CompileShader(sksl, out errors);
        if (instance is null)
        {
            return null;
        }

        return new SKRuntimeEffect(sksl, instance);
    }

    public SKShader ToShader(SKRuntimeEffectUniforms? uniforms) =>
        ToShader(uniforms, null, null);

    public SKShader ToShader(SKRuntimeEffectUniforms? uniforms, SKRuntimeEffectChildren? children) =>
        ToShader(uniforms, children, null);

    public SKShader ToShader(SKRuntimeEffectUniforms? uniforms, SKRuntimeEffectChildren? children, SKMatrix? localMatrix)
    {
        ThrowIfDisposed();

        if (uniforms is not null && !ReferenceEquals(uniforms.Effect, this))
        {
            throw new ArgumentException("Uniform collection belongs to a different runtime effect.", nameof(uniforms));
        }

        return _instance.CreateShader(this, uniforms, children, localMatrix)
            ?? throw new InvalidOperationException("Runtime effect backend failed to produce a shader instance.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _instance.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SKRuntimeEffect));
        }
    }

    private sealed class UnsupportedRuntimeEffectBackend : ISkiaRuntimeEffectBackend
    {
        public static UnsupportedRuntimeEffectBackend Instance { get; } = new();

        public ISkiaRuntimeEffectInstance? CompileShader(string sksl, out string? errors)
        {
            errors = "Runtime effects are not supported by the active Vello backend.";
            return null;
        }
    }
}

public sealed class SKRuntimeEffectUniforms : IEnumerable<string>
{
    private readonly Dictionary<string, SKRuntimeEffectUniform> _values = new(StringComparer.Ordinal);

    public SKRuntimeEffectUniforms(SKRuntimeEffect effect)
    {
        Effect = effect ?? throw new ArgumentNullException(nameof(effect));
    }

    public SKRuntimeEffect Effect { get; }

    public SKRuntimeEffectUniform this[string name]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(name);
            return _values.TryGetValue(name, out var value) ? value : SKRuntimeEffectUniform.Empty;
        }
        set
        {
            ArgumentNullException.ThrowIfNull(name);
            _values[name] = value;
        }
    }

    public void Clear() => _values.Clear();

    public bool Remove(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _values.Remove(name);
    }

    public IEnumerator<string> GetEnumerator() => _values.Keys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal bool TryGetUniform(string name, out SKRuntimeEffectUniform uniform) =>
        _values.TryGetValue(name, out uniform);

    internal IReadOnlyDictionary<string, SKRuntimeEffectUniform> Items => _values;
}

public sealed class SKRuntimeEffectChildren : IEnumerable<string>
{
    private readonly Dictionary<string, SKShader> _children = new(StringComparer.Ordinal);

    public SKShader? this[string name]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(name);
            return _children.TryGetValue(name, out var shader) ? shader : null;
        }
        set
        {
            ArgumentNullException.ThrowIfNull(name);
            if (value is null)
            {
                _children.Remove(name);
            }
            else
            {
                _children[name] = value;
            }
        }
    }

    public void Clear() => _children.Clear();

    public IEnumerator<string> GetEnumerator() => _children.Keys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal bool TryGetChild(string name, out SKShader? shader) =>
        _children.TryGetValue(name, out shader);

    internal IReadOnlyDictionary<string, SKShader> Items => _children;
}

public readonly struct SKRuntimeEffectUniform
{
    private readonly float[]? _floats;
    private readonly int[]? _ints;
    private readonly SKColorF? _color;

    private SKRuntimeEffectUniform(float[] values)
    {
        _floats = values;
        _ints = null;
        _color = null;
    }

    private SKRuntimeEffectUniform(int[] values)
    {
        _ints = values;
        _floats = null;
        _color = null;
    }

    private SKRuntimeEffectUniform(SKColorF color)
    {
        _color = color;
        _floats = null;
        _ints = null;
    }

    public static SKRuntimeEffectUniform Empty => default;

    public static implicit operator SKRuntimeEffectUniform(float value) =>
        new(new[] { value });

    public static implicit operator SKRuntimeEffectUniform(float[] value) =>
        new(Copy(value));

    public static implicit operator SKRuntimeEffectUniform(Span<float> value) =>
        new(Copy(value));

    public static implicit operator SKRuntimeEffectUniform(ReadOnlySpan<float> value) =>
        new(Copy(value));

    public static implicit operator SKRuntimeEffectUniform(int value) =>
        new(new[] { value });

    public static implicit operator SKRuntimeEffectUniform(int[] value) =>
        new(Copy(value));

    public static implicit operator SKRuntimeEffectUniform(Span<int> value) =>
        new(Copy(value));

    public static implicit operator SKRuntimeEffectUniform(ReadOnlySpan<int> value) =>
        new(Copy(value));

    public static implicit operator SKRuntimeEffectUniform(SKPoint value) =>
        new(new[] { value.X, value.Y });

    public static implicit operator SKRuntimeEffectUniform(SKPoint3 value) =>
        new(new[] { value.X, value.Y, value.Z });

    public static implicit operator SKRuntimeEffectUniform(SKPointI value) =>
        new(new[] { value.X, value.Y });

    public static implicit operator SKRuntimeEffectUniform(SKColor value) =>
        new((SKColorF)value);

    public static implicit operator SKRuntimeEffectUniform(SKColorF value) =>
        new(value);

    internal ReadOnlySpan<float> GetFloatValues() => _floats is null ? ReadOnlySpan<float>.Empty : _floats;

    internal ReadOnlySpan<int> GetIntValues() => _ints is null ? ReadOnlySpan<int>.Empty : _ints;

    internal SKColorF? GetColor() => _color;

    private static float[] Copy(ReadOnlySpan<float> source)
    {
        var destination = new float[source.Length];
        source.CopyTo(destination);
        return destination;
    }

    private static int[] Copy(ReadOnlySpan<int> source)
    {
        var destination = new int[source.Length];
        source.CopyTo(destination);
        return destination;
    }
}

internal interface ISkiaRuntimeEffectBackend
{
    ISkiaRuntimeEffectInstance? CompileShader(string sksl, out string? errors);
}

internal interface ISkiaRuntimeEffectInstance : IDisposable
{
    SKShader? CreateShader(SKRuntimeEffect effect, SKRuntimeEffectUniforms? uniforms, SKRuntimeEffectChildren? children, SKMatrix? localMatrix);
}
