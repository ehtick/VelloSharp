using System;

namespace VelloSharp.Windows.Shared.Dispatching;

public interface IVelloWindowsDispatcher
{
    bool HasThreadAccess { get; }
    bool TryEnqueue(Action callback);
    IVelloWindowsDispatcherTimer CreateTimer();
}

public interface IVelloWindowsDispatcherProvider
{
    IVelloWindowsDispatcher? GetForCurrentThread();
}

public static class VelloWindowsDispatcher
{
    private sealed class NullProvider : IVelloWindowsDispatcherProvider
    {
        internal static readonly NullProvider Instance = new();

        public IVelloWindowsDispatcher? GetForCurrentThread() => null;
    }

    private static readonly object Sync = new();
    private static IVelloWindowsDispatcherProvider _provider = NullProvider.Instance;

    public static IVelloWindowsDispatcherProvider Provider
    {
        get
        {
            lock (Sync)
            {
                return _provider;
            }
        }
        set => TrySetProvider(value, overwrite: true);
    }

    public static IVelloWindowsDispatcher? GetForCurrentThread()
    {
        lock (Sync)
        {
            return _provider.GetForCurrentThread();
        }
    }

    public static bool TrySetProvider(IVelloWindowsDispatcherProvider provider, bool overwrite = false)
    {
        if (provider is null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        lock (Sync)
        {
            if (!overwrite && !ReferenceEquals(_provider, NullProvider.Instance))
            {
                return false;
            }

            _provider = provider;
            return true;
        }
    }
}
