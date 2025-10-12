extern alias VSHB;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform;
using CommunityToolkit.Diagnostics;
using VelloSharp;

namespace AvaloniaVelloHarfBuzzSample.Services;

public sealed class FontAssetService : IDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<Task<FontAssetDescriptor>>> _assetCache;
    private readonly string _assemblyName;
    private readonly string? _assetRoot;
    private bool _disposed;

    public FontAssetService(string? assetRoot = null, string? assemblyName = null)
    {
        _assetRoot = assetRoot;
        _assemblyName = assemblyName ?? typeof(FontAssetService).Assembly.GetName().Name ?? "AvaloniaVelloHarfBuzzSample";
        _assetCache = new ConcurrentDictionary<string, Lazy<Task<FontAssetDescriptor>>>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<FontAssetReference> GetFontAsync(string assetKey, int faceIndex = 0, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNullOrWhiteSpace(assetKey);
        EnsureNotDisposed();

        var descriptor = await GetDescriptorAsync(assetKey, cancellationToken).ConfigureAwait(false);
        descriptor.ValidateFaceIndex(faceIndex);
        return new FontAssetReference(descriptor, faceIndex);
    }

    public async Task<IReadOnlyList<FontFaceDescriptor>> GetFaceDescriptorsAsync(string assetKey, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNullOrWhiteSpace(assetKey);
        EnsureNotDisposed();

        var descriptor = await GetDescriptorAsync(assetKey, cancellationToken).ConfigureAwait(false);
        return descriptor.GetFaceDescriptors();
    }

    public async Task<int> GetFaceCountAsync(string assetKey, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNullOrWhiteSpace(assetKey);
        EnsureNotDisposed();

        var descriptor = await GetDescriptorAsync(assetKey, cancellationToken).ConfigureAwait(false);
        return descriptor.FaceCount;
    }

    private async Task<FontAssetDescriptor> GetDescriptorAsync(string assetKey, CancellationToken cancellationToken)
    {
        var normalized = NormalizeKey(assetKey);
        var lazy = _assetCache.GetOrAdd(
            normalized,
            key => new Lazy<Task<FontAssetDescriptor>>(
                () => LoadDescriptorAsync(key, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazy.Value.ConfigureAwait(false);
        }
        catch
        {
            _assetCache.TryRemove(new KeyValuePair<string, Lazy<Task<FontAssetDescriptor>>>(normalized, lazy));
            throw;
        }
    }

    private async Task<FontAssetDescriptor> LoadDescriptorAsync(string assetKey, CancellationToken cancellationToken)
    {
        var uri = ResolveUri(assetKey);
        await using var stream = AssetLoader.Open(uri);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, 81920, cancellationToken).ConfigureAwait(false);
        var data = memory.ToArray();
        return new FontAssetDescriptor(assetKey, data);
    }

    private string NormalizeKey(string assetKey)
    {
        var trimmed = assetKey.Trim();
        if (trimmed.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return trimmed.Replace('\\', '/');
    }

    private Uri ResolveUri(string assetKey)
    {
        if (Uri.TryCreate(assetKey, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        var baseRoot = _assetRoot;
        if (string.IsNullOrWhiteSpace(baseRoot))
        {
            baseRoot = $"avares://{_assemblyName}/Assets/fonts";
        }

        if (!baseRoot.EndsWith("/", StringComparison.Ordinal))
        {
            baseRoot += "/";
        }

        var relative = assetKey.TrimStart('/', '\\');
        return new Uri(baseRoot + relative);
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(FontAssetService));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var entry in _assetCache)
        {
            if (entry.Value.IsValueCreated && entry.Value.Value.IsCompletedSuccessfully)
            {
                entry.Value.Value.Result.Dispose();
            }
        }

        _assetCache.Clear();
    }

    public readonly record struct FontFaceDescriptor(int Index, string Label);

    public sealed class FontAssetReference
    {
        internal FontAssetReference(FontAssetDescriptor descriptor, int faceIndex)
        {
            Descriptor = descriptor;
            FaceIndex = faceIndex;
        }

        internal FontAssetDescriptor Descriptor { get; }

        public string AssetKey => Descriptor.AssetKey;

        public int FaceIndex { get; }

        public int FaceCount => Descriptor.FaceCount;

        public ReadOnlyMemory<byte> FontData => Descriptor.FontData;

        public VSHB::HarfBuzzSharp.Face GetSharedFace() => Descriptor.GetSharedFace(FaceIndex);

        public FontLease CreateFontLease()
        {
            var face = Descriptor.CreateFace(FaceIndex);
            var font = new VSHB::HarfBuzzSharp.Font(face);
            font.SetFunctionsOpenType();
            return new FontLease(font, face);
        }

        public VelloSharp.Font GetSceneFont() => Descriptor.GetSceneFont(FaceIndex);
    }

    public sealed class FontLease : IDisposable
    {
        private readonly VSHB::HarfBuzzSharp.Face _face;

        internal FontLease(VSHB::HarfBuzzSharp.Font font, VSHB::HarfBuzzSharp.Face face)
        {
            Font = font;
            _face = face;
        }

        public VSHB::HarfBuzzSharp.Font Font { get; }

        public void Dispose()
        {
            Font.Dispose();
            _face.Dispose();
        }
    }

    internal sealed class FontAssetDescriptor : IDisposable
    {
        private readonly byte[] _data;
        private readonly VSHB::HarfBuzzSharp.Blob _blob;
        private readonly ConcurrentDictionary<int, VSHB::HarfBuzzSharp.Face> _sharedFaces = new();
        private readonly ConcurrentDictionary<int, Lazy<VelloSharp.Font>> _sceneFonts = new();
        private bool _disposed;

        public FontAssetDescriptor(string assetKey, byte[] data)
        {
            AssetKey = assetKey;
            _data = data ?? throw new ArgumentNullException(nameof(data));
            if (_data.Length == 0)
            {
                throw new ArgumentException("Font data cannot be empty.", nameof(data));
            }

            var handle = GCHandle.Alloc(_data, GCHandleType.Pinned);
            _blob = new VSHB::HarfBuzzSharp.Blob(handle.AddrOfPinnedObject(), _data.Length, VSHB::HarfBuzzSharp.MemoryMode.ReadOnly, () =>
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            });
            _blob.MakeImmutable();

            var faceCount = _blob.FaceCount;
            FaceCount = faceCount <= 0 ? 1 : faceCount;
        }

        public string AssetKey { get; }

        public int FaceCount { get; }

        public ReadOnlyMemory<byte> FontData => _data;

        public IReadOnlyList<FontFaceDescriptor> GetFaceDescriptors()
        {
            var result = new FontFaceDescriptor[FaceCount];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = new FontFaceDescriptor(i, $"Face {i + 1}");
            }

            return result;
        }

        public VSHB::HarfBuzzSharp.Face GetSharedFace(int faceIndex)
        {
            ValidateFaceIndex(faceIndex);
            EnsureNotDisposed();
            return _sharedFaces.GetOrAdd(faceIndex, index => new VSHB::HarfBuzzSharp.Face(_blob, index));
        }

        public VSHB::HarfBuzzSharp.Face CreateFace(int faceIndex)
        {
            ValidateFaceIndex(faceIndex);
            EnsureNotDisposed();
            return new VSHB::HarfBuzzSharp.Face(_blob, faceIndex);
        }

        public VelloSharp.Font GetSceneFont(int faceIndex)
        {
            ValidateFaceIndex(faceIndex);
            EnsureNotDisposed();

            var lazy = _sceneFonts.GetOrAdd(
                faceIndex,
                index => new Lazy<VelloSharp.Font>(
                    () => VelloSharp.Font.Load(_data, (uint)index),
                    LazyThreadSafetyMode.ExecutionAndPublication));

            return lazy.Value;
        }

        public void ValidateFaceIndex(int faceIndex)
        {
            if (faceIndex < 0 || faceIndex >= FaceCount)
            {
                throw new ArgumentOutOfRangeException(nameof(faceIndex), faceIndex, $"Face index must be between 0 and {FaceCount - 1}.");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var face in _sharedFaces.Values)
            {
                face.Dispose();
            }

            foreach (var lazy in _sceneFonts.Values)
            {
                if (lazy.IsValueCreated)
                {
                    lazy.Value.Dispose();
                }
            }

            _blob.Dispose();
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(FontAssetDescriptor));
            }
        }
    }
}
