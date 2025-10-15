using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AvaloniaVelloSkiaSharpSample.Views.Pages;

public partial class RecordingStudioPage : UserControl
{
    public RecordingStudioPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
