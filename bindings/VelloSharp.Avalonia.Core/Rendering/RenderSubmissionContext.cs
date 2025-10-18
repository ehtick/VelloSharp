using Avalonia;
using VelloSharp.Avalonia.Core.Device;
using VelloSharp.Avalonia.Core.Surface;

namespace VelloSharp.Avalonia.Core.Rendering;

/// <summary>
/// Carries the information required to submit a recorded scene to a render surface.
/// </summary>
/// <param name="Device">The device lease to use for rendering.</param>
/// <param name="Surface">The surface lease representing the target framebuffer or swapchain image.</param>
/// <param name="RenderParams">Renderer parameters describing dimensions and format preferences.</param>
/// <param name="Transform">Root transform applied to the scene when it was recorded.</param>
public readonly record struct RenderSubmissionContext(
    GraphicsDeviceLease Device,
    RenderSurfaceLease Surface,
    RenderParams RenderParams,
    Matrix Transform);
