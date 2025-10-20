using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvaloniaVelloCommon.Views;

public partial class SvgDemoPage : UserControl
{
    private const string DefaultAssetPath = "avares://AvaloniaVelloCommon/Assets/Svg/Tiger.svg";

    public SvgDemoPage()
    {
        InitializeComponent();

        AssetCombo.SelectedIndex = 1;
        ApplySelectedAsset();
        DemoSvg.LayoutUpdated += OnSvgLayoutUpdated;
    }

    private void OnSvgLayoutUpdated(object? sender, EventArgs e)
    {
        UpdateStatus();
    }

    private void OnReloadClick(object? sender, RoutedEventArgs e)
    {
        var path = GetSelectedAssetPath();
        DemoSvg.Path = null;
        DemoSvg.Source = null;
        DemoSvg.Path = path;
        UpdateStatus();
    }

    private void OnAssetSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ApplySelectedAsset();
    }

    private void UpdateStatus()
    {
        BackendStatusText.Text = DemoSvg.VelloSvg is not null
            ? "Rendering via Vello (IVello lease acquired)"
            : "Rendering via Skia fallback";
    }

    private void ApplySelectedAsset()
    {
        var path = GetSelectedAssetPath();
        SourceText.Text = path;
        DemoSvg.Source = null;
        DemoSvg.Path = path;
        UpdateStatus();
    }

    private string GetSelectedAssetPath()
    {
        if (AssetCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
        {
            return tag;
        }

        return DefaultAssetPath;
    }
}
