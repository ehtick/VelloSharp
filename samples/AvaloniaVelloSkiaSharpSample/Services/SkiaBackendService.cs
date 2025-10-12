using System;
using System.Collections.Generic;
using System.Linq;

namespace AvaloniaVelloSkiaSharpSample.Services;

public enum SkiaBackendKind
{
    Gpu,
    Cpu,
}

public sealed record SkiaBackendDescriptor(SkiaBackendKind Kind, string Title, string Subtitle);

public sealed class SkiaBackendService
{
    private readonly List<SkiaBackendDescriptor> _backends =
    [
        new(SkiaBackendKind.Gpu, "GPU (WGPU)", "Hardware-accelerated SkiaSharp shim routed through Vello."),
        new(SkiaBackendKind.Cpu, "CPU (Scalar)", "Software fallback path for deterministic validation."),
    ];

    private SkiaBackendKind _current = SkiaBackendKind.Gpu;

    public event EventHandler<SkiaBackendDescriptor>? BackendChanged;

    public IReadOnlyList<SkiaBackendDescriptor> Backends => _backends;

    public SkiaBackendDescriptor CurrentDescriptor => _backends.First(descriptor => descriptor.Kind == _current);

    public SkiaBackendKind Current => _current;

    public void SetBackend(SkiaBackendKind kind)
    {
        if (_current == kind)
        {
            return;
        }

        var descriptor = _backends.FirstOrDefault(item => item.Kind == kind);
        if (descriptor is null)
        {
            return;
        }

        _current = kind;
        BackendChanged?.Invoke(this, descriptor);
    }
}
