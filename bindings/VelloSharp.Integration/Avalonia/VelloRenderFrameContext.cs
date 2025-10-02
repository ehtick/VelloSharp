using System;
using VelloSharp;

namespace VelloSharp.Integration.Avalonia;

public readonly record struct VelloRenderFrameContext(
    Scene Scene,
    uint Width,
    uint Height,
    double RenderScaling,
    TimeSpan DeltaTime,
    TimeSpan TotalTime);
