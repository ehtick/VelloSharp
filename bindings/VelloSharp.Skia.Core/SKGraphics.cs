using System;

namespace SkiaSharp;

public static class SKGraphics
{
    private static long s_fontCacheLimit = 32L * 1024 * 1024;
    private static long s_fontCacheUsed;
    private static int s_fontCacheCountLimit = 1024;
    private static int s_fontCacheCountUsed;
    private static long s_resourceCacheTotalByteLimit = 96L * 1024 * 1024;
    private static long s_resourceCacheTotalBytesUsed;
    private static long s_resourceCacheSingleAllocationByteLimit = 16L * 1024 * 1024;

    public static void Init()
    {
    }

    public static void PurgeFontCache()
    {
        s_fontCacheUsed = 0;
        s_fontCacheCountUsed = 0;
    }

    public static void PurgeResourceCache() => s_resourceCacheTotalBytesUsed = 0;

    public static void PurgeAllCaches()
    {
        PurgeFontCache();
        PurgeResourceCache();
    }

    public static long GetFontCacheUsed() => s_fontCacheUsed;

    public static long GetFontCacheLimit() => s_fontCacheLimit;

    public static long SetFontCacheLimit(long bytes)
    {
        var previous = s_fontCacheLimit;
        s_fontCacheLimit = Math.Max(0, bytes);
        if (s_fontCacheUsed > s_fontCacheLimit)
        {
            s_fontCacheUsed = s_fontCacheLimit;
        }
        return previous;
    }

    public static int GetFontCacheCountUsed() => s_fontCacheCountUsed;

    public static int GetFontCacheCountLimit() => s_fontCacheCountLimit;

    public static int SetFontCacheCountLimit(int count)
    {
        var previous = s_fontCacheCountLimit;
        s_fontCacheCountLimit = Math.Max(0, count);
        if (s_fontCacheCountUsed > s_fontCacheCountLimit)
        {
            s_fontCacheCountUsed = s_fontCacheCountLimit;
        }
        return previous;
    }

    public static long GetResourceCacheTotalBytesUsed() => s_resourceCacheTotalBytesUsed;

    public static long GetResourceCacheTotalByteLimit() => s_resourceCacheTotalByteLimit;

    public static long SetResourceCacheTotalByteLimit(long bytes)
    {
        var previous = s_resourceCacheTotalByteLimit;
        s_resourceCacheTotalByteLimit = Math.Max(0, bytes);
        if (s_resourceCacheTotalBytesUsed > s_resourceCacheTotalByteLimit)
        {
            s_resourceCacheTotalBytesUsed = s_resourceCacheTotalByteLimit;
        }
        return previous;
    }

    public static long GetResourceCacheSingleAllocationByteLimit() => s_resourceCacheSingleAllocationByteLimit;

    public static long SetResourceCacheSingleAllocationByteLimit(long bytes)
    {
        var previous = s_resourceCacheSingleAllocationByteLimit;
        s_resourceCacheSingleAllocationByteLimit = Math.Max(0, bytes);
        return previous;
    }

    public static void DumpMemoryStatistics(SKTraceMemoryDump dump)
    {
        if (dump is null)
        {
            throw new ArgumentNullException(nameof(dump));
        }

        dump.OnDumpNumericValue("skia/font_cache", "limit", "bytes", (ulong)s_fontCacheLimit);
        dump.OnDumpNumericValue("skia/font_cache", "used", "bytes", (ulong)s_fontCacheUsed);
        dump.OnDumpNumericValue("skia/resource_cache", "limit", "bytes", (ulong)s_resourceCacheTotalByteLimit);
        dump.OnDumpNumericValue("skia/resource_cache", "used", "bytes", (ulong)s_resourceCacheTotalBytesUsed);
    }
}
