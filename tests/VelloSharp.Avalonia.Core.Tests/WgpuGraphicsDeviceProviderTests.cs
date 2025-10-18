using System;
using System.Runtime.InteropServices;
using VelloSharp;
using VelloSharp.Avalonia.Core.Device;
using VelloSharp.Avalonia.Core.Options;
using Xunit;

public sealed class WgpuGraphicsDeviceProviderTests
{
    [Fact]
    public void Acquire_Returns_WgpuResources_When_Available()
    {
        if (!IsWgpuSupported())
        {
            return;
        }

        using var provider = new WgpuGraphicsDeviceProvider();
        var options = GraphicsDeviceOptions.CreateDefault(GraphicsBackendKind.VelloWgpu);

        using var lease = provider.Acquire(options);
        Assert.True(lease.TryGetWgpuResources(out var resources));

        Assert.NotNull(resources.Device);
        Assert.NotNull(resources.Queue);
        Assert.NotNull(resources.Renderer);
    }

    private static bool IsWgpuSupported()
    {
        try
        {
            using var instance = new WgpuInstance();
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }
}
