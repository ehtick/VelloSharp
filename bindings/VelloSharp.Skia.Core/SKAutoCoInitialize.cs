using System;

namespace SkiaSharp;

public sealed class SKAutoCoInitialize : IDisposable
{
    public SKAutoCoInitialize()
    {
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
