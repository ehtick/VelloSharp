using System;
using System.Collections.Generic;
using System.Numerics;
using AvaloniaVelloSkiaSharpSample.Diagnostics;
using AvaloniaVelloSkiaSharpSample.Rendering;
using AvaloniaVelloSkiaSharpSample.Services;
using SkiaSharp;
using SampleSkiaBackendService = AvaloniaVelloSkiaSharpSample.Services.SkiaBackendService;

namespace AvaloniaVelloSkiaSharpSample.ViewModels.Pages;

public sealed class GeometryExplorerViewModel : SamplePageViewModel
{
    private const float CircleControlPoint = 0.55228475f;

    private readonly IReadOnlyList<GeometryShapeOption> _shapes =
    [
        new GeometryShapeOption("Rounded Rectangle", GeometryShape.RoundedRectangle),
        new GeometryShapeOption("Clover Loop", GeometryShape.Clover),
        new GeometryShapeOption("Wave Ribbon", GeometryShape.Wave),
        new GeometryShapeOption("Pentagon", GeometryShape.Pentagon),
    ];

    private readonly IReadOnlyList<PathOperationOption> _pathOperations =
    [
        new PathOperationOption("No combine", null, "Skip SKPath.Op and show individual fills."),
        new PathOperationOption("Union", SKPathOp.Union, "Fill area covered by either shape."),
        new PathOperationOption("Intersect", SKPathOp.Intersect, "Overlap shared by both shapes."),
        new PathOperationOption("Difference (A - B)", SKPathOp.Difference, "Primary minus secondary coverage."),
        new PathOperationOption("Reverse difference (B - A)", SKPathOp.ReverseDifference, "Secondary minus primary coverage."),
        new PathOperationOption("Exclusive OR", SKPathOp.Xor, "Regions belonging to only one shape."),
    ];

    private GeometryShapeOption _selectedPrimaryShape;
    private GeometryShapeOption _selectedSecondaryShape;

    private double _primaryRotation;
    private double _primaryScale = 1.0;
    private double _primaryOffsetX;
    private double _primaryOffsetY;

    private bool _showBounds = true;
    private bool _showControlPoints = true;
    private bool _showSecondary = true;

    private double _secondaryRotation = 34.0;
    private double _secondaryScale = 0.72;
    private double _secondaryOffsetX = 68.0;
    private double _secondaryOffsetY = -36.0;

    private string _geometrySummary = string.Empty;
    private PathOperationOption _selectedPathOperation;
    private bool _showBooleanResult = true;
    private string _booleanSummary = "Boolean combine disabled.";

    public GeometryExplorerViewModel(
        SkiaCaptureRecorder? captureRecorder = null,
        SampleSkiaBackendService? backendService = null,
        SkiaResourceService? resourceService = null)
        : base(
            "Geometry Explorer",
            "Manipulate composite paths, inspect tight bounds, and visualise control handles with the shimmed Skia geometry stack.",
            null,
            captureRecorder,
            backendService,
        resourceService)
    {
        _selectedPrimaryShape = _shapes[0];
        _selectedSecondaryShape = _shapes[1];
        _selectedPathOperation = _pathOperations[0];
        BooleanSummary = _booleanSummary;
        UpdateGeometrySummary(default, null);
    }

    protected override string CaptureLabel => "geometry-explorer";

    public IReadOnlyList<GeometryShapeOption> Shapes => _shapes;

    public IReadOnlyList<PathOperationOption> PathOperations => _pathOperations;

    public PathOperationOption SelectedPathOperation
    {
        get => _selectedPathOperation;
        set
        {
            if (SetAndRequestRender(ref _selectedPathOperation, value))
            {
                BooleanSummary = value.Description;
            }
        }
    }

    public bool ShowBooleanResult
    {
        get => _showBooleanResult;
        set => SetAndRequestRender(ref _showBooleanResult, value);
    }

    public string BooleanSummary
    {
        get => _booleanSummary;
        private set => RaiseAndSetIfChanged(ref _booleanSummary, value);
    }

    public GeometryShapeOption SelectedPrimaryShape
    {
        get => _selectedPrimaryShape;
        set
        {
            if (SetAndRequestRender(ref _selectedPrimaryShape, value))
            {
                UpdateGeometrySummary(default, null);
            }
        }
    }

    public GeometryShapeOption SelectedSecondaryShape
    {
        get => _selectedSecondaryShape;
        set
        {
            if (SetAndRequestRender(ref _selectedSecondaryShape, value))
            {
                UpdateGeometrySummary(default, null);
            }
        }
    }

    public double PrimaryRotation
    {
        get => _primaryRotation;
        set => SetAndRequestRender(ref _primaryRotation, NormalizeAngle(value));
    }

    public double PrimaryScale
    {
        get => _primaryScale;
        set => SetAndRequestRender(ref _primaryScale, Math.Clamp(value, 0.35, 1.8));
    }

    public double PrimaryOffsetX
    {
        get => _primaryOffsetX;
        set => SetAndRequestRender(ref _primaryOffsetX, Math.Clamp(value, -220, 220));
    }

    public double PrimaryOffsetY
    {
        get => _primaryOffsetY;
        set => SetAndRequestRender(ref _primaryOffsetY, Math.Clamp(value, -220, 220));
    }

    public bool ShowBounds
    {
        get => _showBounds;
        set => SetAndRequestRender(ref _showBounds, value);
    }

    public bool ShowControlPoints
    {
        get => _showControlPoints;
        set => SetAndRequestRender(ref _showControlPoints, value);
    }

    public bool ShowSecondary
    {
        get => _showSecondary;
        set => SetAndRequestRender(ref _showSecondary, value);
    }

    public double SecondaryRotation
    {
        get => _secondaryRotation;
        set => SetAndRequestRender(ref _secondaryRotation, NormalizeAngle(value));
    }

    public double SecondaryScale
    {
        get => _secondaryScale;
        set => SetAndRequestRender(ref _secondaryScale, Math.Clamp(value, 0.3, 1.4));
    }

    public double SecondaryOffsetX
    {
        get => _secondaryOffsetX;
        set => SetAndRequestRender(ref _secondaryOffsetX, Math.Clamp(value, -220, 220));
    }

    public double SecondaryOffsetY
    {
        get => _secondaryOffsetY;
        set => SetAndRequestRender(ref _secondaryOffsetY, Math.Clamp(value, -220, 220));
    }

    public string GeometrySummary
    {
        get => _geometrySummary;
        private set => RaiseAndSetIfChanged(ref _geometrySummary, value);
    }

    public override void Render(in SkiaLeaseRenderContext context)
    {
        var canvas = context.Canvas;
        var info = context.ImageInfo;
        canvas.Clear(new SKColor(10, 16, 26, 255));

        var baseScale = MathF.Min(info.Width, info.Height) / 520f;
        canvas.Save();
        canvas.Translate(info.Width * 0.5f, info.Height * 0.52f);
        canvas.Scale(baseScale);

        using var primary = CreateOutline(SelectedPrimaryShape.Shape);
        primary.ApplyTransform(CreateTransform(
            (float)PrimaryScale,
            (float)PrimaryRotation,
            (float)PrimaryOffsetX,
            (float)PrimaryOffsetY));

        DrawPrimary(canvas, primary);

        GeometrySnapshot? secondarySnapshot = null;
        var booleanSummary = SelectedPathOperation.Description;

        if (ShowSecondary)
        {
            using var secondary = CreateOutline(SelectedSecondaryShape.Shape);
            secondary.ApplyTransform(CreateTransform(
                (float)SecondaryScale,
                (float)SecondaryRotation,
                (float)SecondaryOffsetX,
                (float)SecondaryOffsetY));

            DrawSecondary(canvas, secondary);
            secondarySnapshot = new GeometrySnapshot(secondary.Path.TightBounds, secondary.CommandCount);

            if (ShowControlPoints)
            {
                DrawControlPoints(canvas, secondary, new SKColor(255, 160, 196, 200), new SKColor(255, 200, 220, 160));
            }

            if (ShowBooleanResult)
            {
                var option = SelectedPathOperation;
                if (option.Operation is SKPathOp op)
                {
                    try
                    {
                        using var booleanPath = primary.Path.Op(secondary.Path, op);
                        using var fill = new SKPaint
                        {
                            Style = SKPaintStyle.Fill,
                            Color = new SKColor(255, 255, 255, 36),
                            IsAntialias = true,
                        };
                        using var outline = new SKPaint
                        {
                            Style = SKPaintStyle.Stroke,
                            Color = new SKColor(255, 255, 255, 120),
                            StrokeWidth = 2f,
                            IsAntialias = true,
                        };

                        if (booleanPath is not null)
                        {
                            canvas.DrawPath(booleanPath, fill);
                            canvas.DrawPath(booleanPath, outline);
                            var bounds = booleanPath.TightBounds;
                            booleanSummary = $"{option.Name} • bounds {FormatRect(bounds)}";
                        }
                        else
                        {
                            booleanSummary = $"{option.Name} • operation returned no geometry.";
                        }
                    }
                    catch (NotImplementedException)
                    {
                        booleanSummary = "SKPath.Op is not implemented in the shim yet.";
                    }
                }
                else
                {
                    booleanSummary = option.Description;
                }
            }
            else
            {
                booleanSummary = "Boolean combine hidden.";
            }
        }
        else
        {
            booleanSummary = "Secondary shape hidden – boolean combine disabled.";
        }

        if (ShowControlPoints)
        {
            DrawControlPoints(canvas, primary, new SKColor(126, 240, 255, 220), new SKColor(160, 240, 255, 140));
        }

        if (ShowBounds)
        {
            DrawBounds(canvas, primary.Path, new SKColor(126, 220, 255, 60));
            if (secondarySnapshot.HasValue)
            {
                using var stroke = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    Color = new SKColor(255, 180, 220, 90),
                    StrokeWidth = 2f,
                    IsAntialias = true,
                };
                canvas.DrawRect(secondarySnapshot.Value.Bounds, stroke);
            }
        }

        canvas.Restore();

        var primarySnapshot = new GeometrySnapshot(primary.Path.TightBounds, primary.CommandCount);
        UpdateGeometrySummary(primarySnapshot, secondarySnapshot);
        BooleanSummary = booleanSummary;
        ProcessCapture(context);
    }

    private void DrawPrimary(SKCanvas canvas, GeometryOutline outline)
    {
        using var fill = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(-220, -200),
                new SKPoint(220, 200),
                new[]
                {
                    new SKColor(64, 174, 255, 240),
                    new SKColor(126, 255, 214, 230),
                },
                null,
                SKShaderTileMode.Clamp),
            IsAntialias = true,
        };

        using var stroke = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(10, 20, 28, 255),
            StrokeWidth = 3.5f,
            IsAntialias = true,
        };

        canvas.DrawPath(outline.Path, fill);
        canvas.DrawPath(outline.Path, stroke);
    }

    private void DrawSecondary(SKCanvas canvas, GeometryOutline outline)
    {
        using var fill = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = new SKColor(255, 168, 216, 180),
            IsAntialias = true,
        };

        using var stroke = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(255, 128, 188, 200),
            StrokeWidth = 2.5f,
            IsAntialias = true,
        };

        canvas.DrawPath(outline.Path, fill);
        canvas.DrawPath(outline.Path, stroke);
    }

    private static void DrawBounds(SKCanvas canvas, SKPath path, SKColor color)
    {
        var bounds = path.TightBounds;
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = color,
            StrokeWidth = 2f,
            IsAntialias = true,
        };

        canvas.DrawRect(bounds, paint);
    }

    private static void DrawControlPoints(SKCanvas canvas, GeometryOutline outline, SKColor anchorColor, SKColor handleColor)
    {
        using var linkPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(255, 255, 255, 70),
            StrokeWidth = 1.2f,
            IsAntialias = true,
        };

        using var anchorPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = anchorColor,
            IsAntialias = true,
        };

        using var handlePaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = handleColor,
            IsAntialias = true,
        };

        foreach (var handle in outline.ControlHandles)
        {
            DrawSimpleLine(canvas, handle.Anchor, handle.Handle, linkPaint);
            canvas.DrawCircle(handle.Handle, 3.2f, handlePaint);
        }

        foreach (var anchor in outline.Anchors)
        {
            canvas.DrawCircle(anchor, 4.0f, anchorPaint);
        }
    }

    private static void DrawSimpleLine(SKCanvas canvas, SKPoint start, SKPoint end, SKPaint paint)
    {
        using var path = new SKPath();
        path.MoveTo(start);
        path.LineTo(end);
        canvas.DrawPath(path, paint);
    }

    private static SKMatrix CreateTransform(float scale, float rotationDegrees, float offsetX, float offsetY)
    {
        var scaleMatrix = SKMatrix.CreateScale(scale, scale);
        var rotation = SKMatrix.CreateRotationDegrees(rotationDegrees, 0, 0);
        var translation = SKMatrix.CreateTranslation(offsetX, offsetY);
        return SKMatrix.Concat(translation, SKMatrix.Concat(rotation, scaleMatrix));
    }

    private static GeometryOutline CreateOutline(GeometryShape shape)
    {
        var path = new SKPath();
        var anchors = new List<SKPoint>();
        var handles = new List<ControlHandle>();
        var commandCount = 0;
        var current = new SKPoint();

        void MoveTo(SKPoint point)
        {
            path.MoveTo(point);
            anchors.Add(point);
            current = point;
            commandCount++;
        }

        void LineTo(SKPoint point)
        {
            path.LineTo(point);
            anchors.Add(point);
            current = point;
            commandCount++;
        }

        void CubicTo(SKPoint c1, SKPoint c2, SKPoint end)
        {
            path.CubicTo(c1, c2, end);
            handles.Add(new ControlHandle(current, c1));
            handles.Add(new ControlHandle(end, c2));
            anchors.Add(end);
            current = end;
            commandCount++;
        }

        switch (shape)
        {
            case GeometryShape.RoundedRectangle:
            {
                var rect = new SKRect(-220, -160, 220, 160);
                var radius = 70f;
                var k = CircleControlPoint * radius;

                MoveTo(new SKPoint(rect.Left + radius, rect.Top));
                LineTo(new SKPoint(rect.Right - radius, rect.Top));
                CubicTo(
                    new SKPoint(rect.Right - k, rect.Top),
                    new SKPoint(rect.Right, rect.Top + k),
                    new SKPoint(rect.Right, rect.Top + radius));
                LineTo(new SKPoint(rect.Right, rect.Bottom - radius));
                CubicTo(
                    new SKPoint(rect.Right, rect.Bottom - k),
                    new SKPoint(rect.Right - k, rect.Bottom),
                    new SKPoint(rect.Right - radius, rect.Bottom));
                LineTo(new SKPoint(rect.Left + radius, rect.Bottom));
                CubicTo(
                    new SKPoint(rect.Left + k, rect.Bottom),
                    new SKPoint(rect.Left, rect.Bottom - k),
                    new SKPoint(rect.Left, rect.Bottom - radius));
                LineTo(new SKPoint(rect.Left, rect.Top + radius));
                CubicTo(
                    new SKPoint(rect.Left, rect.Top + k),
                    new SKPoint(rect.Left + k, rect.Top),
                    new SKPoint(rect.Left + radius, rect.Top));
                break;
            }

            case GeometryShape.Clover:
            {
                MoveTo(new SKPoint(0, -220));
                CubicTo(
                    new SKPoint(-140, -220),
                    new SKPoint(-220, -140),
                    new SKPoint(-220, 0));
                CubicTo(
                    new SKPoint(-220, 120),
                    new SKPoint(-120, 220),
                    new SKPoint(0, 200));
                CubicTo(
                    new SKPoint(120, 220),
                    new SKPoint(220, 120),
                    new SKPoint(220, 0));
                CubicTo(
                    new SKPoint(220, -140),
                    new SKPoint(140, -220),
                    new SKPoint(0, -220));
                break;
            }

            case GeometryShape.Wave:
            {
                MoveTo(new SKPoint(-240, 0));
                CubicTo(
                    new SKPoint(-160, -180),
                    new SKPoint(-80, 180),
                    new SKPoint(0, 0));
                CubicTo(
                    new SKPoint(80, -180),
                    new SKPoint(160, 180),
                    new SKPoint(240, 0));
                CubicTo(
                    new SKPoint(160, 120),
                    new SKPoint(80, -120),
                    new SKPoint(0, 0));
                CubicTo(
                    new SKPoint(-80, 120),
                    new SKPoint(-160, -120),
                    new SKPoint(-240, 0));
                break;
            }

            case GeometryShape.Pentagon:
            {
                const int sides = 5;
                const float radius = 220f;
                for (var i = 0; i < sides; i++)
                {
                    var angle = (float)((-Math.PI / 2) + (i * 2 * Math.PI / sides));
                    var point = new SKPoint(
                        radius * MathF.Cos(angle),
                        radius * MathF.Sin(angle));
                    if (i == 0)
                    {
                        MoveTo(point);
                    }
                    else
                    {
                        LineTo(point);
                    }
                }

                break;
            }
        }

        path.Close();
        commandCount++;

        return new GeometryOutline(path, anchors, handles, commandCount);
    }

    private static double NormalizeAngle(double value)
    {
        var angle = value % 360.0;
        if (angle < 0)
        {
            angle += 360.0;
        }

        return angle;
    }

    private void UpdateGeometrySummary(GeometrySnapshot primary, GeometrySnapshot? secondary)
    {
        var summary = $"Primary • {SelectedPrimaryShape.Name} · commands {primary.CommandCount} · bounds {FormatRect(primary.Bounds)}";
        if (ShowSecondary && secondary.HasValue)
        {
            summary += $" | Secondary • {SelectedSecondaryShape.Name} · commands {secondary.Value.CommandCount} · bounds {FormatRect(secondary.Value.Bounds)}";
        }

        GeometrySummary = summary;
    }

    private static string FormatRect(SKRect rect)
    {
        return $"[{rect.Left:F0},{rect.Top:F0}]→[{rect.Width:F0}×{rect.Height:F0}]";
    }

    private sealed class GeometryOutline : IDisposable
    {
        public GeometryOutline(SKPath path, List<SKPoint> anchors, List<ControlHandle> controlHandles, int commandCount)
        {
            Path = path;
            Anchors = anchors;
            ControlHandles = controlHandles;
            CommandCount = commandCount;
        }

        public SKPath Path { get; }

        public List<SKPoint> Anchors { get; }

        public List<ControlHandle> ControlHandles { get; }

        public int CommandCount { get; }

        public void ApplyTransform(SKMatrix matrix)
        {
            Path.Transform(matrix);
            var m = matrix.ToMatrix3x2();

            for (var i = 0; i < Anchors.Count; i++)
            {
                Anchors[i] = TransformPoint(Anchors[i], m);
            }

            foreach (var handle in ControlHandles)
            {
                handle.Anchor = TransformPoint(handle.Anchor, m);
                handle.Handle = TransformPoint(handle.Handle, m);
            }
        }

        public void Dispose()
        {
            Path.Dispose();
        }
    }

    private sealed class ControlHandle
    {
        public ControlHandle(SKPoint anchor, SKPoint handle)
        {
            Anchor = anchor;
            Handle = handle;
        }

        public SKPoint Anchor { get; set; }

        public SKPoint Handle { get; set; }
    }

    private readonly record struct GeometrySnapshot(SKRect Bounds, int CommandCount);

    public sealed record PathOperationOption(string Name, SKPathOp? Operation, string Description);

    public sealed record GeometryShapeOption(string Name, GeometryShape Shape);

    public enum GeometryShape
    {
        RoundedRectangle,
        Clover,
        Wave,
        Pentagon,
    }

    private static SKPoint TransformPoint(SKPoint point, Matrix3x2 matrix)
    {
        var vector = Vector2.Transform(new Vector2(point.X, point.Y), matrix);
        return new SKPoint(vector.X, vector.Y);
    }
}
