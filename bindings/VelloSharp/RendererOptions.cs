using System;

namespace VelloSharp;

public readonly partial struct RendererOptions
{
    public RendererOptions(
        bool useCpu = false,
        bool supportArea = true,
        bool supportMsaa8 = true,
        bool supportMsaa16 = true,
        int? initThreads = null,
        RendererPipelineCache? pipelineCache = null)
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
    public RendererPipelineCache? PipelineCache { get; }

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

public readonly struct RendererPipelineCache
{
    internal RendererPipelineCache(IntPtr handle)
    {
        Handle = handle;
    }

    internal IntPtr Handle { get; }

    public bool IsNull => Handle == IntPtr.Zero;

    internal static RendererPipelineCache FromHandle(IntPtr handle)
        => new(handle);
}
