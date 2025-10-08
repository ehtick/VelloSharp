using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace VelloSharp.TreeDataGrid.Templates;

public readonly record struct TreeTemplateCompileOptions(
    string TemplateId,
    TreeFrozenKind PaneKind,
    int Generation,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed class TreeTemplateCompiler
{
    private readonly TreeTemplateCache _cache;

    public TreeTemplateCompiler()
        : this(new TreeTemplateCache())
    {
    }

    public TreeTemplateCompiler(TreeTemplateCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public TreeCompiledTemplate Compile(string xaml, in TreeTemplateCompileOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TemplateId))
        {
            throw new ArgumentException("Template identifier must be provided.", nameof(options));
        }

        var contentHash = TreeTemplateHash.Compute(xaml);
        var cacheKey = new TreeTemplateCacheKey(options.TemplateId, options.PaneKind, contentHash);
        if (_cache.TryGet(cacheKey, options.Generation, out var cached))
        {
            return cached;
        }

        var syntaxTree = TreeTemplateXamlParser.Parse(xaml);
        var expression = TreeTemplateExpressionBuilder.Build(syntaxTree);
        return CompileInternal(expression, options, cacheKey);
    }

    internal TreeCompiledTemplate Compile(TreeTemplateExpression expression, in TreeTemplateCompileOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TemplateId))
        {
            throw new ArgumentException("Template identifier must be provided.", nameof(options));
        }

        var contentHash = TreeTemplateHash.Compute(expression);
        var cacheKey = new TreeTemplateCacheKey(options.TemplateId, options.PaneKind, contentHash);
        if (_cache.TryGet(cacheKey, options.Generation, out var cached))
        {
            return cached;
        }

        return CompileInternal(expression, options, cacheKey);
    }

    public void Invalidate(string templateId)
    {
        if (!string.IsNullOrWhiteSpace(templateId))
        {
            _cache.Invalidate(templateId);
        }
    }

    private TreeCompiledTemplate CompileInternal(
        TreeTemplateExpression expression,
        in TreeTemplateCompileOptions options,
        TreeTemplateCacheKey cacheKey)
    {
        var instructions = TreeTemplateInstructionEmitter.Emit(expression).ToArray();

        var compiled = new TreeCompiledTemplate(
            cacheKey,
            options.Generation,
            instructions,
            expression,
            options.Metadata ?? TreeTemplateMetadata.Empty);

        _cache.Store(compiled);
        return compiled;
    }
}

internal static class TreeTemplateHash
{
    public static string Compute(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        if (!TryHash(bytes, out var hash))
        {
            hash = SHA256.HashData(bytes);
        }

        return Convert.ToHexString(hash);
    }

    private static bool TryHash(ReadOnlySpan<byte> bytes, out byte[] hash)
    {
        hash = ArrayPool<byte>.Shared.Rent(SHA256.HashSizeInBytes);
        try
        {
            if (!SHA256.TryHashData(bytes, hash, out var written) || written != SHA256.HashSizeInBytes)
            {
                return false;
            }

            if (written != hash.Length)
            {
                var trimmed = new byte[written];
                Buffer.BlockCopy(hash, 0, trimmed, 0, written);
                ArrayPool<byte>.Shared.Return(hash);
                hash = trimmed;
            }

            return true;
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(hash);
            hash = Array.Empty<byte>();
            return false;
        }
    }

    public static string Compute(TreeTemplateExpression expression)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendExpression(hash, expression);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void AppendExpression(IncrementalHash hash, TreeTemplateExpression expression)
    {
        AppendString(hash, expression.Kind.ToString());
        foreach (var property in expression.Properties.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            AppendString(hash, property.Key);
            AppendValue(hash, property.Value);
        }

        foreach (var child in expression.Children)
        {
            AppendExpression(hash, child);
        }
    }

    private static void AppendValue(IncrementalHash hash, TreeTemplateValue value)
    {
        hash.AppendData(stackalloc byte[] { unchecked((byte)value.Kind) });
        AppendString(hash, value.Raw);
        AppendString(hash, value.BindingPath);
    }

    private static void AppendString(IncrementalHash hash, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            hash.AppendData(bytes);
        }
    }
}

public readonly record struct TreeTemplateCacheKey(string TemplateId, TreeFrozenKind PaneKind, string ContentHash);

public sealed class TreeTemplateCache
{
    private readonly ConcurrentDictionary<TreeTemplateCacheKey, TreeCompiledTemplate> _templates = new();

    public bool TryGet(in TreeTemplateCacheKey key, int generation, [NotNullWhen(true)] out TreeCompiledTemplate? template)
    {
        if (_templates.TryGetValue(key, out template))
        {
            if (template.Generation == generation)
            {
                return true;
            }

            _templates.TryRemove(key, out _);
        }

        template = null;
        return false;
    }

    public void Store(TreeCompiledTemplate template)
    {
        _templates[template.CacheKey] = template;
    }

    public void Invalidate(string templateId)
    {
        foreach (var key in _templates.Keys)
        {
            if (string.Equals(key.TemplateId, templateId, StringComparison.Ordinal))
            {
                _templates.TryRemove(key, out _);
            }
        }
    }
}

public sealed class TreeCompiledTemplate
{
    private readonly TreeTemplateInstruction[] _instructions;

    internal TreeCompiledTemplate(
        TreeTemplateCacheKey cacheKey,
        int generation,
        TreeTemplateInstruction[] instructions,
        TreeTemplateExpression expression,
        IReadOnlyDictionary<string, string> metadata)
    {
        CacheKey = cacheKey;
        Generation = generation;
        _instructions = instructions;
        Expression = expression;
        Metadata = metadata;
    }

    internal TreeTemplateCacheKey CacheKey { get; }
    public int Generation { get; }
    internal TreeTemplateExpression Expression { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
    public ReadOnlySpan<TreeTemplateInstruction> InstructionSpan => _instructions;

    public ReadOnlyMemory<TreeTemplateInstruction> Instructions => _instructions;

    public TreeTemplateRuntimeHandle Bind(TreeTemplateRuntime runtime, in TreeTemplateRuntimeContext context)
    {
        if (runtime is null)
        {
            throw new ArgumentNullException(nameof(runtime));
        }

        return runtime.BindTemplate(this, context);
    }
}

internal static class TreeTemplateMetadata
{
    public static IReadOnlyDictionary<string, string> Empty { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
