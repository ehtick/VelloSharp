using System;
using VelloSharp;

namespace VelloSharp.Integration.Rendering;

public static class VelloRenderPath
{
    public static void Render(Renderer renderer, Scene scene, Span<byte> destination, in RenderParams renderParams, in RenderTargetDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(scene);

        descriptor.EnsureBuffer(destination);
        var negotiated = descriptor.Apply(renderParams);
        renderer.Render(scene, negotiated, destination, descriptor.StrideBytes);
    }

    public static unsafe void Render(Renderer renderer, Scene scene, IntPtr destination, in RenderParams renderParams, in RenderTargetDescriptor descriptor)
    {
        if (destination == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        var span = new Span<byte>((void*)destination, descriptor.RequiredBufferSize);
        Render(renderer, scene, span, renderParams, descriptor);
    }
}
