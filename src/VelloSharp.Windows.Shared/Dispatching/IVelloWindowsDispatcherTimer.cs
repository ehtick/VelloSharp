using System;

namespace VelloSharp.Windows.Shared.Dispatching;

public interface IVelloWindowsDispatcherTimer : IDisposable
{
    TimeSpan Interval { get; set; }
    bool IsRepeating { get; set; }
    event EventHandler? Tick;
    void Start();
    void Stop();
}
