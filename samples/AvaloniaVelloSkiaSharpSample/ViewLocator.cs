using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using AvaloniaVelloSkiaSharpSample.ViewModels;

namespace AvaloniaVelloSkiaSharpSample;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        return Resolve(param);
    }

    public bool Match(object? data) => data is ViewModelBase;

    public static Control? BuildStatic(object? param)
        => Resolve(param);

    private static Control? Resolve(object? param)
    {
        if (param is null)
        {
            return null;
        }

        var viewModelType = param.GetType();
        var name = viewModelType.FullName!;
        var candidate = name.Replace(".ViewModels.", ".Views.", StringComparison.Ordinal)
            .Replace("ViewModel", string.Empty, StringComparison.Ordinal);

        var assembly = viewModelType.Assembly;
        var type = assembly.GetType(candidate)
                   ?? assembly.GetType(candidate + "Page")
                   ?? assembly.GetType(candidate + "View")
                   ?? Type.GetType(candidate);

        if (type is not null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }
}
