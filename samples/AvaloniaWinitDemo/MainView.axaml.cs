using Avalonia.Controls;
using VelloSharp.Integration.Avalonia;

namespace AvaloniaWinitDemo;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        var surface = this.FindControl<VelloSurfaceView>("Surface");
        if (surface is not null)
        {
            surface.IsLoopEnabled = true;
        }
    }
}
