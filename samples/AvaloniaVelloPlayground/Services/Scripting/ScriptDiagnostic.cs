using Microsoft.CodeAnalysis;

namespace AvaloniaVelloPlayground.Services.Scripting;

public sealed record ScriptDiagnostic(
    string Id,
    DiagnosticSeverity Severity,
    string Message,
    int? Line = null,
    int? Column = null)
{
    public bool IsError => Severity == DiagnosticSeverity.Error;

    public string ToDisplayString()
    {
        if (Line is int line && Column is int column)
        {
            return $"{Severity}: {Id} at {line}:{column} - {Message}";
        }

        return $"{Severity}: {Id} - {Message}";
    }
}
