using System;

namespace VelloSharp;

internal static class WebGpuRuntimeTestHooks
{
    internal static Func<WebGpuNativeMethods.VelloWebGpuStatus>? InitializeOverride { get; set; }

    internal static Func<WebGpuNativeMethods.VelloWebGpuRequestAdapterOptionsNative, (WebGpuNativeMethods.VelloWebGpuStatus Status, uint FutureId)>?
        RequestAdapterAsyncOverride { get; set; }

    internal static Func<uint, WebGpuNativeMethods.VelloWebGpuRequestDeviceOptionsNative, (WebGpuNativeMethods.VelloWebGpuStatus Status, uint FutureId)>?
        RequestDeviceAsyncOverride { get; set; }

    internal static Func<uint, (WebGpuNativeMethods.VelloWebGpuStatus Status, WebGpuNativeMethods.VelloWebGpuFuturePollResultNative Result)>?
        FuturePollOverride { get; set; }

    internal static bool SuppressLogCallbackRegistration { get; set; }

    internal static Func<string?>? LastErrorMessageOverride { get; set; }

    internal static void ResetAll()
    {
        InitializeOverride = null;
        RequestAdapterAsyncOverride = null;
        RequestDeviceAsyncOverride = null;
        FuturePollOverride = null;
        SuppressLogCallbackRegistration = false;
        LastErrorMessageOverride = null;
    }
}
