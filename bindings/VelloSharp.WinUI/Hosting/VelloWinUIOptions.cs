using System;
using VelloSharp.Windows.Controls;

namespace VelloSharp.Windows.Hosting;

public sealed class VelloWinUIOptions
{
    /// <summary>
    /// Optional callback used to apply default configuration to every created <see cref="VelloSwapChainControl"/>.
    /// </summary>
    public Action<VelloSwapChainControl>? ConfigureSwapChain { get; set; }
}
