using System;
using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvaloniaVelloExamples.Scenes;

namespace AvaloniaVelloExamples.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const int DefaultComplexity = 1;
    private const int MaxComplexity = 50;

    public ManagedSceneHost Host { get; }
    public IReadOnlyList<ExampleScene> Scenes { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousSceneCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextSceneCommand))]
    private int selectedSceneIndex;

    [ObservableProperty]
    private int complexity = DefaultComplexity;

    [ObservableProperty]
    private bool isInteractive = true;

    public event Action? ResetViewRequested;

    public MainWindowViewModel()
    {
        var assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets", "vello");
        if (!Directory.Exists(assetsPath))
        {
            assetsPath = null;
        }

        Host = ManagedSceneHost.Create(assetsPath);
        Scenes = Host.Scenes;
        if (Scenes.Count > 0)
        {
            SelectedSceneIndex = 0;
        }
    }

    partial void OnComplexityChanged(int value)
    {
        if (value < 1)
        {
            Complexity = 1;
        }
        else if (value > MaxComplexity)
        {
            Complexity = MaxComplexity;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSelectPreviousScene))]
    private void PreviousScene()
    {
        if (Scenes.Count == 0)
        {
            return;
        }

        SelectedSceneIndex = (SelectedSceneIndex - 1 + Scenes.Count) % Scenes.Count;
    }

    [RelayCommand(CanExecute = nameof(CanSelectNextScene))]
    private void NextScene()
    {
        if (Scenes.Count == 0)
        {
            return;
        }

        SelectedSceneIndex = (SelectedSceneIndex + 1) % Scenes.Count;
    }

    [RelayCommand]
    private void IncreaseComplexity()
    {
        if (Complexity < MaxComplexity)
        {
            Complexity++;
        }
    }

    [RelayCommand]
    private void DecreaseComplexity()
    {
        if (Complexity > 1)
        {
            Complexity--;
        }
    }

    [RelayCommand]
    private void ResetView()
    {
        ResetViewRequested?.Invoke();
    }

    private bool CanSelectPreviousScene() => Scenes.Count > 0;
    private bool CanSelectNextScene() => Scenes.Count > 0;
}
