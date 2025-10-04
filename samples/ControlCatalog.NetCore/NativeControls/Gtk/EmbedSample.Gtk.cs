using System;
using Avalonia.Platform;
using ControlCatalog.Pages;

namespace ControlCatalog.NetCore;

public class EmbedSampleGtk : INativeDemoControl
{
    public IPlatformHandle CreateControl(bool isSecond, IPlatformHandle parent, Func<IPlatformHandle> createDefault)
    {
        // Fallback to the default implementation when native interop is unavailable.
        return createDefault();
    }
}
