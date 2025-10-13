using System;

namespace VelloSharp.Windows.Shared.Dispatching;

public interface IVelloCompositionTarget
{
    void AddRenderingHandler(EventHandler<object> handler);
    void RemoveRenderingHandler(EventHandler<object> handler);
}

public interface IVelloCompositionTargetProvider
{
    IVelloCompositionTarget? GetForCurrentThread();
}

public static class VelloCompositionTarget
{
    private sealed class NullProvider : IVelloCompositionTargetProvider
    {
        internal static readonly NullProvider Instance = new();

        public IVelloCompositionTarget? GetForCurrentThread() => null;
    }

    private static readonly object Sync = new();
    private static IVelloCompositionTargetProvider _provider = NullProvider.Instance;

    public static IVelloCompositionTargetProvider Provider
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

    public static IVelloCompositionTarget? GetForCurrentThread()
    {
        lock (Sync)
        {
            return _provider.GetForCurrentThread();
        }
    }

    public static bool TrySetProvider(IVelloCompositionTargetProvider provider, bool overwrite = false)
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
