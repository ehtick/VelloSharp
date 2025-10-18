namespace VelloSharp.Avalonia.Core.Rendering;

/// <summary>
/// Extension helpers for interacting with <see cref="IGraphicsCommandEncoder"/>.
/// </summary>
public static class GraphicsCommandEncoderExtensions
{
    /// <summary>
    /// Attempts to retrieve the WebGPU encoder context.
    /// </summary>
    public static bool TryGetWgpuContext(this IGraphicsCommandEncoder encoder, out WgpuCommandEncoderContext context)
    {
        if (encoder is null)
        {
            throw new ArgumentNullException(nameof(encoder));
        }

        return encoder.TryGetContext(out context);
    }

    /// <summary>
    /// Attempts to encode WebGPU work via the provided callback.
    /// </summary>
    public static bool TryEncodeWgpu(this IGraphicsCommandEncoder encoder, Action<WgpuCommandEncoderContext> callback)
    {
        if (encoder is null)
        {
            throw new ArgumentNullException(nameof(encoder));
        }

        if (callback is null)
        {
            throw new ArgumentNullException(nameof(callback));
        }

        if (encoder.TryGetContext(out WgpuCommandEncoderContext context))
        {
            callback(context);
            return true;
        }

        return false;
    }
}
