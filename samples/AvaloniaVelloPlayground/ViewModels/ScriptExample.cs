namespace AvaloniaVelloPlayground.ViewModels;

public sealed class ScriptExample
{
    public ScriptExample(string name, string category, string code, string? description = null)
    {
        Name = name;
        Category = category;
        Code = code;
        Description = description;
    }

    public string Name { get; }

    public string Category { get; }

    public string Code { get; }

    public string? Description { get; }

    public override string ToString() => $"{Category} · {Name}";
}
