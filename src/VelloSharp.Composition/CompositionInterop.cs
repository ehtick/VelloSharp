using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace VelloSharp.Composition;

public static class CompositionInterop
{
    private const int StackAllocThreshold = 256;
    private const int LinearLayoutStackThreshold = 8;

    public static PlotArea ComputePlotArea(double width, double height)
    {
        if (!NativeMethods.vello_composition_compute_plot_area(width, height, out var area))
        {
            throw new InvalidOperationException("vello_composition_compute_plot_area failed.");
        }

        return new PlotArea(area.Left, area.Top, area.Width, area.Height);
    }

    public static LabelMetrics MeasureLabel(string text, float fontSize = 14f)
    {
        ArgumentNullException.ThrowIfNull(text);
        return MeasureLabel(text.AsSpan(), fontSize);
    }

    public static LabelMetrics MeasureLabel(ReadOnlySpan<char> text, float fontSize = 14f)
    {
        if (text.IsEmpty)
        {
            return default;
        }

        int requiredLength = Encoding.UTF8.GetByteCount(text);
        if (requiredLength == 0)
        {
            return default;
        }

        if (requiredLength <= StackAllocThreshold)
        {
            Span<byte> buffer = stackalloc byte[StackAllocThreshold];
            Span<byte> utf8 = buffer[..requiredLength];
            Encoding.UTF8.GetBytes(text, utf8);
            return MeasureLabelCore(utf8, fontSize);
        }

        byte[] rented = ArrayPool<byte>.Shared.Rent(requiredLength);
        try
        {
            Span<byte> utf8 = rented.AsSpan(0, requiredLength);
            Encoding.UTF8.GetBytes(text, utf8);
            return MeasureLabelCore(utf8, fontSize);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static LabelMetrics MeasureLabelCore(Span<byte> utf8, float fontSize)
    {
        if (utf8.IsEmpty)
        {
            return default;
        }

        unsafe
        {
            fixed (byte* ptr = utf8)
            {
                if (!NativeMethods.vello_composition_measure_label(
                        ptr,
                        (nuint)utf8.Length,
                        fontSize,
                        out var metrics))
                {
                    return default;
                }

                return new LabelMetrics(metrics.Width, metrics.Height, metrics.Ascent);
            }
        }
    }

    public static int SolveLinearLayout(
        ReadOnlySpan<LinearLayoutChild> children,
        double available,
        double spacing,
        Span<LinearLayoutResult> results)
    {
        if (children.Length == 0)
        {
            return 0;
        }

        if (results.Length < children.Length)
        {
            throw new ArgumentException("Result span is too small.", nameof(results));
        }

        int count = children.Length;
        Span<VelloCompositionLinearLayoutItem> nativeItems = count <= LinearLayoutStackThreshold
            ? stackalloc VelloCompositionLinearLayoutItem[LinearLayoutStackThreshold]
            : new VelloCompositionLinearLayoutItem[count];
        nativeItems = nativeItems[..count];

        Span<VelloCompositionLinearLayoutSlot> nativeSlots = count <= LinearLayoutStackThreshold
            ? stackalloc VelloCompositionLinearLayoutSlot[LinearLayoutStackThreshold]
            : new VelloCompositionLinearLayoutSlot[count];
        nativeSlots = nativeSlots[..count];

        for (int i = 0; i < count; i++)
        {
            var child = children[i];
            nativeItems[i] = new VelloCompositionLinearLayoutItem
            {
                Min = child.Min,
                Preferred = child.Preferred,
                Max = child.Max,
                Weight = child.Weight,
                MarginLeading = child.MarginLeading,
                MarginTrailing = child.MarginTrailing,
            };
        }

        nuint solved;
        unsafe
        {
            fixed (VelloCompositionLinearLayoutItem* itemsPtr = nativeItems)
            fixed (VelloCompositionLinearLayoutSlot* slotsPtr = nativeSlots)
            {
                solved = NativeMethods.vello_composition_solve_linear_layout(
                    itemsPtr,
                    (nuint)count,
                    available,
                    spacing,
                    slotsPtr,
                    (nuint)count);
            }
        }

        if (solved == 0 || solved != (nuint)count)
        {
            return 0;
        }

        for (int i = 0; i < count; i++)
        {
            var slot = nativeSlots[i];
            results[i] = new LinearLayoutResult(slot.Offset, slot.Length);
        }

        return count;
    }

    public readonly record struct LinearLayoutChild(
        double Min,
        double Preferred,
        double Max,
        double Weight = 1.0,
        double MarginLeading = 0.0,
        double MarginTrailing = 0.0);

    public readonly record struct LinearLayoutResult(double Offset, double Length);
}

public readonly record struct CompositionColor(float R, float G, float B, float A = 1f)
{
    public CompositionColor Clamp() =>
        new(Math.Clamp(R, 0f, 1f), Math.Clamp(G, 0f, 1f), Math.Clamp(B, 0f, 1f), Math.Clamp(A, 0f, 1f));

    internal VelloCompositionColor ToNative()
    {
        var clamped = Clamp();
        return new VelloCompositionColor
        {
            R = clamped.R,
            G = clamped.G,
            B = clamped.B,
            A = clamped.A,
        };
    }

    internal static CompositionColor FromNative(in VelloCompositionColor native) =>
        new(native.R, native.G, native.B, native.A);
}

public enum CompositionShaderKind : uint
{
    Solid = 0,
}

public readonly record struct CompositionShaderDescriptor(
    CompositionShaderKind Kind,
    CompositionColor Solid);

public static class CompositionShaderRegistry
{
    public static void Register(uint shaderId, CompositionShaderDescriptor descriptor)
    {
        if (shaderId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shaderId), "Shader identifier must be non-zero.");
        }

        var native = new VelloCompositionShaderDescriptor
        {
            Kind = (VelloCompositionShaderKind)descriptor.Kind,
            Solid = descriptor.Solid.ToNative(),
        };

        if (!NativeMethods.vello_composition_shader_register(shaderId, in native))
        {
            throw new InvalidOperationException("Failed to register composition shader.");
        }
    }

    public static void Unregister(uint shaderId)
    {
        if (shaderId == 0)
        {
            return;
        }

        NativeMethods.vello_composition_shader_unregister(shaderId);
    }
}

public readonly record struct CompositionMaterialDescriptor(uint ShaderId, float Opacity = 1f)
{
    internal VelloCompositionMaterialDescriptor ToNative()
    {
        return new VelloCompositionMaterialDescriptor
        {
            Shader = ShaderId,
            Opacity = Math.Clamp(Opacity, 0f, 1f),
        };
    }
}

public static class CompositionMaterialRegistry
{
    public static void Register(uint materialId, CompositionMaterialDescriptor descriptor)
    {
        if (materialId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(materialId), "Material identifier must be non-zero.");
        }

        if (descriptor.ShaderId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), "Descriptor must reference a registered shader.");
        }

        var native = descriptor.ToNative();
        if (!NativeMethods.vello_composition_material_register(materialId, in native))
        {
            throw new InvalidOperationException("Failed to register composition material.");
        }
    }

    public static void Unregister(uint materialId)
    {
        if (materialId == 0)
        {
            return;
        }

        NativeMethods.vello_composition_material_unregister(materialId);
    }

    public static bool TryResolveColor(uint materialId, out CompositionColor color)
    {
        if (NativeMethods.vello_composition_material_resolve_color(materialId, out var native))
        {
            color = CompositionColor.FromNative(native);
            return true;
        }

        color = default;
        return false;
    }
}

public readonly record struct DirtyRegion(double MinX, double MaxX, double MinY, double MaxY)
{
    public bool IsEmpty => MinX > MaxX || MinY > MaxY;
}

public sealed class SceneCache : SafeHandle
{
    public SceneCache()
        : base(IntPtr.Zero, ownsHandle: true)
    {
        var nativeHandle = NativeMethods.vello_composition_scene_cache_create();
        SetHandle(nativeHandle);
        if (IsInvalid)
        {
            throw new InvalidOperationException("Failed to create scene cache.");
        }
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        if (!IsInvalid)
        {
            NativeMethods.vello_composition_scene_cache_destroy(handle);
            SetHandle(IntPtr.Zero);
        }

        return true;
    }

    public uint CreateNode(uint? parentId = null)
    {
        ThrowIfInvalid();
        uint parent = parentId ?? uint.MaxValue;
        uint node = NativeMethods.vello_composition_scene_cache_create_node(handle, parent);
        if (node == uint.MaxValue)
        {
            throw new InvalidOperationException("Failed to allocate scene cache node.");
        }

        return node;
    }

    public void DisposeNode(uint nodeId)
    {
        ThrowIfInvalid();
        NativeMethods.vello_composition_scene_cache_dispose_node(handle, nodeId);
    }

    public void MarkDirty(uint nodeId, double x, double y)
    {
        ThrowIfInvalid();
        if (!NativeMethods.vello_composition_scene_cache_mark_dirty(handle, nodeId, x, y))
        {
            throw new InvalidOperationException("Failed to mark scene cache node dirty.");
        }
    }

    public void MarkDirtyBounds(uint nodeId, double minX, double maxX, double minY, double maxY)
    {
        ThrowIfInvalid();
        if (!NativeMethods.vello_composition_scene_cache_mark_dirty_bounds(
                handle,
                nodeId,
                minX,
                maxX,
                minY,
                maxY))
        {
            throw new InvalidOperationException("Failed to mark scene cache node dirty bounds.");
        }
    }

    public bool TakeDirty(uint nodeId, out DirtyRegion region)
    {
        ThrowIfInvalid();
        if (NativeMethods.vello_composition_scene_cache_take_dirty(handle, nodeId, out var native))
        {
            region = new DirtyRegion(native.MinX, native.MaxX, native.MinY, native.MaxY);
            return true;
        }

        region = default;
        return false;
    }

    public void Clear(uint nodeId)
    {
        ThrowIfInvalid();
        NativeMethods.vello_composition_scene_cache_clear(handle, nodeId);
    }

    private void ThrowIfInvalid()
    {
        if (IsInvalid)
        {
            throw new ObjectDisposedException(nameof(SceneCache));
        }
    }
}

[System.Diagnostics.DebuggerDisplay("{Name} (NodeId = {NodeId})")]
public readonly struct RenderLayer
{
    public RenderLayer(string name, uint nodeId)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        NodeId = nodeId;
    }

    public string Name { get; }
    public uint NodeId { get; }
}

public sealed class ScenePartitioner
{
    private readonly SceneCache _cache;
    private readonly Dictionary<string, uint> _layers;
    private readonly object _sync = new();

    public ScenePartitioner(SceneCache cache, uint rootNodeId, IEqualityComparer<string>? comparer = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        if (cache.IsInvalid)
        {
            throw new ObjectDisposedException(nameof(SceneCache));
        }

        RootNodeId = rootNodeId;
        _layers = new Dictionary<string, uint>(comparer ?? StringComparer.Ordinal);
    }

    public SceneCache Cache => _cache;

    public uint RootNodeId { get; }

    public RenderLayer RootLayer => new RenderLayer("root", RootNodeId);

    public RenderLayer GetOrCreateLayer(string name, uint? parentLayerId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Layer name must be provided.", nameof(name));
        }

        lock (_sync)
        {
            if (_layers.TryGetValue(name, out var existing))
            {
                return new RenderLayer(name, existing);
            }

            uint parent = parentLayerId ?? RootNodeId;
            var nodeId = _cache.CreateNode(parent);
            _layers[name] = nodeId;
            return new RenderLayer(name, nodeId);
        }
    }

    public bool TryGetLayer(string name, out RenderLayer layer)
    {
        if (string.IsNullOrEmpty(name))
        {
            layer = default;
            return false;
        }

        lock (_sync)
        {
            if (_layers.TryGetValue(name, out var nodeId))
            {
                layer = new RenderLayer(name, nodeId);
                return true;
            }
        }

        layer = default;
        return false;
    }

    public bool RemoveLayer(string name, bool disposeNode = true)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        uint nodeId;
        lock (_sync)
        {
            if (!_layers.TryGetValue(name, out nodeId))
            {
                return false;
            }

            _layers.Remove(name);
        }

        if (disposeNode)
        {
            _cache.DisposeNode(nodeId);
        }

        return true;
    }

    public IEnumerable<RenderLayer> EnumerateLayers()
    {
        lock (_sync)
        {
            foreach (var pair in _layers)
            {
                yield return new RenderLayer(pair.Key, pair.Value);
            }
        }
    }
}

[Flags]
public enum TimelineSampleFlags : ushort
{
    None = 0,
    Active = 1 << 0,
    Completed = 1 << 1,
    Looped = 1 << 2,
    PingPongReversed = 1 << 3,
    AtRest = 1 << 4,
}

public enum TimelineRepeat
{
    Once = 0,
    Loop = 1,
    PingPong = 2,
}

public enum TimelineEasing
{
    Linear = 0,
    EaseInQuad = 1,
    EaseOutQuad = 2,
    EaseInOutQuad = 3,
    EaseInCubic = 4,
    EaseOutCubic = 5,
    EaseInOutCubic = 6,
    EaseInQuart = 7,
    EaseOutQuart = 8,
    EaseInOutQuart = 9,
    EaseInQuint = 10,
    EaseOutQuint = 11,
    EaseInOutQuint = 12,
    EaseInSine = 13,
    EaseOutSine = 14,
    EaseInOutSine = 15,
    EaseInExpo = 16,
    EaseOutExpo = 17,
    EaseInOutExpo = 18,
    EaseInCirc = 19,
    EaseOutCirc = 20,
    EaseInOutCirc = 21,
}

public enum TimelineDirtyKind
{
    None = 0,
    Point = 1,
    Bounds = 2,
}

public readonly struct TimelineDirtyBinding
{
    public TimelineDirtyBinding(
        TimelineDirtyKind kind,
        double x,
        double y,
        double minX,
        double maxX,
        double minY,
        double maxY)
    {
        Kind = kind;
        X = x;
        Y = y;
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
    }

    public TimelineDirtyKind Kind { get; }
    public double X { get; }
    public double Y { get; }
    public double MinX { get; }
    public double MaxX { get; }
    public double MinY { get; }
    public double MaxY { get; }

    public static TimelineDirtyBinding None => default;

    public static TimelineDirtyBinding Point(double x, double y) =>
        new(TimelineDirtyKind.Point, x, y, 0.0, 0.0, 0.0, 0.0);

    public static TimelineDirtyBinding Bounds(double minX, double maxX, double minY, double maxY) =>
        new(TimelineDirtyKind.Bounds, 0.0, 0.0, minX, maxX, minY, maxY);

    internal VelloCompositionTimelineDirtyBinding ToNative()
    {
        var binding = new VelloCompositionTimelineDirtyBinding
        {
            Kind = (VelloCompositionTimelineDirtyKind)Kind,
            Reserved = 0,
            X = X,
            Y = Y,
            MinX = MinX,
            MaxX = MaxX,
            MinY = MinY,
            MaxY = MaxY,
        };

        if (Kind == TimelineDirtyKind.Bounds)
        {
            var minX = Math.Min(MinX, MaxX);
            var maxX = Math.Max(MinX, MaxX);
            var minY = Math.Min(MinY, MaxY);
            var maxY = Math.Max(MinY, MaxY);
            binding.MinX = minX;
            binding.MaxX = maxX;
            binding.MinY = minY;
            binding.MaxY = maxY;
        }

        return binding;
    }
}

public readonly struct TimelineGroupConfig
{
    public TimelineGroupConfig(float speed = 1.0f, bool autoplay = true)
    {
        Speed = speed;
        Autoplay = autoplay;
    }

    public float Speed { get; }
    public bool Autoplay { get; }

    internal VelloCompositionTimelineGroupConfig ToNative() => new()
    {
        Speed = Speed,
        Autoplay = Autoplay ? 1 : 0,
    };
}

public readonly struct TimelineEasingTrackDescriptor
{
    public TimelineEasingTrackDescriptor(
        uint nodeId,
        ushort channelId,
        float startValue,
        float endValue,
        float duration,
        TimelineEasing easing,
        TimelineRepeat repeat,
        TimelineDirtyBinding dirtyBinding)
    {
        NodeId = nodeId;
        ChannelId = channelId;
        StartValue = startValue;
        EndValue = endValue;
        Duration = duration;
        Easing = easing;
        Repeat = repeat;
        DirtyBinding = dirtyBinding;
    }

    public uint NodeId { get; }
    public ushort ChannelId { get; }
    public float StartValue { get; }
    public float EndValue { get; }
    public float Duration { get; }
    public TimelineEasing Easing { get; }
    public TimelineRepeat Repeat { get; }
    public TimelineDirtyBinding DirtyBinding { get; }

    internal VelloCompositionTimelineEasingTrackDesc ToNative() => new()
    {
        NodeId = NodeId,
        ChannelId = ChannelId,
        Reserved = 0,
        Repeat = (VelloCompositionTimelineRepeat)Repeat,
        Easing = (VelloCompositionTimelineEasing)Easing,
        StartValue = StartValue,
        EndValue = EndValue,
        Duration = Duration,
        DirtyBinding = DirtyBinding.ToNative(),
    };
}

public readonly struct TimelineSpringTrackDescriptor
{
    public TimelineSpringTrackDescriptor(
        uint nodeId,
        ushort channelId,
        float stiffness,
        float damping,
        float mass,
        float startValue,
        float initialVelocity,
        float targetValue,
        float restVelocity,
        float restOffset,
        TimelineDirtyBinding dirtyBinding)
    {
        NodeId = nodeId;
        ChannelId = channelId;
        Stiffness = stiffness;
        Damping = damping;
        Mass = mass;
        StartValue = startValue;
        InitialVelocity = initialVelocity;
        TargetValue = targetValue;
        RestVelocity = restVelocity;
        RestOffset = restOffset;
        DirtyBinding = dirtyBinding;
    }

    public uint NodeId { get; }
    public ushort ChannelId { get; }
    public float Stiffness { get; }
    public float Damping { get; }
    public float Mass { get; }
    public float StartValue { get; }
    public float InitialVelocity { get; }
    public float TargetValue { get; }
    public float RestVelocity { get; }
    public float RestOffset { get; }
    public TimelineDirtyBinding DirtyBinding { get; }

    internal VelloCompositionTimelineSpringTrackDesc ToNative() => new()
    {
        NodeId = NodeId,
        ChannelId = ChannelId,
        Reserved = 0,
        Stiffness = Stiffness,
        Damping = Damping,
        Mass = Mass,
        StartValue = StartValue,
        InitialVelocity = InitialVelocity,
        TargetValue = TargetValue,
        RestVelocity = RestVelocity,
        RestOffset = RestOffset,
        DirtyBinding = DirtyBinding.ToNative(),
    };
}

[StructLayout(LayoutKind.Sequential)]
public struct TimelineSample
{
    public uint TrackId;
    public uint NodeId;
    public ushort ChannelId;
    public TimelineSampleFlags Flags;
    public float Value;
    public float Velocity;
    public float Progress;

    public readonly bool IsCompleted => (Flags & TimelineSampleFlags.Completed) != 0;
    public readonly bool IsActive => (Flags & TimelineSampleFlags.Active) != 0;
}

public sealed class TimelineSystem : SafeHandle
{
    public TimelineSystem()
        : base(IntPtr.Zero, ownsHandle: true)
    {
        var nativeHandle = NativeMethods.vello_composition_timeline_system_create();
        SetHandle(nativeHandle);
        if (IsInvalid)
        {
            throw new InvalidOperationException("Failed to create timeline system.");
        }
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        if (!IsInvalid)
        {
            NativeMethods.vello_composition_timeline_system_destroy(handle);
            SetHandle(IntPtr.Zero);
        }

        return true;
    }

    public uint CreateGroup(TimelineGroupConfig config)
    {
        ThrowIfInvalid();
        var nativeConfig = config.ToNative();
        uint groupId = NativeMethods.vello_composition_timeline_group_create(handle, nativeConfig);
        if (groupId == uint.MaxValue)
        {
            throw new InvalidOperationException("Failed to create timeline group.");
        }

        return groupId;
    }

    public void DestroyGroup(uint groupId)
    {
        ThrowIfInvalid();
        NativeMethods.vello_composition_timeline_group_destroy(handle, groupId);
    }

    public void PlayGroup(uint groupId)
    {
        ThrowIfInvalid();
        NativeMethods.vello_composition_timeline_group_play(handle, groupId);
    }

    public void PauseGroup(uint groupId)
    {
        ThrowIfInvalid();
        NativeMethods.vello_composition_timeline_group_pause(handle, groupId);
    }

    public void SetGroupSpeed(uint groupId, float speed)
    {
        ThrowIfInvalid();
        NativeMethods.vello_composition_timeline_group_set_speed(handle, groupId, speed);
    }

    public uint AddEasingTrack(uint groupId, TimelineEasingTrackDescriptor descriptor)
    {
        ThrowIfInvalid();
        if (descriptor.NodeId == uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), "Descriptor must reference a valid scene node.");
        }

        if (descriptor.Duration <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), "Duration must be positive.");
        }

        var nativeDescriptor = descriptor.ToNative();
        uint trackId;
        unsafe
        {
            trackId = NativeMethods.vello_composition_timeline_add_easing_track(
                handle,
                groupId,
                (VelloCompositionTimelineEasingTrackDesc*)Unsafe.AsPointer(ref nativeDescriptor));
        }

        if (trackId == uint.MaxValue)
        {
            throw new InvalidOperationException("Failed to add easing track.");
        }

        return trackId;
    }

    public uint AddSpringTrack(uint groupId, TimelineSpringTrackDescriptor descriptor)
    {
        ThrowIfInvalid();
        if (descriptor.NodeId == uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), "Descriptor must reference a valid scene node.");
        }

        if (descriptor.Stiffness <= 0f || descriptor.Mass <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), "Spring stiffness and mass must be positive.");
        }

        var nativeDescriptor = descriptor.ToNative();
        uint trackId;
        unsafe
        {
            trackId = NativeMethods.vello_composition_timeline_add_spring_track(
                handle,
                groupId,
                (VelloCompositionTimelineSpringTrackDesc*)Unsafe.AsPointer(ref nativeDescriptor));
        }

        if (trackId == uint.MaxValue)
        {
            throw new InvalidOperationException("Failed to add spring track.");
        }

        return trackId;
    }

    public void RemoveTrack(uint trackId)
    {
        ThrowIfInvalid();
        NativeMethods.vello_composition_timeline_track_remove(handle, trackId);
    }

    public void ResetTrack(uint trackId)
    {
        ThrowIfInvalid();
        NativeMethods.vello_composition_timeline_track_reset(handle, trackId);
    }

    public void SetSpringTarget(uint trackId, float targetValue)
    {
        ThrowIfInvalid();
        NativeMethods.vello_composition_timeline_track_set_spring_target(handle, trackId, targetValue);
    }

    public int Tick(TimeSpan delta, SceneCache? cache, Span<TimelineSample> samples)
    {
        ThrowIfInvalid();

        if (cache is { IsInvalid: true })
        {
            throw new ObjectDisposedException(nameof(SceneCache));
        }

        double seconds = delta.TotalSeconds;
        nint cacheHandle = cache?.DangerousGetHandle() ?? IntPtr.Zero;

        nuint result;
        unsafe
        {
            if (samples.IsEmpty)
            {
                result = NativeMethods.vello_composition_timeline_tick(
                    handle,
                    seconds,
                    cacheHandle,
                    null,
                    0);
            }
            else
            {
                fixed (TimelineSample* sampleBase = samples)
                {
                    result = NativeMethods.vello_composition_timeline_tick(
                        handle,
                        seconds,
                        cacheHandle,
                        (VelloCompositionTimelineSample*)sampleBase,
                        (nuint)samples.Length);
                }
            }
        }

        GC.KeepAlive(cache);

        if (result > int.MaxValue)
        {
            throw new InvalidOperationException("Timeline produced more samples than supported.");
        }

        return (int)result;
    }

    private void ThrowIfInvalid()
    {
        if (IsInvalid)
        {
            throw new ObjectDisposedException(nameof(TimelineSystem));
        }
    }
}
