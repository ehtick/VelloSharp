using System;
using VelloSharp;
using VelloSharp.WinForms;
using Xunit;

namespace VelloSharp.WinForms.Tests;

public class VelloGraphicsDeviceTests
{
    [Fact]
    public void BeginSession_RespectsDimensions()
    {
        try
        {
            using var device = new VelloGraphicsDevice(32, 32);
            using var session = device.BeginSession(128, 64);

            Assert.Equal(128u, session.Width);
            Assert.Equal(64u, session.Height);
            Assert.False(session.IsSubmitted);

            var buffer = new byte[128 * 64 * 4];
            session.Submit(buffer, 128 * 4);
            Assert.True(session.IsSubmitted);
        }
        catch (DllNotFoundException)
        {
            Console.WriteLine("Skipping GPU shim test because native Vello binaries are unavailable.");
            return;
        }
    }

    [Fact]
    public void GraphicsDeviceOptionsReflectMsaaSampleCount()
    {
        var options = new VelloGraphicsDeviceOptions { MsaaSampleCount = 16 };
        Assert.Equal(AntialiasingMode.Msaa16, options.GetAntialiasingMode());

        options = options with { MsaaSampleCount = 8 };
        Assert.Equal(AntialiasingMode.Msaa8, options.GetAntialiasingMode());

        options = options with { MsaaSampleCount = null, Antialiasing = AntialiasingMode.Area };
        Assert.Equal(AntialiasingMode.Area, options.GetAntialiasingMode());
    }
}
