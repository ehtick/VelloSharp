using System;
using VelloSharp;

namespace VelloSharp.Avalonia.Vello;

internal static class WebGpuCapabilityHelpers
{
    public static bool TryMapTextureFormat(
        WebGpuRuntime.WebGpuTextureFormat textureFormat,
        out WgpuTextureFormat mappedFormat)
    {
        mappedFormat = textureFormat switch
        {
            WebGpuRuntime.WebGpuTextureFormat.Rgba8Unorm => WgpuTextureFormat.Rgba8Unorm,
            WebGpuRuntime.WebGpuTextureFormat.Rgba8UnormSrgb => WgpuTextureFormat.Rgba8UnormSrgb,
            WebGpuRuntime.WebGpuTextureFormat.Bgra8Unorm => WgpuTextureFormat.Bgra8Unorm,
            WebGpuRuntime.WebGpuTextureFormat.Bgra8UnormSrgb => WgpuTextureFormat.Bgra8UnormSrgb,
            _ => default,
        };

        return textureFormat is not WebGpuRuntime.WebGpuTextureFormat.Undefined
               && Enum.IsDefined(typeof(WgpuTextureFormat), mappedFormat);
    }

    public static bool SupportsSampleCount(
        WebGpuRuntime.WebGpuCapabilities? capabilities,
        uint sampleCount)
    {
        if (capabilities is null)
        {
            return false;
        }

        if (sampleCount <= 1)
        {
            return true;
        }

        var bytesPerPixel = GetBytesPerPixel(capabilities.SurfaceTextureFormat);
        if (bytesPerPixel == 0)
        {
            return false;
        }

        var requiredBytes = bytesPerPixel * sampleCount;
        return capabilities.DeviceLimits.MaxColorAttachmentBytesPerSample >= requiredBytes;
    }

    public static string BuildSummary(WebGpuRuntime.WebGpuCapabilities capabilities)
    {
        var msaa8 = SupportsSampleCount(capabilities, 8) ? "supported" : "not supported";
        var msaa16 = SupportsSampleCount(capabilities, 16) ? "supported" : "not supported";

        return $"Format={capabilities.SurfaceTextureFormat}, MaxTexture2D={capabilities.DeviceLimits.MaxTextureDimension2D}, " +
               $"MaxColorAttachments={capabilities.DeviceLimits.MaxColorAttachments}, " +
               $"MSAA8={msaa8}, MSAA16={msaa16}";
    }

    private static uint GetBytesPerPixel(WebGpuRuntime.WebGpuTextureFormat format) => format switch
    {
        WebGpuRuntime.WebGpuTextureFormat.Rgba8Unorm => 4,
        WebGpuRuntime.WebGpuTextureFormat.Rgba8UnormSrgb => 4,
        WebGpuRuntime.WebGpuTextureFormat.Bgra8Unorm => 4,
        WebGpuRuntime.WebGpuTextureFormat.Bgra8UnormSrgb => 4,
        _ => 0,
    };
}
