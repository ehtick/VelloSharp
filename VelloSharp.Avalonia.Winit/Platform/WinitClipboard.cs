using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;

namespace Avalonia.Winit;

internal sealed class WinitClipboard : IClipboard
{
    private readonly object _lock = new();
    private string? _text;

    public Task ClearAsync()
    {
        lock (_lock)
        {
            _text = null;
        }
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_text);
        }
    }

    public Task SetTextAsync(string? text)
    {
        lock (_lock)
        {
            _text = text;
        }
        return Task.CompletedTask;
    }

    public Task SetDataObjectAsync(IDataObject data)
    {
        lock (_lock)
        {
            _text = data.Contains(DataFormats.Text) ? data.Get(DataFormats.Text) as string : null;
        }
        return Task.CompletedTask;
    }

    public Task FlushAsync() => Task.CompletedTask;

    public Task<string[]> GetFormatsAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_text is null ? Array.Empty<string>() : new[] { DataFormats.Text });
        }
    }

    public Task<object?> GetDataAsync(string format)
    {
        lock (_lock)
        {
            if (format == DataFormats.Text)
            {
                return Task.FromResult<object?>(_text);
            }

            return Task.FromResult<object?>(null);
        }
    }

    public Task<IDataObject?> TryGetInProcessDataObjectAsync()
    {
        return Task.FromResult<IDataObject?>(null);
    }
}
