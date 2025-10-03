using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvaloniaWinitDemo.Views;

public partial class MotionMarkLeasePage : UserControl
{
    public MotionMarkLeasePage()
    {
        InitializeComponent();
    }

    private void OnDecreaseComplexity(object? sender, RoutedEventArgs e)
    {
        LeaseControl.DecreaseComplexity();
    }

    private void OnIncreaseComplexity(object? sender, RoutedEventArgs e)
    {
        LeaseControl.IncreaseComplexity();
    }

    private void OnResetComplexity(object? sender, RoutedEventArgs e)
    {
        LeaseControl.ResetComplexity();
    }
}
