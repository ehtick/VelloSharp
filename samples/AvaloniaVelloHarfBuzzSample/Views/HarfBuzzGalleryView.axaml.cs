using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AvaloniaVelloHarfBuzzSample.Views;

public partial class HarfBuzzGalleryView : UserControl
{
    public HarfBuzzGalleryView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
