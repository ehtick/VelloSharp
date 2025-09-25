using System;
using Avalonia.Controls;
using AvaloniaVelloExamples.ViewModels;

namespace AvaloniaVelloExamples.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closed += OnClosed;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.ResetViewRequested -= OnResetViewRequested;
        }

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.ResetViewRequested += OnResetViewRequested;
        }
    }

    private void OnResetViewRequested()
    {
        ExamplesRenderer.ResetView();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.ResetViewRequested -= OnResetViewRequested;
            _viewModel.Host.Dispose();
        }
    }
}
