using System;

namespace HarfBuzzSharp;

public sealed class Face : NativeObject
{
    private readonly IFaceTableProvider? _tableProvider;
    private readonly Func<Face, Tag, Blob?>? _legacyTableProvider;
    private readonly Blob? _blob;
    private global::VelloSharp.Font? _font;
    private readonly object _tablesSync = new();
    private readonly object _variationSync = new();
    private bool _isImmutable;
    private int _index;
    private Tag[]? _tables;
    private VariationAxis[]? _variationAxes;
    private int _unitsPerEm;

    public Face(Func<Face, Tag, Blob?> tableProvider)
        : base(IntPtr.Zero)
    {
        _legacyTableProvider = tableProvider ?? throw new ArgumentNullException(nameof(tableProvider));
    }

    public Face(IFaceTableProvider tableProvider)
        : base(IntPtr.Zero)
    {
        _tableProvider = tableProvider ?? throw new ArgumentNullException(nameof(tableProvider));
    }

    public Face(Blob blob, int index = 0)
        : base(IntPtr.Zero)
    {
        _blob = blob ?? throw new ArgumentNullException(nameof(blob));
        Index = index;
        InitializeMetrics();
    }

    public int Index
    {
        get => _index;
        set
        {
            ValidateIndex(value);
            _index = value;
            _tables = null;
            _variationAxes = null;
            InitializeMetrics();
        }
    }

    public int UnitsPerEm
    {
        get => _unitsPerEm;
        set
        {
            if (_isImmutable)
            {
                throw new InvalidOperationException("Face is immutable.");
            }

            _unitsPerEm = value;
        }
    }

    public int GlyphCount { get; private set; }

    public bool IsImmutable => _isImmutable;

    public Tag[] Tables
    {
        get
        {
            if (_tableProvider is not null)
            {
                return EnsureTableCache(() => _tableProvider.GetTableTags(this) ?? Array.Empty<Tag>());
            }

            if (_legacyTableProvider is not null)
            {
                return Array.Empty<Tag>();
            }

            if (_font is null)
            {
                InitializeMetrics();
            }

            if (_font is null)
            {
                return Array.Empty<Tag>();
            }

            return EnsureTableCache(() => LoadTables(_font));
        }
    }

    public VariationAxis[] VariationAxes
    {
        get
        {
            if (_tableProvider is not null && _font is null)
            {
                InitializeMetrics();
            }

            if (_font is null)
            {
                return Array.Empty<VariationAxis>();
            }

            lock (_variationSync)
            {
                if (_variationAxes is not null)
                {
                    return _variationAxes;
                }

                var axes = LoadVariationAxes(_font);
                _variationAxes = axes;
                return axes;
            }
        }
    }

    internal Blob? Blob => _blob;

    internal Blob? GetTable(Tag tag)
    {
        if (_tableProvider is not null)
        {
            var blob = _tableProvider.GetTable(this, tag);
            if (blob is not null)
            {
                CacheTableTag(tag);
            }

            return blob;
        }

        if (_legacyTableProvider is not null)
        {
            return _legacyTableProvider(this, tag);
        }

        return _blob;
    }

    private static Tag[] LoadTables(global::VelloSharp.Font font)
    {
        var status = global::VelloSharp.NativeMethods.vello_font_get_table_tags(
            font.Handle,
            out var handle,
            out var native);

        if (status != global::VelloSharp.VelloStatus.Success)
        {
            if (handle != IntPtr.Zero)
            {
                global::VelloSharp.NativeMethods.vello_font_table_tags_destroy(handle);
            }

            return Array.Empty<Tag>();
        }

        try
        {
            if (native.Count == 0 || native.Tags == IntPtr.Zero)
            {
                return Array.Empty<Tag>();
            }

            var count = checked((int)native.Count);
            var result = new Tag[count];

            unsafe
            {
                var tags = (uint*)native.Tags;
                for (var i = 0; i < count; i++)
                {
                    result[i] = tags[i];
                }
            }

            return result;
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                global::VelloSharp.NativeMethods.vello_font_table_tags_destroy(handle);
            }
        }
    }

    public Blob ReferenceTable(Tag tag)
    {
        if (_tableProvider is not null)
        {
            return _tableProvider.GetTable(this, tag) is { } blob
                ? CacheAndReturnTable(tag, blob)
                : Blob.Empty;
        }

        if (_legacyTableProvider is not null)
        {
            return _legacyTableProvider(this, tag) ?? Blob.Empty;
        }

        if (_font is null)
        {
            InitializeMetrics();
        }

        if (_font is null)
        {
            return _blob ?? Blob.Empty;
        }

        var status = global::VelloSharp.NativeMethods.vello_font_reference_table(
            _font.Handle,
            (uint)tag,
            out var handle,
            out var native);

        if (status != global::VelloSharp.VelloStatus.Success)
        {
            if (handle != IntPtr.Zero)
            {
                global::VelloSharp.NativeMethods.vello_font_table_data_destroy(handle);
            }

            return _blob ?? Blob.Empty;
        }

        if (native.Length == 0 || native.Data == IntPtr.Zero)
        {
            if (handle != IntPtr.Zero)
            {
                global::VelloSharp.NativeMethods.vello_font_table_data_destroy(handle);
            }

            return Blob.Empty;
        }

        var tableHandle = handle;
        var length = checked((int)native.Length);

        ReleaseDelegate release = () =>
        {
            if (tableHandle != IntPtr.Zero)
            {
                global::VelloSharp.NativeMethods.vello_font_table_data_destroy(tableHandle);
            }
        };

        try
        {
            var blob = new Blob(native.Data, length, MemoryMode.ReadOnly, release);
            blob.MakeImmutable();
            handle = IntPtr.Zero;
            return blob;
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                global::VelloSharp.NativeMethods.vello_font_table_data_destroy(handle);
            }
        }
    }

    public void MakeImmutable() => _isImmutable = true;

    protected override void DisposeHandler()
    {
        _font?.Dispose();
    }

    private void InitializeMetrics()
    {
        if (_blob is null)
        {
            return;
        }

        try
        {
            _font?.Dispose();
            _tables = null;
            _variationAxes = null;

            var span = _blob.AsSpan();
            if (span.IsEmpty)
            {
                _unitsPerEm = 0;
                GlyphCount = 0;
                _font = null;
                return;
            }

            var data = span.ToArray();
            _font = global::VelloSharp.Font.Load(data, (uint)Math.Max(0, _index));
            var status = global::VelloSharp.NativeMethods.vello_font_get_metrics(
                _font.Handle,
                1f,
                out var metrics);

            if (status == global::VelloSharp.VelloStatus.Success)
            {
                _unitsPerEm = metrics.UnitsPerEm;
                GlyphCount = metrics.GlyphCount;
            }
            else
            {
                _unitsPerEm = 0;
                GlyphCount = 0;
            }
        }
        catch
        {
            _unitsPerEm = 0;
            GlyphCount = 0;
            _font = null;
        }
    }

    private Tag[] EnsureTableCache(Func<Tag[]> loader)
    {
        lock (_tablesSync)
        {
            if (_tables is null)
            {
                var tags = loader() ?? Array.Empty<Tag>();
                _tables = tags.Length == 0 ? Array.Empty<Tag>() : tags;
            }

            return _tables;
        }
    }

    private Blob CacheAndReturnTable(Tag tag, Blob blob)
    {
        CacheTableTag(tag);
        return blob;
    }

    private void CacheTableTag(Tag tag)
    {
        lock (_tablesSync)
        {
            if (_tables is null || _tables.Length == 0)
            {
                _tables = new[] { tag };
                return;
            }

            for (var i = 0; i < _tables.Length; i++)
            {
                if (_tables[i] == tag)
                {
                    return;
                }
            }

            var expanded = new Tag[_tables.Length + 1];
            Array.Copy(_tables, expanded, _tables.Length);
            expanded[^1] = tag;
            _tables = expanded;
        }
    }

    private VariationAxis[] LoadVariationAxes(global::VelloSharp.Font font)
    {
        var status = global::VelloSharp.NativeMethods.vello_font_get_variation_axes(
            font.Handle,
            out var handle,
            out var native);

        if (status != global::VelloSharp.VelloStatus.Success)
        {
            if (handle != IntPtr.Zero)
            {
                global::VelloSharp.NativeMethods.vello_font_variation_axes_destroy(handle);
            }

            return Array.Empty<VariationAxis>();
        }

        try
        {
            if (native.Count == 0 || native.Axes == IntPtr.Zero)
            {
                return Array.Empty<VariationAxis>();
            }

            var count = checked((int)native.Count);
            var result = new VariationAxis[count];

            unsafe
            {
                var axes = (VariationAxisNative*)native.Axes;
                for (var i = 0; i < count; i++)
                {
                    var axis = axes[i];
                    result[i] = new VariationAxis(
                        (Tag)axis.Tag,
                        axis.MinValue,
                        axis.DefaultValue,
                        axis.MaxValue);
                }
            }

            return result;
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                global::VelloSharp.NativeMethods.vello_font_variation_axes_destroy(handle);
            }
        }
    }

    private void ValidateIndex(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        if (_blob is null)
        {
            return;
        }

        var faces = _blob.FaceCount;
        if (faces > 0 && value >= faces)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct VariationAxisNative
    {
        public uint Tag;
        public float MinValue;
        public float DefaultValue;
        public float MaxValue;
    }
}

