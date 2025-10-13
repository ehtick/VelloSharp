using System.Reflection;
using VelloSharp.Windows;
using Xunit;

namespace VelloSharp.Windows.Shared.Tests;

public static class DiagnosticsInvoker
{
    public static void Invoke(WindowsGpuDiagnostics diagnostics, string methodName, params object[] args)
    {
        var method = typeof(WindowsGpuDiagnostics).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(diagnostics, args);
    }
}

public class WindowsGpuDiagnosticsTests
{
    [Fact]
    public void RecordsSurfaceAndPresentationMetrics()
    {
        var diagnostics = new WindowsGpuDiagnostics();

        DiagnosticsInvoker.Invoke(diagnostics, "RecordSurfaceConfiguration", (uint)800, (uint)600);
        DiagnosticsInvoker.Invoke(diagnostics, "RecordPresentation");
        DiagnosticsInvoker.Invoke(diagnostics, "RecordDeviceReset", "device lost");
        DiagnosticsInvoker.Invoke(diagnostics, "RecordKeyedMutexTimeout");

        Assert.Equal(1, diagnostics.SurfaceConfigurations);
        Assert.Equal((800, 600), diagnostics.LastSurfaceSize);
        Assert.Equal(1, diagnostics.SwapChainPresentations);
        Assert.Equal(1, diagnostics.DeviceResets);
        Assert.Equal("Keyed mutex acquisition timed out.", diagnostics.LastError);
    }

    [Fact]
    public void TracksStagingAndGlyphMetrics()
    {
        var diagnostics = new WindowsGpuDiagnostics();

        DiagnosticsInvoker.Invoke(diagnostics, "RecordStagingBufferAllocation", (ulong)4096);
        DiagnosticsInvoker.Invoke(diagnostics, "RecordStagingBufferRent", (ulong)2048, true);
        DiagnosticsInvoker.Invoke(diagnostics, "RecordStagingBufferRent", (ulong)1024, false);
        DiagnosticsInvoker.Invoke(diagnostics, "RecordStagingBufferReturn", (ulong)2048);

        DiagnosticsInvoker.Invoke(diagnostics, "RecordGlyphAtlasAllocation", (ulong)512);
        DiagnosticsInvoker.Invoke(diagnostics, "RecordGlyphAtlasRelease", (ulong)256);

        Assert.Equal(1, diagnostics.StagingBufferHits);
        Assert.Equal(1, diagnostics.StagingBufferMisses);
        Assert.Equal(4096, diagnostics.StagingBytesAllocated);
        Assert.Equal(1024, diagnostics.StagingBytesInUse);
        Assert.True(diagnostics.StagingBytesPeak >= 2048);

        Assert.Equal(1, diagnostics.GlyphAtlasAllocations);
        Assert.Equal(1, diagnostics.GlyphAtlasReleases);
        Assert.Equal(256, diagnostics.GlyphAtlasBytesInUse);
        Assert.True(diagnostics.GlyphAtlasBytesPeak >= 512);
    }
}
