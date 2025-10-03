namespace VelloSharp;

public static class RendererOptionsExtensions
{
    public static RendererOptions CreateGpuOptions(
        bool useCpu = false,
        bool supportArea = true,
        bool supportMsaa8 = true,
        bool supportMsaa16 = true,
        int? initThreads = null,
        WgpuPipelineCache? pipelineCache = null)
        => new RendererOptions(
            useCpu,
            supportArea,
            supportMsaa8,
            supportMsaa16,
            initThreads,
            pipelineCache is null ? null : RendererPipelineCache.FromHandle(pipelineCache.Handle));

    public static RendererOptions WithPipelineCache(this RendererOptions options, WgpuPipelineCache? pipelineCache)
        => new RendererOptions(
            options.UseCpu,
            options.SupportArea,
            options.SupportMsaa8,
            options.SupportMsaa16,
            options.InitThreads,
            pipelineCache is null ? null : RendererPipelineCache.FromHandle(pipelineCache.Handle));
}
