#nullable enable

using System;
using VelloSharp;
using VelloSharp.Uno.Controls;
using VelloSharp.WinForms.Integration;
using VelloSharp.Windows;
using Xunit;

namespace VelloSharp.Windows.Core.Tests;

public sealed class VelloSwapChainPresenterTests : IDisposable
{
    public VelloSwapChainPresenterTests()
    {
        VelloSwapChainPresenter.AcquireContext = _ => null;
    }

    [Fact]
    public void OnLoaded_AppliesSkiaOptOutWhenGpuPreferred()
    {
        var host = new TestSwapChainPresenterHost
        {
            PreferredBackend = VelloRenderBackend.Gpu,
            RenderMode = VelloRenderMode.OnDemand,
            RenderLoopDriver = RenderLoopDriver.None,
        };
        using var presenter = new VelloSwapChainPresenter(host, new TestSurfaceSource());

        presenter.OnLoaded();

        Assert.Equal(1, host.ApplySkiaOptOutCount);
        Assert.Equal(0, host.RemoveSkiaOptOutCount);
    }

    [Fact]
    public void OnUnloaded_RemovesSkiaOptOutOnce()
    {
        var host = new TestSwapChainPresenterHost
        {
            PreferredBackend = VelloRenderBackend.Gpu,
            RenderMode = VelloRenderMode.OnDemand,
        };
        using var presenter = new VelloSwapChainPresenter(host, new TestSurfaceSource());

        presenter.OnLoaded();
        presenter.OnUnloaded();

        Assert.Equal(1, host.ApplySkiaOptOutCount);
        Assert.Equal(1, host.RemoveSkiaOptOutCount);
    }

    [Fact]
    public void SwitchingToCpuBackend_RemovesSkiaOptOut()
    {
        var host = new TestSwapChainPresenterHost
        {
            PreferredBackend = VelloRenderBackend.Gpu,
            RenderMode = VelloRenderMode.OnDemand,
        };
        using var presenter = new VelloSwapChainPresenter(host, new TestSurfaceSource());

        presenter.OnLoaded();
        host.PreferredBackend = VelloRenderBackend.Cpu;
        presenter.OnPreferredBackendChanged();

        Assert.Equal(1, host.ApplySkiaOptOutCount);
        Assert.Equal(1, host.RemoveSkiaOptOutCount);
    }

    public void Dispose()
    {
        VelloSwapChainPresenter.ResetTestingHooks();
    }

    private sealed class TestSurfaceSource : IWindowsSurfaceSource
    {
        public WindowsSurfaceDescriptor GetSurfaceDescriptor()
            => new(WindowsSurfaceKind.SwapChainPanel, 1);

        public WindowsSurfaceSize GetSurfaceSize()
            => WindowsSurfaceSize.Empty;
    }
}




