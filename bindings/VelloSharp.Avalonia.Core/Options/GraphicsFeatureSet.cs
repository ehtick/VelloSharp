namespace VelloSharp.Avalonia.Core.Options;

/// <summary>
/// Describes high-level rendering features that callers may toggle regardless of the active backend.
/// </summary>
/// <param name="EnableCpuFallback">
/// When true, the pipeline is permitted to fall back to a CPU renderer if a GPU context cannot be created.
/// </param>
/// <param name="EnableMsaa8">Enables 8x multi-sample anti-aliasing when the backend supports it.</param>
/// <param name="EnableMsaa16">Enables 16x multi-sample anti-aliasing when the backend supports it.</param>
/// <param name="EnableAreaAa">Enables analytical/area anti-aliasing paths.</param>
/// <param name="EnableOpacityLayers">
/// Requests opacity save-layers; backends deciding not to support them may ignore the flag.
/// </param>
/// <param name="MaxGpuResourceBytes">Upper bound for GPU resource allocations; ignored when not applicable.</param>
/// <param name="EnableValidationLayers">
/// Enables extra backend validation (e.g. WebGPU validation layers) when available.
/// </param>
public sealed record GraphicsFeatureSet(
    bool EnableCpuFallback = true,
    bool EnableMsaa8 = true,
    bool EnableMsaa16 = false,
    bool EnableAreaAa = true,
    bool EnableOpacityLayers = true,
    long? MaxGpuResourceBytes = null,
    bool EnableValidationLayers = false);
