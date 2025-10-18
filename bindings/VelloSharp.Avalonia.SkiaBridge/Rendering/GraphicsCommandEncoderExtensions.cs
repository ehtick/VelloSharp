using VelloSharp.Avalonia.Core.Rendering;

namespace VelloSharp.Avalonia.SkiaBridge.Rendering;

/// <summary>
/// Extension helpers for Skia-backed command encoders.
/// </summary>
public static class GraphicsCommandEncoderExtensions
{
    /// <summary>
    /// Attempts to retrieve the Skia encoder context.
    /// </summary>
    public static bool TryGetSkiaContext(this IGraphicsCommandEncoder encoder, out SkiaCommandEncoderContext context)
    {
        if (encoder is null)
        {
            throw new ArgumentNullException(nameof(encoder));
        }

        return encoder.TryGetContext(out context);
    }

    /// <summary>
    /// Attempts to encode Skia work via the provided callback.
    /// </summary>
    public static bool TryEncodeSkia(this IGraphicsCommandEncoder encoder, Action<SkiaCommandEncoderContext> callback)
    {
        if (encoder is null)
        {
            throw new ArgumentNullException(nameof(encoder));
        }

        if (callback is null)
        {
            throw new ArgumentNullException(nameof(callback));
        }

        if (encoder.TryGetContext(out SkiaCommandEncoderContext context))
        {
            callback(context);
            return true;
        }

        return false;
    }
}
