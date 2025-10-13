using System;

namespace VelloSharp.Windows;

internal sealed record class WindowsGpuRuntimeConfiguration(WgpuBackend Backends, bool ForceWarpFallback, bool AllowWarpFallback);

internal static class WindowsGpuConfiguration
{
    private const string AppContextPrefix = "Vello:Windows:";
    private const string EnvPrefix = "VELLO_WINDOWS_";
    internal const WgpuBackend DefaultBackends = WgpuBackend.Dx12 | WgpuBackend.Vulkan;

    internal static WindowsGpuRuntimeConfiguration Load()
    {
        var backends = ParseBackends(GetString("Backends"));
        if (backends == 0)
        {
            backends = DefaultBackends;
        }

        var forceWarp = GetBoolean("ForceWarp") ?? false;
        var disableWarp = GetBoolean("DisableWarp") ?? false;
        if (forceWarp)
        {
            disableWarp = false;
        }

        return new WindowsGpuRuntimeConfiguration(backends, forceWarp, !disableWarp);
    }

    private static string? GetString(string key)
    {
        if (AppContext.GetData(AppContextPrefix + key) is string data && !string.IsNullOrWhiteSpace(data))
        {
            return data;
        }

        var envKey = EnvPrefix + key.ToUpperInvariant();
        var envValue = Environment.GetEnvironmentVariable(envKey);
        return string.IsNullOrWhiteSpace(envValue) ? null : envValue;
    }

    private static bool? GetBoolean(string key)
    {
        if (AppContext.TryGetSwitch(AppContextPrefix + key, out var appSwitch))
        {
            return appSwitch;
        }

        var envKey = EnvPrefix + key.ToUpperInvariant();
        var envValue = Environment.GetEnvironmentVariable(envKey);
        if (string.IsNullOrWhiteSpace(envValue))
        {
            return null;
        }

        var normalized = envValue.Trim();
        if (normalized.Length == 0)
        {
            return null;
        }

        normalized = normalized.ToLowerInvariant();
        return normalized switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => null,
        };
    }

    private static WgpuBackend ParseBackends(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        var separators = new[] { ',', ';', '|', ' ', '\t' };
        var tokens = raw.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return 0;
        }

        WgpuBackend backends = 0;
        foreach (var tokenRaw in tokens)
        {
            var token = tokenRaw.Trim().ToLowerInvariant();
            if (token.Length == 0)
            {
                continue;
            }

            backends |= token switch
            {
                "dx12" or "d3d12" or "direct3d12" => WgpuBackend.Dx12,
                "vulkan" or "vk" => WgpuBackend.Vulkan,
                "wgpu" or "auto" => DefaultBackends,
                "gl" or "opengl" => WgpuBackend.Gl,
                "metal" => WgpuBackend.Metal,
                "webgpu" or "browser" => WgpuBackend.BrowserWebGpu,
                _ => 0,
            };
        }

        return backends;
    }
}
