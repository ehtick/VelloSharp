using System;
using VelloSharp;
using VelloSharp.WinForms;
using Xunit;

namespace VelloSharp.WinForms.Tests;

public class WinFormsGpuContextTests
{
    [Fact]
    public void AcquireReleaseCreatesFreshContext()
    {
        WinFormsGpuContextLease lease1 = null;
        WinFormsGpuContextLease lease2 = null;

        try
        {
            try
            {
                lease1 = WinFormsGpuContext.Acquire();
                var context1 = lease1.Context;
                lease1.Dispose();
                lease1 = null;

                lease2 = WinFormsGpuContext.Acquire();
                var context2 = lease2.Context;

                Assert.NotSame(context1, context2);
            }
            catch (DllNotFoundException)
            {
                Console.WriteLine("Skipping GPU context test because native Vello binaries are unavailable.");
                return;
            }
        }
        finally
        {
            lease1?.Dispose();
            lease2?.Dispose();
        }
    }

    [Fact]
    public void SwapChainFormatMatchesColorSpace()
    {
        var srgbOptions = new VelloGraphicsDeviceOptions
        {
            Format = RenderFormat.Bgra8,
            ColorSpace = WinFormsColorSpace.Srgb,
        };

        Assert.Equal(WgpuTextureFormat.Bgra8UnormSrgb, srgbOptions.GetSwapChainFormat());

        var linearOptions = srgbOptions with { ColorSpace = WinFormsColorSpace.Linear };
        Assert.Equal(WgpuTextureFormat.Bgra8Unorm, linearOptions.GetSwapChainFormat());
    }

    [Fact]
    public void DiagnosticsTrackResourcePooling()
    {
        var diagnostics = new WinFormsGpuDiagnostics();
        diagnostics.RecordPipelineCacheMiss();
        diagnostics.RecordPipelineCacheHit();
        diagnostics.RecordStagingBufferAllocation(512);
        diagnostics.RecordStagingBufferRent(512, hit: false);
        diagnostics.RecordStagingBufferRent(512, hit: true);
        diagnostics.RecordStagingBufferReturn(512);
        diagnostics.RecordStagingBufferReturn(512);
        diagnostics.RecordGlyphAtlasAllocation(256);
        diagnostics.RecordGlyphAtlasRelease(256);

        Assert.Equal(1, diagnostics.PipelineCacheMisses);
        Assert.Equal(1, diagnostics.PipelineCacheHits);
        Assert.Equal(512, diagnostics.StagingBytesAllocated);
        Assert.Equal(1024, diagnostics.StagingBytesPeak);
        Assert.Equal(1, diagnostics.StagingBufferMisses);
        Assert.Equal(1, diagnostics.StagingBufferHits);
        Assert.Equal(0, diagnostics.StagingBytesInUse);
        Assert.Equal(256, diagnostics.GlyphAtlasBytesPeak);
        Assert.Equal(0, diagnostics.GlyphAtlasBytesInUse);
    }
}
