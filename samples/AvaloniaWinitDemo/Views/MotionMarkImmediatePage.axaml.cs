using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvaloniaWinitDemo.Views;

public partial class MotionMarkImmediatePage : UserControl
{
    public MotionMarkImmediatePage()
    {
        InitializeComponent();
    }

    private void OnDecreaseComplexity(object? sender, RoutedEventArgs e)
    {
        ImmediateControl.DecreaseComplexity();
    }

    private void OnIncreaseComplexity(object? sender, RoutedEventArgs e)
    {
        ImmediateControl.IncreaseComplexity();
    }

    private void OnResetComplexity(object? sender, RoutedEventArgs e)
    {
        ImmediateControl.ResetComplexity();
    }
}
