using System;
using System.Collections.Generic;
using SkiaSharpShim;

namespace SkiaSharp;

public sealed class SKTextBlob : IDisposable
{
    private readonly List<TextBlobRun> _runs;
    private bool _disposed;

    internal SKTextBlob(List<TextBlobRun> runs)
    {
        _runs = runs;
    }

    internal IReadOnlyList<TextBlobRun> Runs
    {
        get
        {
            ThrowIfDisposed();
            return _runs;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _runs.Clear();
    }

    public IReadOnlyList<float> GetIntercepts(float lowerLimit, float upperLimit)
    {
        ThrowIfDisposed();
        return Array.Empty<float>();
    }

    internal SKTextBlob Clone()
    {
        ThrowIfDisposed();
        var runs = new List<TextBlobRun>(_runs.Count);
        foreach (var run in _runs)
        {
            var glyphs = (ushort[])run.Glyphs.Clone();
            var positions = (SKPoint[])run.Positions.Clone();
            runs.Add(new TextBlobRun(run.FontSnapshot, run.Handle, glyphs, positions, run.Origin));
        }

        return new SKTextBlob(runs);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SKTextBlob));
        }
    }

    internal readonly struct TextBlobRun
    {
        public TextBlobRun(
            SKFont.FontSnapshot fontSnapshot,
            FontMeasurement.ShapedRunHandle? handle,
            ushort[] glyphs,
            SKPoint[] positions,
            SKPoint origin)
        {
            FontSnapshot = fontSnapshot;
            Handle = handle;
            Glyphs = glyphs;
            Positions = positions;
            Origin = origin;
        }

        public SKFont.FontSnapshot FontSnapshot { get; }
        public FontMeasurement.ShapedRunHandle? Handle { get; }
        public bool UsesHandle => Handle is not null;
        public ushort[] Glyphs { get; }
        public SKPoint[] Positions { get; }
        public SKPoint Origin { get; }
    }
}

public sealed class SKTextBlobBuilder : IDisposable
{
    private readonly List<PendingRun> _runs = new();

    public PositionedRunBuffer AllocatePositionedRun(SKFont font, int glyphCount, SKPoint origin = default)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (glyphCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(glyphCount));
        }

        var glyphs = new ushort[glyphCount];
        var positions = new SKPoint[glyphCount];
        var pending = new PendingRun(font.CreateSnapshot(), glyphs, positions, origin);
        _runs.Add(pending);
        return new PositionedRunBuffer(pending);
    }

    public void AddTextRun(SKFont font, ReadOnlySpan<char> text, SKPaint? paint = null, SKPoint origin = default)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (text.IsEmpty)
        {
            return;
        }

        var typeface = font.Typeface ?? SKTypeface.Default;
        var handle = FontMeasurement.Instance.GetOrCreateRun(typeface, font.Size, paint, text);
        handle.Touch();

        var pending = new PendingRun(font.CreateSnapshot(), Array.Empty<ushort>(), Array.Empty<SKPoint>(), origin);
        pending.SetHandle(handle);
        _runs.Add(pending);
    }

    public SKTextBlob? Build()
    {
        if (_runs.Count == 0)
        {
            return null;
        }

        var completedRuns = new List<SKTextBlob.TextBlobRun>(_runs.Count);
        foreach (var run in _runs)
        {
            if (run.HasHandle)
            {
                completedRuns.Add(new SKTextBlob.TextBlobRun(run.FontSnapshot, run.Handle, Array.Empty<ushort>(), Array.Empty<SKPoint>(), run.Origin));
                continue;
            }

            if (!run.IsComplete)
            {
                continue;
            }

            completedRuns.Add(new SKTextBlob.TextBlobRun(run.FontSnapshot, null, run.Glyphs, run.Positions, run.Origin));
        }

        _runs.Clear();

        if (completedRuns.Count == 0)
        {
            return null;
        }

        return new SKTextBlob(completedRuns);
    }

    public void Dispose()
    {
        _runs.Clear();
    }

    public readonly struct PositionedRunBuffer
    {
        private readonly PendingRun _run;

        internal PositionedRunBuffer(PendingRun run)
        {
            _run = run;
        }

        public void SetGlyphs(ReadOnlySpan<ushort> glyphs)
        {
            glyphs.CopyTo(_run.Glyphs);
            _run.MarkGlyphsSet();
        }

        public void SetGlyphs(ushort[] glyphs)
        {
            SetGlyphs(glyphs.AsSpan());
        }

        public void SetPositions(ReadOnlySpan<SKPoint> positions)
        {
            positions.CopyTo(_run.Positions);
            _run.MarkPositionsSet();
        }

        public void SetPositions(SKPoint[] positions)
        {
            SetPositions(positions.AsSpan());
        }

        public void SetOrigin(SKPoint origin)
        {
            _run.SetOrigin(origin);
        }
    }

    internal sealed class PendingRun
    {
        private bool _glyphsSet;
        private bool _positionsSet;

        internal PendingRun(SKFont.FontSnapshot fontSnapshot, ushort[] glyphs, SKPoint[] positions, SKPoint origin)
        {
            FontSnapshot = fontSnapshot;
            Glyphs = glyphs;
            Positions = positions;
            Origin = origin;
        }

        internal SKFont.FontSnapshot FontSnapshot { get; }
        internal ushort[] Glyphs { get; }
        internal SKPoint[] Positions { get; }
        internal SKPoint Origin { get; private set; }
        internal FontMeasurement.ShapedRunHandle? Handle { get; private set; }
        internal bool HasHandle => Handle is not null;
        internal bool IsComplete => HasHandle || (_glyphsSet && _positionsSet);

        internal void MarkGlyphsSet() => _glyphsSet = true;
        internal void MarkPositionsSet() => _positionsSet = true;
        internal void SetOrigin(SKPoint origin) => Origin = origin;

        internal void SetHandle(FontMeasurement.ShapedRunHandle handle)
        {
            Handle = handle;
            _glyphsSet = true;
            _positionsSet = true;
        }
    }
}
