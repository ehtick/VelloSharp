#nullable enable
using System;
using VelloSharp.Windows.Shared.Dispatching;
using Xunit;

namespace VelloSharp.Windows.Shared.Tests;

public sealed class VelloWindowsDispatcherTests : IDisposable
{
    private readonly IVelloWindowsDispatcherProvider _originalProvider;

    public VelloWindowsDispatcherTests()
    {
        _originalProvider = VelloWindowsDispatcher.Provider;
    }

    [Fact]
    public void TrySetProviderSetsOnce()
    {
        var provider = new StubDispatcherProvider();
        Assert.True(VelloWindowsDispatcher.TrySetProvider(provider, overwrite: true));
        Assert.False(VelloWindowsDispatcher.TrySetProvider(new StubDispatcherProvider()));
        Assert.Same(provider, VelloWindowsDispatcher.Provider);
    }

    [Fact]
    public void GetForCurrentThreadReturnsProviderValue()
    {
        var dispatcher = new StubDispatcher();
        var provider = new StubDispatcherProvider(() => dispatcher);

        Assert.True(VelloWindowsDispatcher.TrySetProvider(provider, overwrite: true));
        Assert.Same(dispatcher, VelloWindowsDispatcher.GetForCurrentThread());
    }

    public void Dispose()
    {
        VelloWindowsDispatcher.Provider = _originalProvider;
    }

    private sealed class StubDispatcherProvider : IVelloWindowsDispatcherProvider
    {
        private readonly Func<IVelloWindowsDispatcher?> _factory;

        public StubDispatcherProvider()
            : this(() => null)
        {
        }

        public StubDispatcherProvider(Func<IVelloWindowsDispatcher?> factory)
        {
            _factory = factory;
        }

        public IVelloWindowsDispatcher? GetForCurrentThread() => _factory();
    }

    private sealed class StubDispatcher : IVelloWindowsDispatcher
    {
        public bool HasThreadAccess => true;

        public bool TryEnqueue(Action callback)
        {
            callback?.Invoke();
            return true;
        }

        public IVelloWindowsDispatcherTimer CreateTimer() => new StubDispatcherTimer();

        private sealed class StubDispatcherTimer : IVelloWindowsDispatcherTimer
        {
            public TimeSpan Interval { get; set; }

            public bool IsRepeating { get; set; }

            public event EventHandler? Tick;

            public void Dispose()
            {
            }

            public void Start() => Tick?.Invoke(this, EventArgs.Empty);

            public void Stop()
            {
            }
        }
    }
}
