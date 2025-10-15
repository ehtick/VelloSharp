using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AvaloniaVelloSkiaSharpSample.Views.Pages;

public partial class CanvasPaintStudioPage : UserControl
{
    public CanvasPaintStudioPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
