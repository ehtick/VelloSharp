using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using VelloSharp;

namespace VelloSharp.Scenes;

public static class TestScenes
{
    public static IReadOnlyList<ExampleScene> BuildScenes(
        ImageCache images,
        SimpleText text,
        string? assetRoot,
        IList<IDisposable> resources)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(resources);

        var tigerScene = TryLoadTigerSvg(assetRoot, resources);
        var mmark = new MMarkScene();

        return new List<ExampleScene>
        {
            SplashWithTiger(tigerScene, text),
            FunkyPaths(),
            StrokeStyles(),
            StrokeStylesNonUniform(),
            StrokeStylesSkew(),
            Emoji(text),
            TrickyStrokes(),
            FillTypes(),
            CardioidAndFriends(),
            AnimatedText(images, text, assetRoot),
            GradientExtend(),
            TwoPointRadial(),
            BrushTransform(),
            BlendGrid(),
            DeepBlend(),
            ManyClips(),
            ConflationArtifacts(),
            Labyrinth(),
            RobustPaths(),
            BaseColorTest(),
            ClipTest(),
            LongPathDash("longpathdash (butt caps)", LineCap.Butt),
            LongPathDash("longpathdash (round caps)", LineCap.Round),
            MotionMark(mmark),
            ManyDrawObjects(),
            BlurredRoundedRect(),
            ImageSampling(images, assetRoot),
            ImageExtendModes("image_extend_modes (bilinear)", images, assetRoot, ImageQuality.Medium),
            ImageExtendModes("image_extend_modes (nearest neighbor)", images, assetRoot, ImageQuality.Low),
            LuminanceMask(images, assetRoot),
            ImageLuminanceMask(images, assetRoot),
        };
    }

    private static ExampleScene SplashWithTiger(VelloSvg? svg, SimpleText text)
        => new("splash_with_tiger", false, (scene, parameters) =>
        {
            if (svg is not null)
            {
                svg.Render(scene, parameters.ViewTransform);
                parameters.Resolution = svg.Size;
            }
            RenderSplashOverlay(scene, parameters, text);
        });

    private static VelloSvg? TryLoadTigerSvg(string? assetRoot, IList<IDisposable> resources)
    {
        if (string.IsNullOrWhiteSpace(assetRoot))
        {
            return null;
        }

        var path = Path.Combine(assetRoot, "Ghostscript_Tiger.svg");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var svg = VelloSvg.LoadFromFile(path);
            resources.Add(svg);
            return svg;
        }
        catch
        {
            return null;
        }
    }

    private static Matrix3x2 CreateSkewFactor(float skewX, float skewY)
        => new(1f, skewY, skewX, 1f, 0f, 0f);

    private static Matrix3x2 Compose(params Matrix3x2[] transforms)
    {
        var result = Matrix3x2.Identity;
        foreach (var transform in transforms)
        {
            result = Matrix3x2.Multiply(transform, result);
        }
        return result;
    }

    private static void RenderSplashOverlay(Scene scene, SceneParams parameters, SimpleText text)
    {
        var lines = new[]
        {
            "Vello test",
            "  Arrow keys: switch scenes",
            "  Space: reset transform",
            "  S: toggle stats",
            "  V: toggle vsync",
            "  M: cycle AA method",
            "  Q, E: rotate",
        };

        var baseTransform = Compose(Matrix3x2.CreateScale(0.11f), Matrix3x2.CreateTranslation(-90f, -50f));
        var view = Compose(baseTransform, parameters.ViewTransform);

        for (var i = 0; i < lines.Length; i++)
        {
            var size = i == 0 ? 60f : 40f;
            var lineTransform = Matrix3x2.CreateTranslation(100f, 100f + 60f * i);
            var finalTransform = Compose(lineTransform, view);
            text.Add(scene, size, new RgbaColor(1f, 1f, 1f, 1f), finalTransform, null, lines[i]);
        }
    }

    private static ExampleScene FunkyPaths()
        => new("funky_paths", false, (scene, parameters) =>
        {
            var view = parameters.ViewTransform;

            var missingMovetos = new PathBuilder()
                .MoveTo(0, 0)
                .LineTo(100, 100)
                .LineTo(100, 200)
                .Close()
                .LineTo(0, 400)
                .LineTo(100, 400);

            var onlyMovetos = new PathBuilder()
                .MoveTo(0, 0)
                .MoveTo(100, 100);

            var blue = RgbaColor.FromBytes(0, 0, 255);
            var aqua = RgbaColor.FromBytes(0, 255, 255);

            scene.FillPath(
                missingMovetos,
                FillRule.NonZero,
                Matrix3x2.CreateTranslation(100f, 100f) * view,
                blue);

            // The original test exercises empty path handling; the managed wrapper rejects empty paths,
            // so we simply skip issuing that draw call here.

            scene.FillPath(
                onlyMovetos,
                FillRule.NonZero,
                view,
                blue);

            var stroke = new StrokeStyle
            {
                Width = 8,
            };

            scene.StrokePath(
                missingMovetos,
                stroke,
                Matrix3x2.CreateTranslation(100f, 100f) * view,
                aqua);
        });

    private static ExampleScene StrokeStyles() => StrokeStyles("stroke_styles", Matrix3x2.Identity);

    private static ExampleScene StrokeStylesNonUniform()
        => StrokeStyles("stroke_styles (non-uniform scale)", Matrix3x2.CreateScale(1.2f, 0.7f));

    private static ExampleScene StrokeStylesSkew()
        => StrokeStyles("stroke_styles (skew)", CreateSkewFactor(1f, 0f));

    private static ExampleScene StrokeStyles(string name, Matrix3x2 strokeTransform)
        => new(name, false, (scene, parameters) =>
        {
            static Matrix3x2 ApplyView(Matrix3x2 transform, Matrix3x2 view)
                => Matrix3x2.Multiply(transform, view);

            static Vector2 GetTranslation(Matrix3x2 matrix) => new(matrix.M31, matrix.M32);

            var view = parameters.ViewTransform;

            var colors = new[]
            {
                RgbaColor.FromBytes(140, 181, 236),
                RgbaColor.FromBytes(246, 236, 202),
                RgbaColor.FromBytes(201, 147, 206),
                RgbaColor.FromBytes(150, 195, 160),
            };

            var simpleStroke = new PathBuilder().MoveTo(0, 0).LineTo(100, 0);
            var joinStroke = new PathBuilder()
                .MoveTo(0, 0)
                .CubicTo(20, 0, 42.5, 5, 50, 25)
                .CubicTo(57.5, 5, 80, 0, 100, 0);
            var miterStroke = new PathBuilder()
                .MoveTo(0, 0)
                .LineTo(90, 16)
                .LineTo(0, 31)
                .LineTo(90, 46);
            var closedStrokes = new PathBuilder()
                .MoveTo(0, 0)
                .LineTo(90, 21)
                .LineTo(0, 42)
                .Close()
                .MoveTo(200, 0)
                .CubicTo(100, 72, 300, 72, 200, 0)
                .Close()
                .MoveTo(290, 0)
                .CubicTo(200, 72, 400, 72, 310, 0)
                .Close();

            var capStyles = new[] { LineCap.Butt, LineCap.Square, LineCap.Round };
            var joinStyles = new[] { LineJoin.Bevel, LineJoin.Miter, LineJoin.Round };
            var miterLimits = new[] { 4.0, 6.0, 0.1, 10.0 };

            var capsBase = Compose(Matrix3x2.CreateTranslation(60f, 40f), Matrix3x2.CreateScale(2f));
            var dashedBase = Compose(Matrix3x2.CreateTranslation(450f, 0f), capsBase);
            var joinBase = Compose(Matrix3x2.CreateTranslation(550f, 0f), dashedBase);
            var miterBase = Compose(Matrix3x2.CreateTranslation(500f, 0f), joinBase);

            float y = 0f;
            float yMax = 0f;
            var colorIndex = 0;

            void DrawLabel(string text, Matrix3x2 sectionTransform, float offset)
            {
                var labelTransform = Compose(Matrix3x2.CreateTranslation(0f, offset), sectionTransform);
                parameters.Text.Add(scene, 12f, RgbaColor.FromBytes(255, 255, 255), ApplyView(labelTransform, view), null, text);
            }

            Matrix3x2 BuildStrokeTransform(Matrix3x2 sectionTransform, float offset)
            {
                var local = Compose(Matrix3x2.CreateTranslation(0f, offset), sectionTransform, strokeTransform);
                return ApplyView(local, view);
            }

            foreach (var start in capStyles)
            {
                foreach (var end in capStyles)
                {
                    DrawLabel($"Start cap: {start}, End cap: {end}", capsBase, y);

                    var stroke = new StrokeStyle
                    {
                        Width = 20,
                        StartCap = start,
                        EndCap = end,
                    };
                    scene.StrokePath(simpleStroke, stroke, BuildStrokeTransform(capsBase, y + 30f), colors[colorIndex]);

                    y += 180f;
                    colorIndex = (colorIndex + 1) % colors.Length;
                }
            }

            yMax = MathF.Max(yMax, y);
            y = 0f;

            foreach (var start in capStyles)
            {
                foreach (var end in capStyles)
                {
                    DrawLabel($"Dashing - Start cap: {start}, End cap: {end}", dashedBase, y);

                    var stroke = new StrokeStyle
                    {
                        Width = 20,
                        StartCap = start,
                        EndCap = end,
                        DashPattern = new[] { 10.0, 21.0 },
                    };
                    scene.StrokePath(simpleStroke, stroke, BuildStrokeTransform(dashedBase, y + 30f), colors[colorIndex]);

                    y += 180f;
                    colorIndex = (colorIndex + 1) % colors.Length;
                }
            }

            yMax = MathF.Max(yMax, y);
            y = 0f;

            foreach (var cap in capStyles)
            {
                foreach (var join in joinStyles)
                {
                    DrawLabel($"Caps: {cap}, Joins: {join}", joinBase, y);

                    var stroke = new StrokeStyle
                    {
                        Width = 20,
                        StartCap = cap,
                        EndCap = cap,
                        LineJoin = join,
                    };
                    scene.StrokePath(joinStroke, stroke, BuildStrokeTransform(joinBase, y + 30f), colors[colorIndex]);

                    y += 185f;
                    colorIndex = (colorIndex + 1) % colors.Length;
                }
            }

            yMax = MathF.Max(yMax, y);
            y = 0f;

            foreach (var limit in miterLimits)
            {
                DrawLabel($"Miter limit: {limit:0.###}", miterBase, y);

                var stroke = new StrokeStyle
                {
                    Width = 10,
                    LineJoin = LineJoin.Miter,
                    StartCap = LineCap.Butt,
                    EndCap = LineCap.Butt,
                    MiterLimit = limit,
                };
                scene.StrokePath(miterStroke, stroke, BuildStrokeTransform(miterBase, y + 30f), colors[colorIndex]);

                y += 180f;
                colorIndex = (colorIndex + 1) % colors.Length;
            }

            for (var i = 0; i < joinStyles.Length; i++)
            {
                var join = joinStyles[i];
                var cap = capStyles[i];

                DrawLabel($"Closed path with join: {join}", miterBase, y);

                var stroke = new StrokeStyle
                {
                    Width = 10,
                    LineJoin = join,
                    StartCap = cap,
                    EndCap = cap,
                    MiterLimit = 5,
                };
                scene.StrokePath(closedStrokes, stroke, BuildStrokeTransform(miterBase, y + 30f), colors[colorIndex]);

                y += 180f;
                colorIndex = (colorIndex + 1) % colors.Length;
            }

            yMax = MathF.Max(yMax, y);

            var translation = GetTranslation(miterBase);
            var xMax = translation.X + 400f * 2f + 50f;
            parameters.Resolution = new Vector2(xMax, yMax);
        });

    private static ExampleScene Emoji(SimpleText text)
        => new("emoji", true, (scene, parameters) =>
        {
            var t = (float)(parameters.Time * 2.0);
            var textSize = 120f + 20f * MathF.Sin(t);

            var colrTransform = Matrix3x2.CreateTranslation(100f, 250f) * parameters.ViewTransform;
            text.AddColrEmojiRun(scene, textSize, colrTransform, null, "🎉🤠✅");

            var bitmapTransform = Matrix3x2.CreateTranslation(100f, 500f) * parameters.ViewTransform;
            text.AddBitmapEmojiRun(scene, textSize, bitmapTransform, null, "🎉🤠✅");
        });

    private static ExampleScene TrickyStrokes()
        => new("tricky_strokes", false, (scene, parameters) =>
        {
            var view = parameters.ViewTransform;
            var colors = new[]
            {
                RgbaColor.FromBytes(140, 181, 236),
                RgbaColor.FromBytes(246, 236, 202),
                RgbaColor.FromBytes(201, 147, 206),
                RgbaColor.FromBytes(150, 195, 160),
            };

            const double CellSize = 200.0;
            const double StrokeWidth = 30.0;
            const int Columns = 5;

            var trickyCubics = new (double X, double Y)[][]
            {
                new[] { (122.0, 737.0), (348.0, 553.0), (403.0, 761.0), (400.0, 760.0) },
                new[] { (244.0, 520.0), (244.0, 518.0), (1141.0, 634.0), (394.0, 688.0) },
                new[] { (550.0, 194.0), (138.0, 130.0), (1035.0, 246.0), (288.0, 300.0) },
                new[] { (226.0, 733.0), (556.0, 779.0), (-43.0, 471.0), (348.0, 683.0) },
                new[] { (268.0, 204.0), (492.0, 304.0), (352.0, 23.0), (433.0, 412.0) },
                new[] { (172.0, 480.0), (396.0, 580.0), (256.0, 299.0), (338.0, 677.0) },
                new[] { (731.0, 340.0), (318.0, 252.0), (1026.0, -64.0), (367.0, 265.0) },
                new[] { (475.0, 708.0), (62.0, 620.0), (770.0, 304.0), (220.0, 659.0) },
                new[] { (0.0, 0.0), (128.0, 128.0), (128.0, 0.0), (0.0, 128.0) },
                new[] { (0.0, 0.01), (128.0, 127.999), (128.0, 0.01), (0.0, 127.99) },
                new[] { (0.0, -0.01), (128.0, 128.001), (128.0, -0.01), (0.0, 128.001) },
                new[] { (0.0, 0.0), (0.0, -10.0), (0.0, -10.0), (0.0, 10.0) },
                new[] { (10.0, 0.0), (0.0, 0.0), (20.0, 0.0), (10.0, 0.0) },
                new[] { (39.0, -39.0), (40.0, -40.0), (40.0, -40.0), (0.0, 0.0) },
                new[] { (40.0, 40.0), (0.0, 0.0), (200.0, 200.0), (0.0, 0.0) },
                new[] { (0.0, 0.0), (0.01, 0.0), (-0.01, 0.0), (0.0, 0.0) },
                new[] { (400.75, 100.05), (400.75, 100.05), (100.05, 300.95), (100.05, 300.95) },
                new[] { (0.5, 0.0), (0.0, 0.0), (20.0, 0.0), (10.0, 0.0) },
                new[] { (10.0, 0.0), (0.0, 0.0), (10.0, 0.0), (10.0, 0.0) },
            };

            var flatQuad = new ((double X, double Y) Control, (double X, double Y) End)[]
            {
                ((2.0, 1.0), (1.0, 1.0)),
            };

            var flatConicAsQuads = new ((double X, double Y) Control, (double X, double Y) End)[]
            {
                ((2.232486, 1.0), (3.47174, 1.0)),
                ((4.710995, 1.0), (5.949262, 1.0)),
                ((7.18753, 1.0), (8.417061, 1.0)),
                ((9.646591, 1.0), (10.85969, 1.0)),
                ((12.072789, 1.0), (13.261865, 1.0)),
                ((14.45094, 1.0), (15.608549, 1.0)),
                ((16.766161, 1.0), (17.885059, 1.0)),
                ((19.003958, 1.0), (20.077141, 1.0)),
                ((21.150328, 1.0), (22.171083, 1.0)),
                ((23.191839, 1.0), (24.153776, 1.0)),
                ((25.115715, 1.0), (26.012812, 1.0)),
                ((26.909912, 1.0), (27.736557, 1.0)),
                ((28.563202, 1.0), (29.31422, 1.0)),
                ((30.065239, 1.0), (30.735928, 1.0)),
                ((31.40662, 1.0), (31.992788, 1.0)),
                ((32.578957, 1.0), (33.076927, 1.0)),
                ((33.574905, 1.0), (33.981567, 1.0)),
                ((34.388233, 1.0), (34.701038, 1.0)),
                ((35.013851, 1.0), (35.23085, 1.0)),
                ((35.447845, 1.0), (35.567669, 1.0)),
                ((35.6875, 1.0), (35.709404, 1.0)),
                ((35.731312, 1.0), (35.655155, 1.0)),
                ((35.579006, 1.0), (35.405273, 1.0)),
                ((35.231541, 1.0), (34.961311, 1.0)),
                ((34.691086, 1.0), (34.326057, 1.0)),
                ((33.961029, 1.0), (33.503479, 1.0)),
                ((33.045937, 1.0), (32.498734, 1.0)),
                ((31.95153, 1.0), (31.318098, 1.0)),
                ((30.684669, 1.0), (29.968971, 1.0)),
                ((29.253277, 1.0), (28.459791, 1.0)),
                ((27.666309, 1.0), (26.800005, 1.0)),
                ((25.933704, 1.0), (25.0, 1.0)),
            };

            var biggerFlatConicAsQuads = new ((double X, double Y) Control, (double X, double Y) End)[]
            {
                ((8.979845, 1.0), (15.795975, 1.0)),
                ((22.612104, 1.0), (28.363287, 1.0)),
                ((34.114471, 1.0), (38.884045, 1.0)),
                ((43.653618, 1.0), (47.510696, 1.0)),
                ((51.367767, 1.0), (54.368233, 1.0)),
                ((57.368698, 1.0), (59.55603, 1.0)),
                ((61.743366, 1.0), (63.149269, 1.0)),
                ((64.555168, 1.0), (65.200005, 1.0)),
                ((65.844841, 1.0), (65.737961, 1.0)),
                ((65.631073, 1.0), (64.770912, 1.0)),
                ((63.910763, 1.0), (62.284878, 1.0)),
                ((60.658997, 1.0), (58.243816, 1.0)),
                ((55.82864, 1.0), (52.589172, 1.0)),
                ((49.349705, 1.0), (45.239006, 1.0)),
                ((41.128315, 1.0), (36.086826, 1.0)),
                ((31.045338, 1.0), (25.0, 1.0)),
            };

            var flatCurves = new[] { flatQuad, flatConicAsQuads, biggerFlatConicAsQuads };

            var totalCurves = trickyCubics.Length + flatCurves.Length;
            var rows = (int)Math.Ceiling(totalCurves / (double)Columns);
            parameters.Resolution = new Vector2((float)(CellSize * Columns), (float)(CellSize * rows));

            var colorIndex = 0;
            var curveIndex = 0;

            foreach (var cubic in trickyCubics)
            {
                using var path = new KurboPath();
                path.MoveTo(cubic[0].X, cubic[0].Y);
                path.CubicTo(cubic[1].X, cubic[1].Y, cubic[2].X, cubic[2].Y, cubic[3].X, cubic[3].Y);

                DrawPath(path, ref curveIndex, ref colorIndex);
            }

            foreach (var segments in flatCurves)
            {
                using var path = new KurboPath();
                path.MoveTo(1.0, 1.0);
                foreach (var segment in segments)
                {
                    path.QuadraticTo(segment.Control.X, segment.Control.Y, segment.End.X, segment.End.Y);
                }

                DrawPath(path, ref curveIndex, ref colorIndex);
            }

            void DrawPath(KurboPath path, ref int index, ref int colorIdx)
            {
                var bounds = Inflate(path.GetBounds(), StrokeWidth);
                var cellX = (index % Columns) * CellSize;
                var cellY = (index / Columns) * CellSize;
                var (transform, scale) = MapRectToCell(bounds, cellX, cellY, CellSize);
                var stroke = new StrokeStyle
                {
                    Width = StrokeWidth / Math.Max(scale, 1e-6),
                    LineJoin = LineJoin.Miter,
                    StartCap = LineCap.Butt,
                    EndCap = LineCap.Butt,
                };
                var color = colors[colorIdx];
                scene.StrokePath(path, stroke, transform * view, new SolidColorBrush(color));

                index++;
                colorIdx = (colorIdx + 1) % colors.Length;
            }

            static KurboRect Inflate(KurboRect rect, double amount)
            {
                return new KurboRect(rect.X0 - amount, rect.Y0 - amount, rect.X1 + amount, rect.Y1 + amount);
            }

            static (Matrix3x2 Transform, double Scale) MapRectToCell(KurboRect rect, double cellX, double cellY, double cellSize)
            {
                var width = rect.X1 - rect.X0;
                var height = rect.Y1 - rect.Y0;
                if (width <= 0.0 || height <= 0.0)
                {
                    return (Matrix3x2.CreateTranslation((float)cellX, (float)cellY), 1.0);
                }

                var scaleX = cellSize / width;
                var scaleY = cellSize / height;
                var scale = Math.Min(scaleX, scaleY);

                var tx = cellX - rect.X0 * scale;
                var ty = cellY - rect.Y0 * scale;
                if (scaleX > scaleY)
                {
                    tx += (cellSize - width * scale) * 0.5;
                }
                else
                {
                    ty += (cellSize - height * scale) * 0.5;
                }

                var transform = Matrix3x2.Multiply(Matrix3x2.CreateScale((float)scale), Matrix3x2.CreateTranslation((float)tx, (float)ty));
                return (transform, scale);
            }
        });

    private static ExampleScene FillTypes()
        => new("fill_types", false, (scene, parameters) =>
        {
            var view = parameters.ViewTransform;
            parameters.Resolution = new Vector2(1400f, 700f);

            var rect = CreateRectanglePath(0f, 0f, 500f, 500f);

            var star = new PathBuilder()
                .MoveTo(250, 0)
                .LineTo(105, 450)
                .LineTo(490, 175)
                .LineTo(10, 175)
                .LineTo(395, 450)
                .Close();

            var arcs = new PathBuilder()
                .MoveTo(0, 480)
                .CubicTo(500, 480, 500, -10, 0, -10)
                .Close()
                .MoveTo(500, -10)
                .CubicTo(0, -10, 0, 480, 500, 480)
                .Close();

            var rules = new (FillRule Rule, string Label, PathBuilder Path)[]
            {
                (FillRule.NonZero, "Non-Zero", star),
                (FillRule.EvenOdd, "Even-Odd", star),
                (FillRule.NonZero, "Non-Zero", arcs),
                (FillRule.EvenOdd, "Even-Odd", arcs),
            };

            var gray = RgbaColor.FromBytes(0x80, 0x80, 0x80);
            var yellow = RgbaColor.FromBytes(0xFF, 0xFF, 0x00);
            var white = RgbaColor.FromBytes(0xFF, 0xFF, 0xFF);
            var overlayA = new RgbaColor(0f, 1f, 0.7f, 0.6f);
            var overlayB = new RgbaColor(0.9f, 0.7f, 0.5f, 0.6f);

            var scale = Matrix3x2.CreateScale(0.6f);
            var baseTransform = Matrix3x2.CreateTranslation(10f, 25f);

            for (var i = 0; i < rules.Length; i++)
            {
                var column = i % 2;
                var row = i / 2;
                var blockTransform = Matrix3x2.Multiply(baseTransform, Matrix3x2.CreateTranslation(column * 306f, row * 340f));

                parameters.Text.Add(scene, 24f, white, Matrix3x2.Multiply(blockTransform, view), null, rules[i].Label);

                var fillBase = Matrix3x2.Multiply(Matrix3x2.Multiply(scale, blockTransform), Matrix3x2.CreateTranslation(0f, 5f));
                scene.FillPath(rect, FillRule.NonZero, Matrix3x2.Multiply(fillBase, view), gray);

                var primary = Matrix3x2.Multiply(fillBase, Matrix3x2.CreateTranslation(0f, 10f));
                scene.FillPath(rules[i].Path, rules[i].Rule, Matrix3x2.Multiply(primary, view), yellow);
            }

            var blendBase = Matrix3x2.Multiply(baseTransform, Matrix3x2.CreateTranslation(700f, 0f));
            for (var i = 0; i < rules.Length; i++)
            {
                var column = i % 2;
                var row = i / 2;
                var blockTransform = Matrix3x2.Multiply(blendBase, Matrix3x2.CreateTranslation(column * 306f, row * 340f));

                parameters.Text.Add(scene, 24f, white, Matrix3x2.Multiply(blockTransform, view), null, rules[i].Label);

                var fillBase = Matrix3x2.Multiply(Matrix3x2.Multiply(scale, blockTransform), Matrix3x2.CreateTranslation(0f, 5f));
                scene.FillPath(rect, FillRule.NonZero, Matrix3x2.Multiply(fillBase, view), gray);

                var translated = Matrix3x2.Multiply(fillBase, Matrix3x2.CreateTranslation(0f, 10f));
                var rotatedA = Matrix3x2.Multiply(Matrix3x2.CreateRotation(0.06f), translated);
                var rotatedB = Matrix3x2.Multiply(Matrix3x2.CreateRotation(-0.06f), translated);

                scene.FillPath(rules[i].Path, rules[i].Rule, Matrix3x2.Multiply(translated, view), yellow);
                scene.FillPath(rules[i].Path, rules[i].Rule, Matrix3x2.Multiply(rotatedA, view), overlayA);
                scene.FillPath(rules[i].Path, rules[i].Rule, Matrix3x2.Multiply(rotatedB, view), overlayB);
            }
        });

    private static ExampleScene CardioidAndFriends()
        => new("cardioid_and_friends", false, (scene, parameters) =>
        {
            var view = parameters.ViewTransform;
            RenderCardioid(scene, view);
            RenderClipTest(scene, view);
            RenderAlphaTest(scene, view);
        });

    private static ExampleScene AnimatedText(ImageCache images, SimpleText text, string? assetRoot)
        => new("animated_text", true, (scene, parameters) =>
        {
            var view = parameters.ViewTransform;

            var background = CreateRectanglePath(0f, 0f, 1000f, 1000f);
            scene.FillPath(background, FillRule.NonZero, view, RgbaColor.FromBytes(0x80, 0x80, 0x80));

            var baseText = "\U0001F600hello Vello text!";
            var textSize = 60f + 40f * MathF.Sin((float)parameters.Time);
            text.Add(scene, textSize, RgbaColor.FromBytes(0xF0, 0xF0, 0xF0), Matrix3x2.CreateTranslation(110f, 600f) * view, null, baseText);

            var stroke = new StrokeStyle
            {
                Width = 1.0,
                LineJoin = LineJoin.Bevel,
                StartCap = LineCap.Butt,
                EndCap = LineCap.Butt,
                DashPattern = new[] { 1.0, 1.0 },
            };
            var skew = Matrix3x2.CreateSkew((float)Math.Tan(20 * Math.PI / 180), 0f);
            text.AddRun(scene, textSize, RgbaColor.FromBytes(0xFF, 0xFF, 0xFF), Matrix3x2.CreateTranslation(110f, 700f) * view, skew, stroke, baseText);

            var anim = (float)(Math.Sin(parameters.Time) * 0.5 + 0.5);
            var weight = anim * 700f + 200f;
            var width = anim * 150f + 50f;
            var variations = new (string Axis, float Value)[] { ("wght", weight), ("wdth", width) };
            text.AddVarRun(
                scene,
                72f,
                variations,
                RgbaColor.FromBytes(0xFF, 0xFF, 0xFF),
                Matrix3x2.CreateTranslation(110f, 800f) * view,
                null,
                GlyphRunStyle.Fill,
                "And some Vello\ntext with a newline",
                hint: false);

            var center = new Vector2(500f, 500f);
            var endpoint = center + new Vector2(
                400f * (float)Math.Cos(parameters.Time),
                400f * (float)Math.Sin(parameters.Time));
            var line = new PathBuilder();
            line.MoveTo(center.X, center.Y).LineTo(endpoint.X, endpoint.Y);
            scene.StrokePath(line, new StrokeStyle { Width = 5.0 }, view, RgbaColor.FromBytes(0x80, 0x00, 0x00));

            scene.FillPath(CreateRectanglePath(0f, 0f, 1000f, 1000f), FillRule.NonZero,
                Matrix3x2.CreateScale(0.2f) * Matrix3x2.CreateTranslation(150f, 150f) * view,
                RgbaColor.FromBytes(0xFF, 0x00, 0x00));

            var alpha = (float)(Math.Sin(parameters.Time) * 0.5 + 0.5);
            var clip = CreateRectanglePath(0f, 0f, 1000f, 1000f);
            scene.PushLayer(clip, new LayerBlend(LayerMix.Normal, LayerCompose.SrcOver), view, alpha);
            scene.FillPath(CreateRectanglePath(0f, 0f, 1000f, 1000f), FillRule.NonZero,
                Matrix3x2.CreateScale(0.2f) * Matrix3x2.CreateTranslation(100f, 100f) * view,
                RgbaColor.FromBytes(0x00, 0x00, 0xFF));
            scene.FillPath(CreateRectanglePath(0f, 0f, 1000f, 1000f), FillRule.NonZero,
                Matrix3x2.CreateScale(0.2f) * Matrix3x2.CreateTranslation(200f, 200f) * view,
                RgbaColor.FromBytes(0x00, 0x80, 0x00));
            scene.PopLayer();

            scene.FillPath(CreateStarPath(), FillRule.NonZero, Matrix3x2.CreateTranslation(400f, 100f) * view, RgbaColor.FromBytes(0x80, 0x00, 0x80));
            scene.FillPath(CreateStarPath(), FillRule.EvenOdd, Matrix3x2.CreateTranslation(500f, 100f) * view, RgbaColor.FromBytes(0x80, 0x00, 0x80));

            var alphaValue = (float)((Math.Sin(parameters.Time * 0.5 + 200.0) + 1.0) * 0.5);
            try
            {
                var image = images.GetFromPath("splash-flower.jpg");
                var brush = new ImageBrush(image) { Alpha = alphaValue };
                var imageTransform = Matrix3x2.CreateRotation((float)(20 * Math.PI / 180)) * Matrix3x2.CreateTranslation(800f, 50f) * view;
                scene.DrawImage(brush, imageTransform);
            }
            catch (IOException)
            {
                // Ignore missing image assets.
            }
        });

    private static ExampleScene GradientExtend()
        => new("gradient_extend", false, (scene, parameters) =>
        {
            var transform = parameters.ViewTransform;
            var builder = new PathBuilder();
            builder.MoveTo(100, 100).LineTo(540, 100).LineTo(540, 360).LineTo(100, 360).Close();
            var gradient = new LinearGradientBrush(new Vector2(100, 100), new Vector2(540, 360), new[]
            {
                new GradientStop(0f, RgbaColor.FromBytes(255, 80, 120)),
                new GradientStop(1f, RgbaColor.FromBytes(80, 180, 255)),
            }, ExtendMode.Reflect);
            scene.FillPath(builder, FillRule.NonZero, transform, gradient);
        });

    private static ExampleScene TwoPointRadial()
        => new("two_point_radial", false, (scene, parameters) =>
        {
            var transform = parameters.ViewTransform;
            var gradient = new RadialGradientBrush(
                new Vector2(220, 200),
                40,
                new Vector2(360, 260),
                220,
                new[]
                {
                    new GradientStop(0f, RgbaColor.FromBytes(255, 255, 255)),
                    new GradientStop(1f, RgbaColor.FromBytes(40, 40, 60)),
                },
                ExtendMode.Reflect);
            var builder = new PathBuilder();
            builder.MoveTo(60, 80).LineTo(540, 120).LineTo(500, 420).LineTo(40, 380).Close();
            scene.FillPath(builder, FillRule.NonZero, transform, gradient);
        });

    private static ExampleScene BrushTransform()
        => new("brush_transform", true, (scene, parameters) =>
        {
            var baseTransform = Matrix3x2.CreateTranslation(120, 120) * parameters.ViewTransform;
            var gradient = new LinearGradientBrush(new Vector2(0, 0), new Vector2(400, 0), new[]
            {
                new GradientStop(0f, RgbaColor.FromBytes(255, 200, 80)),
                new GradientStop(1f, RgbaColor.FromBytes(80, 120, 255)),
            });
            var builder = new PathBuilder();
            builder.MoveTo(0, 0).LineTo(400, 0).LineTo(400, 200).LineTo(0, 200).Close();
            var angle = (float)(parameters.Time * 0.5);
            var brushTransform = Matrix3x2.CreateRotation(angle, new Vector2(200, 100));
            scene.FillPath(builder, FillRule.NonZero, baseTransform, gradient, brushTransform);
        });

    private static ExampleScene BlendGrid()
        => new("blend_grid", false, (scene, parameters) =>
        {
            var transform = parameters.ViewTransform;
            var builder = new PathBuilder();
            var colors = new[]
            {
                RgbaColor.FromBytes(255, 120, 120, 200),
                RgbaColor.FromBytes(120, 220, 255, 200),
                RgbaColor.FromBytes(180, 255, 140, 200),
            };
            for (var y = 0; y < 4; y++)
            {
                for (var x = 0; x < 4; x++)
                {
                    builder.Clear();
                    var left = 80 + x * 140;
                    var top = 80 + y * 140;
                    builder.MoveTo(left, top)
                        .LineTo(left + 120, top)
                        .LineTo(left + 120, top + 120)
                        .LineTo(left, top + 120)
                        .Close();
                    scene.FillPath(builder, FillRule.NonZero, transform, colors[(x + y) % colors.Length]);
                }
            }
        });

    private static ExampleScene DeepBlend()
        => new("deep_blend", false, (scene, parameters) =>
        {
            var transform = parameters.ViewTransform;
            var builder = new PathBuilder();
            var rnd = new Random(42);
            var blend = new LayerBlend(LayerMix.Screen, LayerCompose.SrcOver);
            for (var i = 0; i < 8; i++)
            {
                builder.Clear();
                var center = new Vector2(200 + rnd.Next(400), 180 + rnd.Next(260));
                var radius = 40 + rnd.Next(120);
                const int samples = 64;
                for (var j = 0; j <= samples; j++)
                {
                    var t = j / (float)samples;
                    var theta = t * MathF.Tau;
                    var point = center + new Vector2(MathF.Cos(theta), MathF.Sin(theta)) * radius;
                    if (j == 0)
                    {
                        builder.MoveTo(point.X, point.Y);
                    }
                    else
                    {
                        builder.LineTo(point.X, point.Y);
                    }
                }
                builder.Close();
                scene.PushLayer(builder, blend, transform, alpha: 0.5f);
                var color = RgbaColor.FromBytes((byte)rnd.Next(40, 255), (byte)rnd.Next(40, 255), (byte)rnd.Next(40, 255), 120);
                scene.FillPath(builder, FillRule.NonZero, transform, color);
                scene.PopLayer();
            }
        });

    private static ExampleScene ManyClips()
        => new("many_clips", false, (scene, parameters) =>
        {
            var transform = parameters.ViewTransform;
            var builder = new PathBuilder();
            for (var i = 0; i < 6; i++)
            {
                builder.Clear();
                builder.MoveTo(120 + i * 60, 100)
                    .LineTo(160 + i * 60, 140)
                    .LineTo(120 + i * 60, 180)
                    .Close();
                scene.PushLayer(builder, new LayerBlend(LayerMix.Normal, LayerCompose.SrcOver), transform, alpha: 1f);
            }

            builder.Clear();
            builder.MoveTo(140, 140).LineTo(500, 140).LineTo(500, 360).LineTo(140, 360).Close();
            scene.FillPath(builder, FillRule.NonZero, transform, RgbaColor.FromBytes(120, 200, 255, 160));

            for (var i = 0; i < 6; i++)
            {
                scene.PopLayer();
            }
        });

    private static ExampleScene ConflationArtifacts()
        => new("conflation_artifacts", false, (scene, parameters) =>
        {
            var transform = parameters.ViewTransform;
            var builder = new PathBuilder();
            builder.MoveTo(200, 120).LineTo(440, 120).LineTo(440, 340).LineTo(200, 340).Close();
            scene.FillPath(builder, FillRule.NonZero, transform, RgbaColor.FromBytes(255, 255, 255));
            builder.Clear();
            builder.MoveTo(200, 120).LineTo(440, 340).Close();
            var stroke = new StrokeStyle { Width = 48, LineJoin = LineJoin.Bevel };
            scene.StrokePath(builder, stroke, transform, RgbaColor.FromBytes(50, 50, 50));
        });

    private static ExampleScene Labyrinth()
        => new("labyrinth", false, (scene, parameters) =>
        {
            var transform = parameters.ViewTransform;
            var builder = new PathBuilder();
            var rnd = new Random(7);
            var stroke = new StrokeStyle { Width = 6, LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            for (var i = 0; i < 160; i++)
            {
                builder.Clear();
                var start = new Vector2(rnd.Next(40, 760), rnd.Next(40, 560));
                var angle = rnd.NextDouble() * Math.PI * 2;
                var length = rnd.Next(40, 120);
                var end = start + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * length;
                builder.MoveTo(start.X, start.Y).LineTo(end.X, end.Y);
                var color = RgbaColor.FromBytes((byte)rnd.Next(100, 255), (byte)rnd.Next(100, 255), (byte)rnd.Next(100, 255));
                scene.StrokePath(builder, stroke, transform, color);
            }
        });

    private static ExampleScene RobustPaths()
        => new("robust_paths", false, (scene, parameters) =>
        {
            var transform = parameters.ViewTransform;
            var builder = new PathBuilder();
            builder.MoveTo(120, 120);
            for (int i = 0; i < 100; i++)
            {
                var angle = i * (MathF.PI / 12f);
                var radius = 60 + i * 4;
                var position = new Vector2(320 + MathF.Cos(angle) * radius, 320 + MathF.Sin(angle) * radius);
                builder.LineTo(position.X, position.Y);
            }
            var stroke = new StrokeStyle { Width = 8, LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            scene.StrokePath(builder, stroke, transform, RgbaColor.FromBytes(90, 220, 110));
        });

    private static ExampleScene BaseColorTest()
        => new("base_color_test", true, (scene, parameters) =>
        {
            var t = (float)(Math.Sin(parameters.Time) * 0.5 + 0.5);
            parameters.BaseColor = new RgbaColor(0.05f + 0.1f * t, 0.05f, 0.15f, 1f);
            var transform = parameters.ViewTransform;
            var builder = new PathBuilder();
            builder.MoveTo(160, 160).LineTo(480, 160).LineTo(480, 360).LineTo(160, 360).Close();
            scene.FillPath(builder, FillRule.NonZero, transform, RgbaColor.FromBytes(240, 200, 120));
        });

    private static ExampleScene ClipTest()
        => new("clip_test", true, (scene, parameters) =>
        {
            var view = parameters.ViewTransform;

            const float X0 = 50f;
            const float Y0 = 0f;
            const float X1 = 200f;
            const float Y1 = 500f;

            var clip = new PathBuilder();
            clip.MoveTo(X0, Y0)
                .LineTo(X1, Y0)
                .LineTo(X1, Y0 + (Y1 - Y0))
                .LineTo(X1 + (X0 - X1), Y1)
                .LineTo(X0, Y1)
                .Close();
            scene.PushLayer(clip, new LayerBlend(LayerMix.Clip, LayerCompose.SrcOver), view, 1f);

            var textSize = 60f + 40f * MathF.Sin((float)parameters.Time);
            parameters.Text.Add(
                scene,
                textSize,
                RgbaColor.FromBytes(255, 255, 255),
                Matrix3x2.CreateTranslation(110f, 100f) * view,
                null,
                "Some clipped text!");

            scene.PopLayer();

            const double Scale = 2.0;
            var clipRectPath = CreateRectanglePath(0f, 0f, 74.4f, 339.20001f);
            var clipTransform = new Matrix3x2((float)Scale, 0f, 0f, (float)Scale, 27.074707f, 176.4066f);
            scene.PushLayer(clipRectPath, new LayerBlend(LayerMix.Normal, LayerCompose.SrcOver), clipTransform * view, 1f);

            scene.FillPath(
                CreateRectanglePath(-1000f, -1000f, 3000f, 3000f),
                FillRule.NonZero,
                clipTransform * view,
                RgbaColor.FromBytes(0, 0, 255));

            var insideTransform = new Matrix3x2((float)Scale, 0f, 0f, (float)Scale, 29.027637f, 182.97555f);
            scene.FillPath(
                CreateRectanglePath(11f, 13.4f, 48f, 43.2f),
                FillRule.NonZero,
                insideTransform * view,
                RgbaColor.FromBytes(0, 255, 0));

            var outsideTransform = new Matrix3x2((float)Scale, 0f, 0f, (float)Scale, 29.027637f, (float)(Scale * 559.3583631427786));
            scene.FillPath(
                CreateRectanglePath(12.6f, 12.6f, 44.800003f, 44.800003f),
                FillRule.NonZero,
                outsideTransform * view,
                RgbaColor.FromBytes(255, 0, 0));

            scene.PopLayer();
        });

    private static ExampleScene LongPathDash(string name, LineCap cap)
        => new(name, false, (scene, parameters) =>
        {
            var transform = parameters.ViewTransform;
            var builder = new PathBuilder();
            builder.MoveTo(80, 200);
            for (var i = 1; i <= 200; i++)
            {
                builder.LineTo(80 + i * 6, 200 + 40 * Math.Sin(i * 0.1));
            }
            var stroke = new StrokeStyle
            {
                Width = 8,
                StartCap = cap,
                EndCap = cap,
                DashPattern = new[] { 20.0, 10.0 },
            };
            scene.StrokePath(builder, stroke, transform, RgbaColor.FromBytes(255, 220, 120));
        });

    private static ExampleScene MotionMark(MMarkScene scene) => new("mmark", true, scene.Render);


    private static ExampleScene ManyDrawObjects()
        => new("many_draw_objects", false, (scene, parameters) =>
        {
            var transform = parameters.ViewTransform;
            var rng = new Random(10);
            var builder = new PathBuilder();
            for (var i = 0; i < 500; i++)
            {
                builder.Clear();
                var x = rng.Next(40, 760);
                var y = rng.Next(40, 560);
                var w = rng.Next(20, 160);
                var h = rng.Next(20, 160);
                builder.MoveTo(x, y).LineTo(x + w, y).LineTo(x + w, y + h).LineTo(x, y + h).Close();
                scene.FillPath(builder, FillRule.NonZero, transform, RandomColor(rng, 0.8f));
            }
        });

    private static ExampleScene BlurredRoundedRect()
        => new("blurred_rounded_rect", false, (scene, parameters) =>
        {
            var transform = parameters.ViewTransform;
            scene.DrawBlurredRoundedRect(new Vector2(220, 180), new Vector2(320, 220), transform, RgbaColor.FromBytes(255, 200, 120), 30, 16);
        });

    private static ExampleScene ImageSampling(ImageCache images, string? assetRoot)
        => new("image_sampling", false, (scene, parameters) =>
        {
            var transform = parameters.ViewTransform;
            var image = images.GetFromPath("splash-flower.jpg");
            var brush = new ImageBrush(image)
            {
                Quality = ImageQuality.High,
            };
            scene.DrawImage(brush, Matrix3x2.CreateScale(0.7f) * Matrix3x2.CreateTranslation(120, 120) * transform);
        });

    private static ExampleScene ImageExtendModes(string name, ImageCache images, string? assetRoot, ImageQuality quality)
        => new(name, false, (scene, parameters) =>
        {
            var transform = parameters.ViewTransform;
            var image = images.GetFromPath("splash-flower.jpg");
            var brush = new ImageBrush(image)
            {
                XExtend = ExtendMode.Reflect,
                YExtend = ExtendMode.Reflect,
                Quality = quality,
            };
            var matrix = Matrix3x2.CreateScale(0.4f) * Matrix3x2.CreateTranslation(120, 120) * transform;
            scene.DrawImage(brush, matrix);
        });

    private static ExampleScene LuminanceMask(ImageCache images, string? assetRoot)
        => new("luminance_mask", false, (scene, parameters) =>
        {
            var transform = parameters.ViewTransform;
            var maskBuilder = new PathBuilder();
            maskBuilder.MoveTo(200, 180)
                       .LineTo(480, 180)
                       .LineTo(480, 420)
                       .LineTo(200, 420)
                       .Close();
            scene.PushLuminanceMaskLayer(maskBuilder, transform, alpha: 0.8f);

            var gradient = new LinearGradientBrush(new Vector2(140, 160), new Vector2(540, 460), new[]
            {
                new GradientStop(0f, RgbaColor.FromBytes(255, 255, 255)),
                new GradientStop(1f, RgbaColor.FromBytes(40, 40, 60, 0)),
            });
            var fill = new PathBuilder();
            fill.MoveTo(120, 120).LineTo(520, 120).LineTo(520, 480).LineTo(120, 480).Close();
            scene.FillPath(fill, FillRule.NonZero, transform, gradient);

            scene.PopLayer();
        });

    private static ExampleScene ImageLuminanceMask(ImageCache images, string? assetRoot)
        => new("image_luminance_mask", false, (scene, parameters) =>
        {
            var transform = parameters.ViewTransform;
            var image = images.GetFromPath("splash-flower.jpg");
            var brush = new ImageBrush(image) { Alpha = 0.8f };
            var mask = new PathBuilder();
            mask.MoveTo(160, 160).LineTo(560, 160).LineTo(560, 420).LineTo(160, 420).Close();
            scene.PushLuminanceMaskLayer(mask, transform, 1f);
            scene.DrawImage(brush, Matrix3x2.CreateScale(0.6f) * Matrix3x2.CreateTranslation(160, 160) * transform);
            scene.PopLayer();
        });

    private sealed class MMarkScene
    {
        private const int Width = 1600;
        private const int Height = 900;
        private const int GridWidth = 80;
        private const int GridHeight = 40;
        private static readonly (int X, int Y)[] Offsets = { (-4, 0), (2, 0), (1, -2), (1, 2) };
        private static readonly RgbaColor[] Colors =
        {
            RgbaColor.FromBytes(0x10, 0x10, 0x10),
            RgbaColor.FromBytes(0x80, 0x80, 0x80),
            RgbaColor.FromBytes(0xC0, 0xC0, 0xC0),
            RgbaColor.FromBytes(0x10, 0x10, 0x10),
            RgbaColor.FromBytes(0x80, 0x80, 0x80),
            RgbaColor.FromBytes(0xC0, 0xC0, 0xC0),
            RgbaColor.FromBytes(0xE0, 0x10, 0x40),
        };

        private readonly List<Element> _elements = new();
        private readonly Random _random = new(1);

        public void Render(Scene scene, SceneParams parameters)
        {
            var complexity = Math.Max(1, parameters.Complexity);
            var target = complexity < 10 ? (complexity + 1) * 1000 : Math.Min((complexity - 8) * 10000, 120_000);
            Resize(target);

            var view = parameters.ViewTransform;
            var path = new PathBuilder();

            for (var i = 0; i < _elements.Count; i++)
            {
                var element = _elements[i];
                if (path.Count == 0)
                {
                    path.MoveTo(element.Start.X, element.Start.Y);
                }

                element.AppendTo(path);

                var isLast = i == _elements.Count - 1;
                if (element.IsSplit || isLast)
                {
                    var stroke = new StrokeStyle
                    {
                        Width = element.Width,
                        LineJoin = LineJoin.Bevel,
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round,
                    };
                    scene.StrokePath(path, stroke, view, element.Color);
                    path.Clear();
                }

                if (_random.NextDouble() > 0.995)
                {
                    element.IsSplit = !element.IsSplit;
                }

                _elements[i] = element;
            }

            var label = $"mmark test: {target} path elements (up/down to adjust)";
            parameters.Text.Add(scene, 40f, RgbaColor.FromBytes(0xFF, 0xFF, 0xFF), Matrix3x2.CreateTranslation(100f, 1100f) * view, null, label);
        }

        private void Resize(int target)
        {
            if (_elements.Count > target)
            {
                _elements.RemoveRange(target, _elements.Count - target);
                return;
            }

            var last = _elements.Count > 0 ? _elements[^1].GridPoint : new GridPoint(GridWidth / 2, GridHeight / 2);
            while (_elements.Count < target)
            {
                var element = Element.CreateRandom(last, _random);
                _elements.Add(element);
                last = element.GridPoint;
            }
        }

        private enum SegmentType
        {
            Line,
            Quad,
            Cubic,
        }

        private struct Element
        {
            public SegmentType Type;
            public Vector2 Start;
            public Vector2 Control1;
            public Vector2 Control2;
            public Vector2 End;
            public RgbaColor Color;
            public double Width;
            public bool IsSplit;
            public GridPoint GridPoint;

            public void AppendTo(PathBuilder builder)
            {
                switch (Type)
                {
                    case SegmentType.Line:
                        builder.LineTo(End.X, End.Y);
                        break;
                    case SegmentType.Quad:
                        builder.QuadraticTo(Control1.X, Control1.Y, End.X, End.Y);
                        break;
                    case SegmentType.Cubic:
                        builder.CubicTo(Control1.X, Control1.Y, Control2.X, Control2.Y, End.X, End.Y);
                        break;
                }
            }

            public static Element CreateRandom(GridPoint last, Random random)
            {
                var next = GridPoint.Next(last, random);
                SegmentType type;
                Vector2 start = last.ToCoordinate();
                Vector2 end = next.ToCoordinate();
                Vector2 control1 = end;
                Vector2 control2 = end;
                GridPoint gridPoint = next;

                var choice = random.Next(4);
                if (choice < 2)
                {
                    type = SegmentType.Line;
                }
                else if (choice < 3)
                {
                    type = SegmentType.Quad;
                    gridPoint = GridPoint.Next(next, random);
                    control1 = next.ToCoordinate();
                    end = gridPoint.ToCoordinate();
                }
                else
                {
                    type = SegmentType.Cubic;
                    control1 = next.ToCoordinate();
                    var mid = GridPoint.Next(next, random);
                    control2 = mid.ToCoordinate();
                    gridPoint = GridPoint.Next(next, random);
                    end = gridPoint.ToCoordinate();
                }

                var color = Colors[random.Next(Colors.Length)];
                var width = Math.Pow(random.NextDouble(), 5) * 20.0 + 1.0;
                var isSplit = random.Next(2) == 0;

                return new Element
                {
                    Type = type,
                    Start = start,
                    Control1 = control1,
                    Control2 = control2,
                    End = end,
                    Color = color,
                    Width = width,
                    IsSplit = isSplit,
                    GridPoint = gridPoint,
                };
            }
        }

        private readonly struct GridPoint
        {
            public GridPoint(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }
            public int Y { get; }

            public static GridPoint Next(GridPoint last, Random random)
            {
                var offset = Offsets[random.Next(Offsets.Length)];
                var x = last.X + offset.X;
                if (x < 0 || x > GridWidth)
                {
                    x -= offset.X * 2;
                }

                var y = last.Y + offset.Y;
                if (y < 0 || y > GridHeight)
                {
                    y -= offset.Y * 2;
                }

                return new GridPoint(x, y);
            }

            public Vector2 ToCoordinate()
            {
                var scaleX = Width / (GridWidth + 1f);
                var scaleY = Height / (GridHeight + 1f);
                return new Vector2((float)((X + 0.5f) * scaleX), 100f + (float)((Y + 0.5f) * scaleY));
            }
        }
    }

    private static RgbaColor RandomColor(Random rng, float alpha)
    {
        var r = (byte)rng.Next(30, 255);
        var g = (byte)rng.Next(30, 255);
        var b = (byte)rng.Next(30, 255);
        return new RgbaColor(r / 255f, g / 255f, b / 255f, alpha);
    }

    private static PathBuilder CreateRectanglePath(float x, float y, float width, float height)
    {
        var builder = new PathBuilder();
        builder.MoveTo(x, y)
               .LineTo(x + width, y)
               .LineTo(x + width, y + height)
               .LineTo(x, y + height)
               .Close();
        return builder;
    }

    private static PathBuilder CreateStarPath()
    {
        var builder = new PathBuilder();
        builder.MoveTo(50, 0)
               .LineTo(21, 90)
               .LineTo(98, 35)
               .LineTo(2, 35)
               .LineTo(79, 90)
               .Close();
        return builder;
    }

    private static void RenderCardioid(Scene scene, Matrix3x2 view)
    {
        const int Segments = 601;
        var delta = (float)(Math.PI * 2.0 / Segments);
        var center = new Vector2(1024f, 768f);
        const float radius = 750f;

        var path = new PathBuilder();
        for (var i = 1; i < Segments; i++)
        {
            var angle0 = i * delta;
            var angle1 = ((i * 2) % Segments) * delta;

            var p0 = new Vector2(center.X + MathF.Cos(angle0) * radius, center.Y + MathF.Sin(angle0) * radius);
            var p1 = new Vector2(center.X + MathF.Cos(angle1) * radius, center.Y + MathF.Sin(angle1) * radius);

            path.MoveTo(p0.X, p0.Y);
            path.LineTo(p1.X, p1.Y);
        }

        var stroke = new StrokeStyle
        {
            Width = 2.0,
            StartCap = LineCap.Butt,
            EndCap = LineCap.Butt,
            LineJoin = LineJoin.Miter,
        };

        scene.StrokePath(path, stroke, view, RgbaColor.FromBytes(0, 0, 255));
    }

    private static void RenderClipTest(Scene scene, Matrix3x2 view)
    {
        const int LayerCount = 16;
        const float X0 = 50f;
        const float Y0 = 450f;
        const float X1 = 550f;
        const float Y1 = 950f;

        var step = 1f / (LayerCount + 1f);
        for (var i = 0; i < LayerCount; i++)
        {
            var t = (i + 1f) * step;
            var clip = new PathBuilder();
            clip.MoveTo(X0, Y0)
                .LineTo(X1, Y0)
                .LineTo(X1, Y0 + t * (Y1 - Y0))
                .LineTo(X1 + t * (X0 - X1), Y1)
                .LineTo(X0, Y1)
                .Close();
            scene.PushLayer(clip, new LayerBlend(LayerMix.Clip, LayerCompose.SrcOver), view, 1f);
        }

        scene.FillPath(CreateRectanglePath(X0, Y0, X1 - X0, Y1 - Y0), FillRule.NonZero, view, RgbaColor.FromBytes(0, 255, 0));

        for (var i = 0; i < LayerCount; i++)
        {
            scene.PopLayer();
        }
    }

    private static void RenderAlphaTest(Scene scene, Matrix3x2 view)
    {
        scene.FillPath(CreateDiamondPath(1024f, 100f), FillRule.NonZero, view, RgbaColor.FromBytes(255, 0, 0));
        scene.FillPath(CreateDiamondPath(1024f, 125f), FillRule.NonZero, view, new RgbaColor(0f, 1f, 0f, 0.5f));

        var clip = CreateDiamondPath(1024f, 150f);
        scene.PushLayer(clip, new LayerBlend(LayerMix.Clip, LayerCompose.SrcOver), view, 1f);
        scene.FillPath(CreateDiamondPath(1024f, 175f), FillRule.NonZero, view, new RgbaColor(0f, 0f, 1f, 0.5f));
        scene.PopLayer();
    }

    private static PathBuilder CreateDiamondPath(float centerX, float centerY, float size = 50f)
    {
        var builder = new PathBuilder();
        builder.MoveTo(centerX, centerY - size)
               .LineTo(centerX + size, centerY)
               .LineTo(centerX, centerY + size)
               .LineTo(centerX - size, centerY)
               .Close();
        return builder;
    }
}
