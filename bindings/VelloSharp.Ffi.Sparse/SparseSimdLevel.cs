namespace VelloSharp;

/// <summary>
/// Specifies which SIMD level the sparse CPU renderer should target.
/// </summary>
public enum SparseSimdLevel
{
    /// <summary>
    /// Let the renderer choose the optimal SIMD level for the current machine.
    /// </summary>
    Auto = -1,

    /// <summary>
    /// Force scalar execution without SIMD acceleration.
    /// </summary>
    Fallback = 0,

    /// <summary>
    /// Target Neon on 64-bit ARM platforms.
    /// </summary>
    Neon = 1,

    /// <summary>
    /// Target the wasm32 SIMD128 instruction set.
    /// </summary>
    WasmSimd128 = 2,

    /// <summary>
    /// Target SSE4.2 on x86/x64 platforms.
    /// </summary>
    Sse4_2 = 3,

    /// <summary>
    /// Target AVX2+FMA on x86/x64 platforms.
    /// </summary>
    Avx2 = 4,
}
