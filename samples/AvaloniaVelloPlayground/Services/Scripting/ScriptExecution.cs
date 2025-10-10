using System;

namespace AvaloniaVelloPlayground.Services.Scripting;

public sealed class ScriptExecution
{
    private readonly Action<ScriptRenderContext> _render;

    public ScriptExecution(string name, Action<ScriptRenderContext> render, string generatedSource)
    {
        Name = name ?? "Script";
        _render = render ?? throw new ArgumentNullException(nameof(render));
        GeneratedSource = generatedSource;
    }

    public string Name { get; }

    public string GeneratedSource { get; }

    public void Render(ScriptRenderContext context) => _render(context);
}
