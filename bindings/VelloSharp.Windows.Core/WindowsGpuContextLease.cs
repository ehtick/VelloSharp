using System;

namespace VelloSharp.Windows;

public sealed class WindowsGpuContextLease : IDisposable
{
    private WindowsGpuContext? _context;

    internal WindowsGpuContextLease(WindowsGpuContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public WindowsGpuContext Context
        => _context ?? throw new ObjectDisposedException(nameof(WindowsGpuContextLease));

    public void Dispose()
    {
        if (_context is { } ctx)
        {
            ctx.Release();
            _context = null;
        }
    }
}
