using SkiaSharp;

namespace VelloSharp.Avalonia.SkiaBridge.Rendering;

/// <summary>
/// Backend-specific context for encoding Skia commands.
/// </summary>
public readonly struct SkiaCommandEncoderContext
{
    /// <summary>
    /// Initializes a new <see cref="SkiaCommandEncoderContext"/>.
    /// </summary>
    /// <param name="canvas">Skia canvas for issuing draw commands.</param>
    /// <param name="surface">Underlying Skia surface for the current frame.</param>
    /// <param name="grContext">Optional Skia GPU context.</param>
    public SkiaCommandEncoderContext(
        SKCanvas canvas,
        SKSurface surface,
        GRContext? grContext)
    {
        Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        Surface = surface ?? throw new ArgumentNullException(nameof(surface));
        GrContext = grContext;
    }

    /// <summary>
    /// Gets the Skia canvas that should receive draw commands.
    /// </summary>
    public SKCanvas Canvas { get; }

    /// <summary>
    /// Gets the Skia surface backing the canvas.
    /// </summary>
    public SKSurface Surface { get; }

    /// <summary>
    /// Gets the optional Skia GPU context.
    /// </summary>
    public GRContext? GrContext { get; }
}
