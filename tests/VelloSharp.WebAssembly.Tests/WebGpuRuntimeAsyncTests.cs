using System;
using System.Threading.Tasks;
using VelloSharp;
using Xunit;
using System.Runtime.Versioning;

namespace VelloSharp.WebAssembly.Tests;

[CollectionDefinition(nameof(WebGpuRuntimeTestsCollection), DisableParallelization = true)]
public sealed class WebGpuRuntimeTestsCollection : ICollectionFixture<WebGpuRuntimeTestFixture>
{
}

public sealed class WebGpuRuntimeTestFixture : IDisposable
{
    public WebGpuRuntimeTestFixture()
    {
        WebGpuRuntimeTestHooks.ResetAll();
        WebGpuRuntimeTestHooks.SuppressLogCallbackRegistration = true;
    }

    public void Dispose()
    {
        WebGpuRuntimeTestHooks.ResetAll();
    }
}

[Collection(nameof(WebGpuRuntimeTestsCollection))]
[SupportedOSPlatform("browser")]
public sealed class WebGpuRuntimeAsyncTests
{
    [Fact]
    public async Task RequestAdapterAsync_CompletesAfterPendingPolls()
    {
        WebGpuRuntimeTestHooks.ResetAll();
        WebGpuRuntimeTestHooks.SuppressLogCallbackRegistration = true;
        WebGpuRuntimeTestHooks.InitializeOverride = () => WebGpuNativeMethods.VelloWebGpuStatus.Success;
        WebGpuRuntimeTestHooks.RequestAdapterAsyncOverride = _ => (WebGpuNativeMethods.VelloWebGpuStatus.Success, 1u);

        var pollCount = 0;
        WebGpuRuntimeTestHooks.FuturePollOverride = _ =>
        {
            pollCount++;
            if (pollCount < 3)
            {
                return (WebGpuNativeMethods.VelloWebGpuStatus.Success, new WebGpuNativeMethods.VelloWebGpuFuturePollResultNative
                {
                    State = WebGpuNativeMethods.VelloWebGpuFutureState.Pending,
                    Kind = WebGpuNativeMethods.VelloWebGpuFutureKind.Adapter,
                });
            }

            return (WebGpuNativeMethods.VelloWebGpuStatus.Success, new WebGpuNativeMethods.VelloWebGpuFuturePollResultNative
            {
                State = WebGpuNativeMethods.VelloWebGpuFutureState.Ready,
                Kind = WebGpuNativeMethods.VelloWebGpuFutureKind.Adapter,
                AdapterHandle = 0xABCDEFu,
            });
        };

        var handle = await WebGpuRuntime.RequestAdapterAsync();

        Assert.Equal<uint?>(0xABCDEFu, handle);
        Assert.Equal(3, pollCount);
    }

    [Fact]
    public async Task RequestAdapterAsync_ThrowsWhenFutureFails()
    {
        WebGpuRuntimeTestHooks.ResetAll();
        WebGpuRuntimeTestHooks.SuppressLogCallbackRegistration = true;
        WebGpuRuntimeTestHooks.InitializeOverride = () => WebGpuNativeMethods.VelloWebGpuStatus.Success;
        WebGpuRuntimeTestHooks.RequestAdapterAsyncOverride = _ => (WebGpuNativeMethods.VelloWebGpuStatus.Success, 99u);
        WebGpuRuntimeTestHooks.FuturePollOverride = _ => (WebGpuNativeMethods.VelloWebGpuStatus.Success, new WebGpuNativeMethods.VelloWebGpuFuturePollResultNative
        {
            State = WebGpuNativeMethods.VelloWebGpuFutureState.Failed,
            Kind = WebGpuNativeMethods.VelloWebGpuFutureKind.Adapter,
        });
        WebGpuRuntimeTestHooks.LastErrorMessageOverride = () => "Future registry reported failure.";

        await Assert.ThrowsAsync<WebGpuInteropException>(() => WebGpuRuntime.RequestAdapterAsync());
    }
}
