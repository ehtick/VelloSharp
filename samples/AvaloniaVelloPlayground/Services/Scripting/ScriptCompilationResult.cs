using System.Collections.Generic;

namespace AvaloniaVelloPlayground.Services.Scripting;

public sealed class ScriptCompilationResult
{
    public ScriptCompilationResult(
        ScriptExecution? execution,
        IReadOnlyList<ScriptDiagnostic> diagnostics,
        string generatedSource)
    {
        Execution = execution;
        Diagnostics = diagnostics;
        GeneratedSource = generatedSource;
    }

    public ScriptExecution? Execution { get; }

    public IReadOnlyList<ScriptDiagnostic> Diagnostics { get; }

    public string GeneratedSource { get; }
}
