using System;

namespace HarfBuzzSharp;

public class NativeObject : IDisposable
{
    private bool _disposed;

    protected NativeObject(IntPtr handle)
    {
        Handle = handle;
    }

    public virtual IntPtr Handle { get; protected set; }

    ~NativeObject() => Dispose(false);

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (disposing)
        {
            DisposeHandler();
            Handle = IntPtr.Zero;
        }
    }

    protected virtual void DisposeHandler()
    {
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
