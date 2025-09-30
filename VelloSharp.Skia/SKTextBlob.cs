using System;
using System.Collections.Generic;

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
            runs.Add(new TextBlobRun(run.FontSnapshot, glyphs, positions));
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
        public TextBlobRun(SKFont.FontSnapshot fontSnapshot, ushort[] glyphs, SKPoint[] positions)
        {
            FontSnapshot = fontSnapshot;
            Glyphs = glyphs;
            Positions = positions;
        }

        public SKFont.FontSnapshot FontSnapshot { get; }
        public ushort[] Glyphs { get; }
        public SKPoint[] Positions { get; }
    }
}

public sealed class SKTextBlobBuilder : IDisposable
{
    private readonly List<PendingRun> _runs = new();

    public PositionedRunBuffer AllocatePositionedRun(SKFont font, int glyphCount)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (glyphCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(glyphCount));
        }

        var glyphs = new ushort[glyphCount];
        var positions = new SKPoint[glyphCount];
        var pending = new PendingRun(font.CreateSnapshot(), glyphs, positions);
        _runs.Add(pending);
        return new PositionedRunBuffer(pending);
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
            if (!run.IsComplete)
            {
                continue;
            }

            completedRuns.Add(new SKTextBlob.TextBlobRun(run.FontSnapshot, run.Glyphs, run.Positions));
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
    }

    internal sealed class PendingRun
    {
        private bool _glyphsSet;
        private bool _positionsSet;

        internal PendingRun(SKFont.FontSnapshot fontSnapshot, ushort[] glyphs, SKPoint[] positions)
        {
            FontSnapshot = fontSnapshot;
            Glyphs = glyphs;
            Positions = positions;
        }

        internal SKFont.FontSnapshot FontSnapshot { get; }
        internal ushort[] Glyphs { get; }
        internal SKPoint[] Positions { get; }
        internal bool IsComplete => _glyphsSet && _positionsSet;

        internal void MarkGlyphsSet() => _glyphsSet = true;
        internal void MarkPositionsSet() => _positionsSet = true;
    }
}
