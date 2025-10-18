namespace VelloSharp.Avalonia.Core.Options;

/// <summary>
/// Represents the rendering backend that will service Avalonia draw calls.
/// </summary>
public enum GraphicsBackendKind
{
    /// <summary>
    /// Vello renderer powered by WebGPU.
    /// </summary>
    VelloWgpu,

    /// <summary>
    /// Skia renderer backed by a GPU context.
    /// </summary>
    SkiaGpu,

    /// <summary>
    /// Skia renderer using CPU rasterization.
    /// </summary>
    SkiaCpu,
}
