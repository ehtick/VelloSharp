using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AvaloniaVelloHarfBuzzSample.Views.Pages;

public partial class WelcomePage : UserControl
{
    public WelcomePage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
