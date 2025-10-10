using System;
using System.Numerics;
using Avalonia;
using VelloSharp;
using VelloSharp.Avalonia.Vello.Rendering;

namespace AvaloniaVelloPlayground.Services.Scripting;

public sealed class ScriptRenderContext
{
    internal ScriptRenderContext(
        IVelloApiLease lease,
        Rect bounds,
        TimeSpan totalTime,
        TimeSpan deltaTime,
        Random random)
    {
        Lease = lease ?? throw new ArgumentNullException(nameof(lease));
        Scene = lease.Scene ?? throw new InvalidOperationException("Lease scene is unavailable.");
        Bounds = bounds;
        TotalTime = totalTime;
        DeltaTime = deltaTime;
        Transform = ToMatrix3x2(lease.Transform);
        Random = random;
    }

    public Scene Scene { get; }

    public IVelloApiLease Lease { get; }

    public Rect Bounds { get; }

    public TimeSpan TotalTime { get; }

    public TimeSpan DeltaTime { get; }

    public Matrix3x2 Transform { get; }

    public float Width => (float)Bounds.Width;

    public float Height => (float)Bounds.Height;

    public Random Random { get; }

    public double TimeSeconds => TotalTime.TotalSeconds;

    public double DeltaSeconds => DeltaTime.TotalSeconds;

    public RenderParams RenderParams => Lease.RenderParams;

    public void ScheduleWgpuSurfaceRender(Action<WgpuSurfaceRenderContext> renderAction)
        => Lease.ScheduleWgpuSurfaceRender(renderAction);

    private static Matrix3x2 ToMatrix3x2(Matrix matrix)
    {
        return new Matrix3x2(
            (float)matrix.M11,
            (float)matrix.M12,
            (float)matrix.M21,
            (float)matrix.M22,
            (float)matrix.M31,
            (float)matrix.M32);
    }
}
