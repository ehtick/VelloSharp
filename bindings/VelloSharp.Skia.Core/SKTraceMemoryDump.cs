using System;

namespace SkiaSharp;

public class SKTraceMemoryDump : IDisposable
{
    private bool _disposed;
    public bool DetailedDump { get; }
    public bool DumpWrappedObjects { get; }

    public SKTraceMemoryDump(bool detailedDump = false, bool dumpWrappedObjects = false)
    {
        DetailedDump = detailedDump;
        DumpWrappedObjects = dumpWrappedObjects;
    }

    public virtual void OnDumpNumericValue(string dumpName, string valueName, string units, ulong value)
    {
        _ = dumpName;
        _ = valueName;
        _ = units;
        _ = value;
    }

    public virtual void OnDumpStringValue(string dumpName, string valueName, string value)
    {
        _ = dumpName;
        _ = valueName;
        _ = value;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
