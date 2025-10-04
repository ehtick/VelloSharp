using System.Runtime.InteropServices;

namespace ControlCatalog.ViewModels;

public class PlatformInformationViewModel
{
    public PlatformInformationViewModel()
    {
        if (OperatingSystem.IsBrowser())
        {
            PlatformInfo = "Platform: Browser";
        }
        else if (OperatingSystem.IsIOS() || OperatingSystem.IsAndroid())
        {
            PlatformInfo = "Platform: Mobile (native)";
        }
        else if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            PlatformInfo = "Platform: Desktop (native)";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER")))
        {
            PlatformInfo = "Platform: Unknown (browser) - please report";
        }
        else
        {
            PlatformInfo = "Platform: Unknown (native) - please report";
        }
    }
    
    public string? PlatformInfo { get; }
}
