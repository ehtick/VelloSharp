using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AvaloniaVelloSkiaSharpSample.Views.Pages;

public partial class GeometryExplorerPage : UserControl
{
    public GeometryExplorerPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
