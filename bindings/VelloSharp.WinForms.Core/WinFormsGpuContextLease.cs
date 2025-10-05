using System;

namespace VelloSharp.WinForms;

public sealed class WinFormsGpuContextLease : IDisposable
{
    private WinFormsGpuContext? _context;

    internal WinFormsGpuContextLease(WinFormsGpuContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public WinFormsGpuContext Context
        => _context ?? throw new ObjectDisposedException(nameof(WinFormsGpuContextLease));

    public void Dispose()
    {
        if (_context is { } ctx)
        {
            ctx.Release();
            _context = null;
        }
    }
}
