using System;
using System.Collections.Generic;
using VelloSharp;

namespace VelloSharp.WinForms;

internal sealed class WinFormsGpuResourcePool
{
    private readonly object _sync = new();
    private readonly Dictionary<ulong, Stack<WgpuBuffer>> _stagingBuffers = new();
    private WgpuPipelineCache? _pipelineCache;

    internal WgpuPipelineCache? EnsurePipelineCache(WgpuDevice device, WgpuFeature deviceFeatures, VelloGraphicsDeviceOptions options, WinFormsGpuDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (!options.EnablePipelineCaching)
        {
            return null;
        }

        if (!deviceFeatures.HasFlag(WgpuFeature.PipelineCache))
        {
            diagnostics.RecordPipelineCacheMiss();
            return null;
        }

        lock (_sync)
        {
            if (_pipelineCache is not null)
            {
                diagnostics.RecordPipelineCacheHit();
                return _pipelineCache;
            }

            diagnostics.RecordPipelineCacheMiss();

            var label = string.IsNullOrWhiteSpace(options.DiagnosticsLabel)
                ? "vello.winforms.pipeline_cache"
                : $"{options.DiagnosticsLabel}.pipeline_cache";

            var descriptor = new WgpuPipelineCacheDescriptor(label, ReadOnlyMemory<byte>.Empty, fallback: true);
            _pipelineCache = device.CreatePipelineCache(descriptor);
            return _pipelineCache;
        }
    }

    internal WinFormsGpuBufferLease RentUploadBuffer(WgpuDevice device, ulong minimumSize, WinFormsGpuDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (minimumSize == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumSize), "Minimum staging buffer size must be greater than zero.");
        }

        var bucketSize = CalculateBucketSize(minimumSize);

        lock (_sync)
        {
            if (_stagingBuffers.TryGetValue(bucketSize, out var pool) && pool.Count > 0)
            {
                var buffer = pool.Pop();
                diagnostics.RecordStagingBufferRent(bucketSize, hit: true);
                return new WinFormsGpuBufferLease(this, buffer, bucketSize, diagnostics);
            }

            var descriptor = new WgpuBufferDescriptor
            {
                Label = $"vello.winforms.staging.{bucketSize}",
                Usage = WgpuBufferUsage.CopySrc | WgpuBufferUsage.CopyDst | WgpuBufferUsage.MapRead,
                Size = bucketSize,
                MappedAtCreation = false,
            };

            var created = device.CreateBuffer(descriptor);
            diagnostics.RecordStagingBufferAllocation(bucketSize);
            diagnostics.RecordStagingBufferRent(bucketSize, hit: false);
            return new WinFormsGpuBufferLease(this, created, bucketSize, diagnostics);
        }
    }

    internal void ReturnUploadBuffer(WgpuBuffer buffer, ulong bucketSize, WinFormsGpuDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(diagnostics);

        lock (_sync)
        {
            if (!_stagingBuffers.TryGetValue(bucketSize, out var pool))
            {
                pool = new Stack<WgpuBuffer>();
                _stagingBuffers[bucketSize] = pool;
            }

            pool.Push(buffer);
            diagnostics.RecordStagingBufferReturn(bucketSize);
        }
    }

    internal void Reset()
    {
        lock (_sync)
        {
            if (_pipelineCache is not null)
            {
                _pipelineCache.Dispose();
                _pipelineCache = null;
            }

            foreach (var entry in _stagingBuffers)
            {
                while (entry.Value.Count > 0)
                {
                    entry.Value.Pop().Dispose();
                }
            }

            _stagingBuffers.Clear();
        }
    }

    private static ulong CalculateBucketSize(ulong size)
    {
        if (size <= 256)
        {
            return 256;
        }

        size--;
        size |= size >> 1;
        size |= size >> 2;
        size |= size >> 4;
        size |= size >> 8;
        size |= size >> 16;
        size |= size >> 32;
        size++;
        return size;
    }
}
