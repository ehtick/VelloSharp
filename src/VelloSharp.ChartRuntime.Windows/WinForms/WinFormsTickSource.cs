using System;
using System.Threading;
using System.Windows.Forms;
using VelloSharp.ChartRuntime;

namespace VelloSharp.ChartRuntime.Windows.WinForms;

/// <summary>
/// Schedules ticks through a WinForms control using BeginInvoke to align with the UI loop.
/// </summary>
public sealed class WinFormsTickSource : IFrameTickSource
{
    private readonly Control _control;
    private int _state;
    private bool _disposed;

    public WinFormsTickSource(Control control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
    }

    public event Action? Tick;

    public void RequestTick()
    {
        if (_disposed)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
        {
            return;
        }

        if (_control.IsDisposed)
        {
            Interlocked.Exchange(ref _state, 0);
            return;
        }

        _control.BeginInvoke(new Action(OnTick));
    }

    private void OnTick()
    {
        if (_disposed)
        {
            Interlocked.Exchange(ref _state, 0);
            return;
        }

        try
        {
            Tick?.Invoke();
        }
        finally
        {
            Interlocked.Exchange(ref _state, 0);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        Interlocked.Exchange(ref _state, 0);
    }
}
