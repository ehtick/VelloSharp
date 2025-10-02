using System;

namespace VelloSharp;

/// <summary>
/// Provides configuration for sparse CPU rendering, including optional multithreading control.
/// </summary>

public sealed class SparseRenderContextOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether multithreaded rendering should be enabled.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool EnableMultithreading { get; set; } = true;

    /// <summary>
    /// Gets or sets an explicit worker thread count to use when multithreading is enabled.
    /// A value of <see langword="null"/> lets the renderer choose automatically.
    /// </summary>
    public int? ThreadCount { get; set; }

    /// <summary>
    /// Gets or sets the SIMD level the renderer should target. Defaults to automatic detection.
    /// </summary>
    public SparseSimdLevel SimdLevel { get; set; } = SparseSimdLevel.Auto;

    public SparseRenderContextOptions Clone()
    {
        return new SparseRenderContextOptions
        {
            EnableMultithreading = EnableMultithreading,
            ThreadCount = ThreadCount,
            SimdLevel = SimdLevel,
        };
    }

    public static SparseSimdLevel DetectSimdLevel()
    {
        return SparseNativeMethods.vello_sparse_detect_simd_level();
    }

    public static int DetectThreadCount()
    {
        var detected = (int)SparseNativeMethods.vello_sparse_detect_thread_count();
        if (detected <= 0)
        {
            var fallback = Environment.ProcessorCount;
            return Math.Clamp(fallback, 1, ushort.MaxValue);
        }

        return Math.Clamp(detected, 1, ushort.MaxValue);
    }

    public static SparseRenderContextOptions CreateForCurrentMachine(bool enableMultithreading = true)
    {
        var options = new SparseRenderContextOptions
        {
            EnableMultithreading = enableMultithreading,
            SimdLevel = DetectSimdLevel(),
        };

        if (enableMultithreading)
        {
            options.ThreadCount = DetectThreadCount();
        }

        return options;
    }
}
