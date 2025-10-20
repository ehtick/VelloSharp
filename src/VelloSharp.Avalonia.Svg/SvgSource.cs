using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Metadata;
using Avalonia.Platform;
using VelloSharp;

namespace VelloSharp.Avalonia.Svg;

/// <summary>
/// Wraps a Vello-backed SVG and mirrors the legacy loading helper surface.
/// </summary>
[TypeConverter(typeof(SvgSourceTypeConverter))]
public sealed class SvgSource : IDisposable
{
    private static readonly HttpClient s_httpClient = new();

    private readonly Uri? _baseUri;
    private VelloSvg? _vello;
    private string? _originalPath;
    private byte[]? _originalBytes;
    private SvgParameters? _originalParameters;

    public static bool EnableThrowOnMissingResource { get; set; }

    public object Sync { get; } = new();

    [Content]
    public string? Path { get; init; }

    public Dictionary<string, string>? Entities { get; init; }

    public string? Css { get; init; }

    public VelloSvg? Vello
    {
        get
        {
            lock (Sync)
            {
                if (_vello is null && Path is not null && _originalBytes is null)
                {
                    LoadFromPathCore(Path, _baseUri, _originalParameters);
                }

                return _vello;
            }
        }
    }

    public Size Size
    {
        get
        {
            lock (Sync)
            {
                if (_vello is { } vello)
                {
                    var vec = vello.Size;
                    if (vec.X > 0 && vec.Y > 0)
                    {
                        return new Size(vec.X, vec.Y);
                    }
                }

                return default;
            }
        }
    }

    public SvgSource(Uri? baseUri)
    {
        _baseUri = baseUri;
    }

    public SvgSource(IServiceProvider services)
    {
        _baseUri = services.GetContextBaseUri();
    }

    public void Dispose()
    {
        lock (Sync)
        {
            _vello?.Dispose();
            _vello = null;

            _originalBytes = null;
            _originalPath = null;
        }

        GC.SuppressFinalize(this);
    }

    public void ReLoad(SvgParameters? parameters = null)
    {
        lock (Sync)
        {
            _originalParameters = parameters;
            ReloadCore();
        }
    }

    private void ReloadCore()
    {
        _vello?.Dispose();
        _vello = null;

        if (_originalBytes is { Length: > 0 } bytes)
        {
            SetSourceFromBytes(bytes, _originalParameters);
            return;
        }

        if (_originalPath is { } path)
        {
            LoadFromPathCore(path, _baseUri, _originalParameters);
        }
    }

    public static SvgSource Load(string path, Uri? baseUri = default, SvgParameters? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
        }

        var source = new SvgSource(baseUri);
        source.LoadFromPath(path, parameters);
        return source;
    }

    public static SvgSource LoadFromSvg(string svg, SvgParameters? parameters = null)
    {
        if (svg is null)
        {
            throw new ArgumentNullException(nameof(svg));
        }

        var source = new SvgSource(baseUri: null);
        source.LoadFromBytes(Encoding.UTF8.GetBytes(svg), parameters);
        return source;
    }

    public static SvgSource LoadFromStream(Stream stream, SvgParameters? parameters = null)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        var source = new SvgSource(baseUri: null);
        source.LoadFromStreamInternal(stream, parameters);
        return source;
    }

    private void LoadFromPath(string path, SvgParameters? parameters)
    {
        lock (Sync)
        {
            _originalParameters = parameters;
            _originalPath = path;
            _originalBytes = null;
            LoadFromPathCore(path, _baseUri, parameters);
        }
    }

    private void LoadFromStreamInternal(Stream stream, SvgParameters? parameters)
    {
        lock (Sync)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var bytes = ms.ToArray();

            _originalBytes = bytes;
            _originalParameters = parameters;
            _originalPath = null;

            SetSourceFromBytes(bytes, parameters);
        }
    }

    private void LoadFromBytes(byte[] bytes, SvgParameters? parameters)
    {
        lock (Sync)
        {
            _originalBytes = bytes;
            _originalParameters = parameters;
            _originalPath = null;

            SetSourceFromBytes(bytes, parameters);
        }
    }

    private void LoadFromPathCore(string path, Uri? baseUri, SvgParameters? parameters)
    {
        if (File.Exists(path))
        {
            SetSourceFromFile(path, parameters);
            return;
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
        {
            if (absolute.IsFile)
            {
                var filePath = absolute.LocalPath;
                if (File.Exists(filePath))
                {
                    SetSourceFromFile(filePath, parameters);
                    return;
                }

                HandleMissing(path);
                return;
            }

            if (absolute.Scheme is "http" or "https")
            {
                TryLoadFromHttp(absolute, parameters);
                return;
            }
        }

        LoadFromAsset(path, baseUri, parameters);
    }

    private void SetSourceFromFile(string path, SvgParameters? parameters)
    {
        if (!File.Exists(path))
        {
            HandleMissing(path);
            return;
        }

        if (RequiresTextProcessing(parameters))
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                SetSourceFromBytes(bytes, parameters);
                return;
            }
            catch
            {
                HandleMissing(path);
                return;
            }
        }

        AssignSource(SafeLoadVelloFromFile(path));
    }

    private void SetSourceFromBytes(byte[] bytes, SvgParameters? parameters)
    {
        AssignSource(SafeLoadVelloFromBytes(ApplyOverrides(bytes, parameters)));
    }

    private void AssignSource(VelloSvg? vello)
    {
        _vello?.Dispose();
        _vello = vello;
    }

    private void TryLoadFromHttp(Uri uri, SvgParameters? parameters)
    {
        try
        {
            using var response = s_httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).Result;
            if (!response.IsSuccessStatusCode)
            {
                HandleMissing(uri.ToString());
                return;
            }

            using var stream = response.Content.ReadAsStreamAsync().Result;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var bytes = ms.ToArray();

            _originalBytes = bytes;
            SetSourceFromBytes(bytes, parameters);
        }
        catch
        {
            HandleMissing(uri.ToString());
        }
    }

    private void LoadFromAsset(string path, Uri? baseUri, SvgParameters? parameters)
    {
        var uri = CreateRelativeUri(path);
        try
        {
            using var stream = AssetLoader.Open(uri, baseUri);
            if (stream is null)
            {
                HandleMissing(path);
                return;
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);

            var bytes = ms.ToArray();
            _originalBytes = bytes;
            SetSourceFromBytes(bytes, parameters);
        }
        catch
        {
            HandleMissing(path);
        }
    }

    private static VelloSvg? SafeLoadVelloFromFile(string path)
    {
        try
        {
            return VelloSvg.LoadFromFile(path);
        }
        catch
        {
            HandleMissing(path);
            return null;
        }
    }

    private static VelloSvg? SafeLoadVelloFromBytes(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return null;
        }

        try
        {
            return VelloSvg.LoadFromUtf8(bytes);
        }
        catch
        {
            return null;
        }
    }

    private byte[] ApplyOverrides(byte[] bytes, SvgParameters? parameters)
    {
        if (!RequiresTextProcessing(parameters))
        {
            return bytes;
        }

        try
        {
            var text = Encoding.UTF8.GetString(bytes);
            if (CombineEntities(parameters) is { Count: > 0 } entities)
            {
                foreach (var kvp in entities)
                {
                    if (string.IsNullOrEmpty(kvp.Key))
                    {
                        continue;
                    }

                    text = text.Replace($"&{kvp.Key};", kvp.Value ?? string.Empty, StringComparison.Ordinal);
                }
            }

            if (CombineCss(parameters) is { Length: > 0 } css)
            {
                text = InjectCss(text, css);
            }

            return Encoding.UTF8.GetBytes(text);
        }
        catch
        {
            return bytes;
        }
    }

    private Dictionary<string, string>? CombineEntities(SvgParameters? parameters)
    {
        Dictionary<string, string>? combined = null;

        void Append(Dictionary<string, string>? source)
        {
            if (source is null || source.Count == 0)
            {
                return;
            }

            combined ??= new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kvp in source)
            {
                combined[kvp.Key] = kvp.Value;
            }
        }

        Append(Entities);
        Append(parameters?.Entities);

        return combined;
    }

    private string? CombineCss(SvgParameters? parameters)
    {
        var cssA = Css;
        var cssB = parameters?.Css;

        if (string.IsNullOrWhiteSpace(cssA))
        {
            return string.IsNullOrWhiteSpace(cssB) ? null : cssB;
        }

        if (string.IsNullOrWhiteSpace(cssB))
        {
            return cssA;
        }

        return string.Concat(cssA, ' ', cssB);
    }

    private static string InjectCss(string svgText, string css)
    {
        var styleBlock = "<style type=\"text/css\"><![CDATA[" + css + "]]></style>";
        var svgIndex = svgText.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
        if (svgIndex < 0)
        {
            return svgText;
        }

        var insertIndex = svgText.IndexOf('>', svgIndex);
        if (insertIndex < 0)
        {
            return svgText;
        }

        return svgText.Insert(insertIndex + 1, styleBlock);
    }

    private bool RequiresTextProcessing(SvgParameters? parameters)
    {
        if (!string.IsNullOrWhiteSpace(Css) ||
            (Entities is { Count: > 0 }))
        {
            return true;
        }

        if (parameters is null)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(parameters.Css) ||
               (parameters.Entities is { Count: > 0 });
    }

    private static void HandleMissing(string path)
    {
        if (EnableThrowOnMissingResource)
        {
            throw new FileNotFoundException($"Unable to resolve SVG resource '{path}'.", path);
        }
    }

    private static Uri CreateRelativeUri(string path)
    {
        return path.StartsWith("/", StringComparison.Ordinal)
            ? new Uri(path, UriKind.Relative)
            : new Uri(path, UriKind.RelativeOrAbsolute);
    }
}
