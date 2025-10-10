using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaVelloPlayground.Services;
using AvaloniaVelloPlayground.Services.Scripting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaVelloPlayground.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ScriptCompiler _compiler = new();
    private readonly ScriptExampleCatalog _catalog = new();
    private readonly ObservableCollection<ScriptExample> _examples;
    private CancellationTokenSource? _compilationCts;

    private ScriptExample? _selectedExample;
    private ScriptExecution? _activeExecution;
    private string _scriptText = string.Empty;
    private string _statusMessage = "Ready";
    private string _diagnostics = string.Empty;
    private bool _hasErrors;
    private bool _isCompiling;
    private bool _hasPendingChanges;

    public MainViewModel()
    {
        _examples = new ObservableCollection<ScriptExample>(_catalog.GetExamples());
        RunCommand = new AsyncRelayCommand(CompileAsync, () => !_isCompiling && !string.IsNullOrWhiteSpace(ScriptText));
        ResetToExampleCommand = new RelayCommand(ResetToSelectedExample, () => SelectedExample is not null);
        if (_examples.Count > 0)
        {
            SelectedExample = _examples[0];
            ScriptText = SelectedExample.Code;
            _ = CompileAsync();
        }
    }

    public ObservableCollection<ScriptExample> Examples => _examples;

    public ScriptExample? SelectedExample
    {
        get => _selectedExample;
        set
        {
            if (SetProperty(ref _selectedExample, value))
            {
                ResetToSelectedExample();
                ResetToExampleCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ScriptText
    {
        get => _scriptText;
        set
        {
            if (SetProperty(ref _scriptText, value))
            {
                HasPendingChanges = true;
                RunCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public ScriptExecution? ActiveExecution
    {
        get => _activeExecution;
        private set => SetProperty(ref _activeExecution, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string Diagnostics
    {
        get => _diagnostics;
        private set => SetProperty(ref _diagnostics, value);
    }

    public bool HasErrors
    {
        get => _hasErrors;
        private set => SetProperty(ref _hasErrors, value);
    }

    public bool IsCompiling
    {
        get => _isCompiling;
        private set
        {
            if (SetProperty(ref _isCompiling, value))
            {
                RunCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasPendingChanges
    {
        get => _hasPendingChanges;
        private set => SetProperty(ref _hasPendingChanges, value);
    }

    public IAsyncRelayCommand RunCommand { get; }

    public IRelayCommand ResetToExampleCommand { get; }

    private void ResetToSelectedExample()
    {
        if (SelectedExample is null)
        {
            return;
        }

        ScriptText = SelectedExample.Code;
        HasPendingChanges = false;
        StatusMessage = $"Loaded example: {SelectedExample.Category} · {SelectedExample.Name}";
    }

    private async Task CompileAsync()
    {
        _compilationCts?.Cancel();
        _compilationCts?.Dispose();
        _compilationCts = new CancellationTokenSource();
        var token = _compilationCts.Token;

        try
        {
            IsCompiling = true;
            StatusMessage = "Compiling...";
            Diagnostics = string.Empty;
            HasErrors = false;

            var name = SelectedExample?.Name ?? "Custom Script";
            var result = await _compiler.CompileAsync(ScriptText, name, token).ConfigureAwait(false);
            var diagnostics = string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.ToDisplayString()));

            if (result.Diagnostics.Count > 0)
            {
                foreach (var diagnostic in result.Diagnostics)
                {
                    var message = $"[ScriptCompile] {diagnostic.ToDisplayString()}";
                    if (diagnostic.IsError)
                    {
                        Console.Error.WriteLine(message);
                    }
                    else
                    {
                        Console.WriteLine(message);
                    }
                }
            }

            HasErrors = result.Diagnostics.Any(d => d.IsError);
            Diagnostics = diagnostics;
            ActiveExecution = result.Execution;
            HasPendingChanges = false;
            StatusMessage = HasErrors
                ? "Compilation failed."
                : "Compilation succeeded.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Compilation canceled.";
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ScriptCompile] Unexpected failure: {ex}");
            HasErrors = true;
            Diagnostics = ex.ToString();
            ActiveExecution = null;
            StatusMessage = "Compilation crashed.";
        }
        finally
        {
            IsCompiling = false;
        }
    }
}
