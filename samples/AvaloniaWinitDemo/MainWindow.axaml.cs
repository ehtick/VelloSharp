using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering;

namespace AvaloniaWinitDemo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // TODO: Fix RendererDiagnostics.DebugOverlays in vello platform.
        // RendererDiagnostics.DebugOverlays = RendererDebugOverlays.Fps | RendererDebugOverlays.LayoutTimeGraph | RendererDebugOverlays.RenderTimeGraph;
        RendererDiagnostics.DebugOverlays = RendererDebugOverlays.Fps;
#if DEBUG
        this.AttachDevTools();
#endif
    }
}
