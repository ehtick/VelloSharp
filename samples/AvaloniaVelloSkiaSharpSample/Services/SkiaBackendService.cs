using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace AvaloniaVelloSkiaSharpSample.Services;

public enum SkiaBackendKind
{
    Gpu,
    Cpu,
}

public sealed record SkiaBackendDescriptor(SkiaBackendKind Kind, string Title, string Subtitle);

public sealed class SkiaBackendService
{
    public static SkiaBackendDescriptor GpuDescriptor { get; } =
        new(SkiaBackendKind.Gpu, "GPU (WGPU)", "Hardware-accelerated SkiaSharp shim routed through Vello.");

    public static SkiaBackendDescriptor CpuDescriptor { get; } =
        new(SkiaBackendKind.Cpu, "CPU (Scalar)", "Software fallback path for deterministic validation.");

    private readonly List<SkiaBackendDescriptor> _backends =
    [
        GpuDescriptor,
        CpuDescriptor,
    ];

    private SkiaBackendKind _current = SkiaBackendKind.Gpu;

    public SkiaBackendService()
    {
        ApplyBackend(_current, raiseEvent: false);
    }

    public event EventHandler<SkiaBackendDescriptor>? BackendChanged;

    public IReadOnlyList<SkiaBackendDescriptor> Backends => _backends;

    public SkiaBackendDescriptor CurrentDescriptor => _backends.First(descriptor => descriptor.Kind == _current);

    public SkiaBackendKind Current => _current;

    public void SetBackend(SkiaBackendKind kind)
    {
        if (!ApplyBackend(kind, raiseEvent: true))
        {
            return;
        }
    }

    private bool ApplyBackend(SkiaBackendKind kind, bool raiseEvent)
    {
        if (_current == kind && _backendInitialized)
        {
            return false;
        }

        var descriptor = _backends.FirstOrDefault(item => item.Kind == kind);
        if (descriptor is null)
        {
            return false;
        }

        SwitchShim(kind);

        var changed = _current != kind || !_backendInitialized;
        _current = kind;
        _backendInitialized = true;
        if (changed && raiseEvent)
        {
            BackendChanged?.Invoke(this, descriptor);
        }

        return changed;
    }

    private static void SwitchShim(SkiaBackendKind kind)
    {
        switch (kind)
        {
            case SkiaBackendKind.Gpu:
                GpuSkiaBackend.Use();
                break;
            case SkiaBackendKind.Cpu:
                CpuSkiaBackend.Use();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported Skia backend.");
        }
    }

    private bool _backendInitialized;
}
