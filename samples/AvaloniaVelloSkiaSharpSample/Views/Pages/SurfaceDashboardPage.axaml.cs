using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AvaloniaVelloSkiaSharpSample.Views.Pages;

public partial class SurfaceDashboardPage : UserControl
{
    public SurfaceDashboardPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
