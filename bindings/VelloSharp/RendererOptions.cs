using System;

namespace VelloSharp;

public readonly struct RendererOptions
{
    public RendererOptions(
        bool useCpu = false,
        bool supportArea = true,
        bool supportMsaa8 = true,
        bool supportMsaa16 = true,
        int? initThreads = null,
        WgpuPipelineCache? pipelineCache = null)
    {
        UseCpu = useCpu;
        SupportArea = supportArea;
        SupportMsaa8 = supportMsaa8;
        SupportMsaa16 = supportMsaa16;
        InitThreads = initThreads;
        PipelineCache = pipelineCache;
    }

    public bool UseCpu { get; }
    public bool SupportArea { get; }
    public bool SupportMsaa8 { get; }
    public bool SupportMsaa16 { get; }
    public int? InitThreads { get; }
    public WgpuPipelineCache? PipelineCache { get; }

    internal VelloRendererOptions ToNative() => new()
    {
        UseCpu = UseCpu,
        SupportArea = SupportArea,
        SupportMsaa8 = SupportMsaa8,
        SupportMsaa16 = SupportMsaa16,
        InitThreads = InitThreads ?? 0,
        PipelineCache = PipelineCache?.Handle ?? IntPtr.Zero,
    };
}
