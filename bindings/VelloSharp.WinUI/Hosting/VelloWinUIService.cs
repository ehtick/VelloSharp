using System;
using VelloSharp.Windows.Controls;

namespace VelloSharp.Windows.Hosting;

public sealed class VelloWinUIService
{
    private readonly VelloWinUIOptions _options;

    public VelloWinUIService(VelloWinUIOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void Configure(VelloSwapChainControl control)
    {
        ArgumentNullException.ThrowIfNull(control);
        _options.ConfigureSwapChain?.Invoke(control);
    }
}
