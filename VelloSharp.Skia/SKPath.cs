using System.Collections.Generic;
using VelloSharp;

namespace SkiaSharp;

public sealed class SKPath : IDisposable
{
    private readonly List<PathCommand> _commands = new();

    public void MoveTo(float x, float y) => MoveTo(new SKPoint(x, y));

    public void MoveTo(SKPoint point)
    {
        _commands.Add(new PathCommand(PathVerb.MoveTo, point));
    }

    public void LineTo(float x, float y) => LineTo(new SKPoint(x, y));

    public void LineTo(SKPoint point)
    {
        _commands.Add(new PathCommand(PathVerb.LineTo, point));
    }

    public void QuadTo(SKPoint control, SKPoint end)
    {
        _commands.Add(new PathCommand(PathVerb.QuadTo, control, end));
    }

    public void CubicTo(SKPoint control1, SKPoint control2, SKPoint end)
    {
        _commands.Add(new PathCommand(PathVerb.CubicTo, control1, control2, end));
    }

    public void Reset() => _commands.Clear();

    public void Close() => _commands.Add(PathCommand.ClosePath);

    public void Dispose()
    {
        _commands.Clear();
    }

    internal PathBuilder ToPathBuilder()
    {
        var builder = new PathBuilder();
        foreach (var command in _commands)
        {
            switch (command.Verb)
            {
                case PathVerb.MoveTo:
                    builder.MoveTo(command.Point0.X, command.Point0.Y);
                    break;
                case PathVerb.LineTo:
                    builder.LineTo(command.Point0.X, command.Point0.Y);
                    break;
                case PathVerb.QuadTo:
                    builder.QuadraticTo(command.Point0.X, command.Point0.Y, command.Point1.X, command.Point1.Y);
                    break;
                case PathVerb.CubicTo:
                    builder.CubicTo(
                        command.Point0.X,
                        command.Point0.Y,
                        command.Point1.X,
                        command.Point1.Y,
                        command.Point2.X,
                        command.Point2.Y);
                    break;
                case PathVerb.Close:
                    builder.Close();
                    break;
            }
        }

        return builder;
    }

    private readonly record struct PathCommand
    {
        public PathCommand(PathVerb verb, SKPoint point0)
        {
            Verb = verb;
            Point0 = point0;
            Point1 = default;
            Point2 = default;
        }

        public PathCommand(PathVerb verb, SKPoint point0, SKPoint point1)
        {
            Verb = verb;
            Point0 = point0;
            Point1 = point1;
            Point2 = default;
        }

        public PathCommand(PathVerb verb, SKPoint point0, SKPoint point1, SKPoint point2)
        {
            Verb = verb;
            Point0 = point0;
            Point1 = point1;
            Point2 = point2;
        }

        public static PathCommand ClosePath => new(PathVerb.Close, default);

        public PathVerb Verb { get; }
        public SKPoint Point0 { get; }
        public SKPoint Point1 { get; }
        public SKPoint Point2 { get; }
    }

    private enum PathVerb
    {
        MoveTo,
        LineTo,
        QuadTo,
        CubicTo,
        Close,
    }
}
