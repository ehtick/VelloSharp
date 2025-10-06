using System;
using System.Threading;
using VelloSharp;

namespace VelloSharp.Windows;

public sealed class WindowsGpuDiagnostics
{
    private long _surfaceConfigurations;
    private long _swapChainPresentations;
    private long _deviceResets;
    private long _pipelineCacheHits;
    private long _pipelineCacheMisses;
    private long _stagingBufferHits;
    private long _stagingBufferMisses;
    private long _stagingBytesAllocated;
    private long _stagingBytesInUse;
    private long _stagingBytesPeak;
    private long _glyphAtlasBytesInUse;
    private long _glyphAtlasBytesPeak;
    private long _glyphAtlasAllocations;
    private long _glyphAtlasReleases;
    private long _adapterMismatches;
    private long _lastExpectedAdapterLuid;
    private long _lastActualAdapterLuid;
    private long _keyedMutexTimeouts;
    private long _keyedMutexFallbacks;
    private long _sharedTextureFailures;
    private string? _lastError;
    private int _lastSurfaceWidth;
    private int _lastSurfaceHeight;

    public long SurfaceConfigurations => Interlocked.Read(ref _surfaceConfigurations);

    public long SwapChainPresentations => Interlocked.Read(ref _swapChainPresentations);

    public long DeviceResets => Interlocked.Read(ref _deviceResets);

    public long PipelineCacheHits => Interlocked.Read(ref _pipelineCacheHits);

    public long PipelineCacheMisses => Interlocked.Read(ref _pipelineCacheMisses);

    public long StagingBufferHits => Interlocked.Read(ref _stagingBufferHits);

    public long StagingBufferMisses => Interlocked.Read(ref _stagingBufferMisses);

    public long StagingBytesAllocated => Interlocked.Read(ref _stagingBytesAllocated);

    public long StagingBytesInUse => Interlocked.Read(ref _stagingBytesInUse);

    public long StagingBytesPeak => Interlocked.Read(ref _stagingBytesPeak);

    public long GlyphAtlasBytesInUse => Interlocked.Read(ref _glyphAtlasBytesInUse);

    public long GlyphAtlasBytesPeak => Interlocked.Read(ref _glyphAtlasBytesPeak);

    public long GlyphAtlasAllocations => Interlocked.Read(ref _glyphAtlasAllocations);

    public long GlyphAtlasReleases => Interlocked.Read(ref _glyphAtlasReleases);

    public long AdapterMismatchCount => Interlocked.Read(ref _adapterMismatches);

    public long KeyedMutexTimeouts => Interlocked.Read(ref _keyedMutexTimeouts);

    public long KeyedMutexFallbacks => Interlocked.Read(ref _keyedMutexFallbacks);

    public long SharedTextureFailures => Interlocked.Read(ref _sharedTextureFailures);

    public (AdapterLuid Expected, AdapterLuid Actual) LastAdapterMismatch
    {
        get
        {
            var expected = Interlocked.Read(ref _lastExpectedAdapterLuid);
            var actual = Interlocked.Read(ref _lastActualAdapterLuid);
            return (UnpackLuid(expected), UnpackLuid(actual));
        }
    }

    public string? LastError => Interlocked.CompareExchange(ref _lastError, null, null);

    public (int Width, int Height) LastSurfaceSize =>
        (Interlocked.CompareExchange(ref _lastSurfaceWidth, 0, 0),
         Interlocked.CompareExchange(ref _lastSurfaceHeight, 0, 0));

    internal void RecordSurfaceConfiguration(uint width, uint height)
    {
        Interlocked.Increment(ref _surfaceConfigurations);
        Interlocked.Exchange(ref _lastSurfaceWidth, (int)width);
        Interlocked.Exchange(ref _lastSurfaceHeight, (int)height);
    }

    internal void RecordPresentation()
        => Interlocked.Increment(ref _swapChainPresentations);

    internal void RecordDeviceReset(string? error = null)
    {
        Interlocked.Increment(ref _deviceResets);
        if (error is not null)
        {
            Interlocked.Exchange(ref _lastError, error);
        }
    }

    internal void RecordPipelineCacheHit()
        => Interlocked.Increment(ref _pipelineCacheHits);

    internal void RecordPipelineCacheMiss()
        => Interlocked.Increment(ref _pipelineCacheMisses);

    internal void RecordStagingBufferAllocation(ulong bytes)
        => Interlocked.Add(ref _stagingBytesAllocated, (long)bytes);

    internal void RecordStagingBufferRent(ulong bytes, bool hit)
    {
        if (hit)
        {
            Interlocked.Increment(ref _stagingBufferHits);
        }
        else
        {
            Interlocked.Increment(ref _stagingBufferMisses);
        }

        var inUse = Interlocked.Add(ref _stagingBytesInUse, (long)bytes);
        UpdatePeak(ref _stagingBytesPeak, inUse);
    }

    internal void RecordStagingBufferReturn(ulong bytes)
        => Interlocked.Add(ref _stagingBytesInUse, -(long)bytes);

    internal void RecordGlyphAtlasAllocation(ulong bytes)
    {
        Interlocked.Increment(ref _glyphAtlasAllocations);
        var inUse = Interlocked.Add(ref _glyphAtlasBytesInUse, (long)bytes);
        UpdatePeak(ref _glyphAtlasBytesPeak, inUse);
    }

    internal void RecordGlyphAtlasRelease(ulong bytes)
    {
        Interlocked.Increment(ref _glyphAtlasReleases);
        Interlocked.Add(ref _glyphAtlasBytesInUse, -(long)bytes);
    }

    internal void RecordAdapterMismatch(AdapterLuid expected, AdapterLuid actual)
    {
        Interlocked.Increment(ref _adapterMismatches);
        Interlocked.Exchange(ref _lastExpectedAdapterLuid, PackLuid(expected));
        Interlocked.Exchange(ref _lastActualAdapterLuid, PackLuid(actual));
    }

    internal void RecordKeyedMutexTimeout()
    {
        Interlocked.Increment(ref _keyedMutexTimeouts);
        Interlocked.Exchange(ref _lastError, "Keyed mutex acquisition timed out.");
    }

    internal void RecordKeyedMutexFallback(string? reason = null)
    {
        Interlocked.Increment(ref _keyedMutexFallbacks);
        if (!string.IsNullOrWhiteSpace(reason))
        {
            Interlocked.Exchange(ref _lastError, reason);
        }
    }

    internal void RecordSharedTextureFailure(string message)
    {
        Interlocked.Increment(ref _sharedTextureFailures);
        Interlocked.Exchange(ref _lastError, message);
    }

    private static void UpdatePeak(ref long target, long candidate)
    {
        while (true)
        {
            var observed = Interlocked.Read(ref target);
            if (observed >= candidate)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref target, candidate, observed) == observed)
            {
                return;
            }
        }
    }

    private static long PackLuid(AdapterLuid luid)
        => ((long)luid.High << 32) | luid.Low;

    private static AdapterLuid UnpackLuid(long value)
        => new((int)(value >> 32), (uint)(value & 0xFFFF_FFFF));
}
