using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AvaloniaVelloSkiaSharpSample.Views.Pages;

public partial class IoDiagnosticsPage : UserControl
{
    public IoDiagnosticsPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
