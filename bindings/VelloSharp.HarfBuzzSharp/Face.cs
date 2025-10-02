using System;

namespace HarfBuzzSharp;

public sealed class Face : IDisposable
{
    private readonly Func<Face, Tag, Blob?>? _tableProvider;
    private readonly Blob? _blob;

    public Face(Func<Face, Tag, Blob?> tableProvider)
    {
        _tableProvider = tableProvider ?? throw new ArgumentNullException(nameof(tableProvider));
    }

    public Face(Blob blob, int index = 0)
    {
        _blob = blob ?? throw new ArgumentNullException(nameof(blob));
    }

    public ushort UnitsPerEm { get; set; }

    public uint GlyphCount { get; set; }

    internal Blob? Blob => _blob;

    internal Blob? GetTable(Tag tag)
    {
        if (_tableProvider is not null)
        {
            return _tableProvider(this, tag);
        }

        return _blob;
    }

    public void Dispose()
    {
        _blob?.Dispose();
    }
}
