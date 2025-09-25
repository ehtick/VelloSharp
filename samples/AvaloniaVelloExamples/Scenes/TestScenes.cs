using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using VelloSharp;

namespace AvaloniaVelloExamples.Scenes;

public static class TestScenes
{
    public static IReadOnlyList<ExampleScene> BuildScenes(ImageCache images, string? assetRoot)
    {
        ArgumentNullException.ThrowIfNull(images);

        return new List<ExampleScene>
        {
            FunkyPaths(),
            StrokeStyles(),
            StrokeStylesNonUniform(),
            StrokeStylesSkew(),
            TrickyStrokes(),
            FillTypes(),
            CardioidAndFriends(),
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
            MotionMark(),
            ManyDrawObjects(),
            BlurredRoundedRect(),
            ImageSampling(images, assetRoot),
            ImageExtendModes("image_extend_modes (bilinear)", images, assetRoot, ImageQuality.Medium),
            ImageExtendModes("image_extend_modes (nearest)", images, assetRoot, ImageQuality.Low),
            LuminanceMask(images, assetRoot),
            ImageLuminanceMask(images, assetRoot),
            Spinner(),
        };
    }

    private static ExampleScene FunkyPaths()
        => new("funky_paths", false, (scene, parameters) =>
        {
            var rnd = new Random(12345);
            var builder = new PathBuilder();
            var transform = parameters.ViewTransform;
            for (var i = 0; i < 20; i++)
            {
                builder.Clear();
                var x = rnd.NextDouble() * 800;
                var y = rnd.NextDouble() * 600;
                builder.MoveTo(x, y);
                for (var j = 0; j < 6; j++)
                {
                    var tx = rnd.NextDouble() * 800;
                    var ty = rnd.NextDouble() * 600;
                    builder.QuadraticTo((x + tx) / 2, (y + ty) / 2, tx, ty);
                    x = tx;
                    y = ty;
                }
                var color = RgbaColor.FromBytes((byte)rnd.Next(255), (byte)rnd.Next(255), (byte)rnd.Next(255), 160);
                scene.FillPath(builder, FillRule.EvenOdd, transform, color);
            }
        });

    private static ExampleScene StrokeStyles()
        => new("stroke_styles", false, (scene, parameters) =>
        {
            var transform = Matrix3x2.CreateTranslation(100, 100) * parameters.ViewTransform;
            var builder = new PathBuilder();
            builder.MoveTo(0, 0).LineTo(200, 0).LineTo(200, 200).Close();
            var style = new StrokeStyle
            {
                Width = 20,
                LineJoin = LineJoin.Miter,
                StartCap = LineCap.Butt,
                EndCap = LineCap.Butt,
            };
            scene.StrokePath(builder, style, transform, RgbaColor.FromBytes(230, 90, 90));

            style.LineJoin = LineJoin.Bevel;
            style.StartCap = style.EndCap = LineCap.Round;
            var t2 = Matrix3x2.CreateTranslation(260, 0) * parameters.ViewTransform;
            scene.StrokePath(builder, style, t2, RgbaColor.FromBytes(90, 160, 220));
        });

    private static ExampleScene StrokeStylesNonUniform()
        => new("stroke_styles (non-uniform scale)", false, (scene, parameters) =>
        {
            var transform = Matrix3x2.CreateScale(1.2f, 0.7f) * Matrix3x2.CreateTranslation(120, 120) * parameters.ViewTransform;
            DrawStrokeGrid(scene, transform);
        });

    private static ExampleScene StrokeStylesSkew()
        => new("stroke_styles (skew)", false, (scene, parameters) =>
        {
            var transform = Matrix3x2.CreateSkew(1.0f, 0f) * Matrix3x2.CreateTranslation(120, 120) * parameters.ViewTransform;
            DrawStrokeGrid(scene, transform);
        });

    private static void DrawStrokeGrid(Scene scene, Matrix3x2 transform)
    {
        var builder = new PathBuilder();
        builder.MoveTo(0, 0).LineTo(200, 0).LineTo(200, 200).Close();
        var styles = new[]
        {
            new StrokeStyle { Width = 20, LineJoin = LineJoin.Miter, StartCap = LineCap.Butt, EndCap = LineCap.Butt },
            new StrokeStyle { Width = 20, LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round },
            new StrokeStyle { Width = 20, LineJoin = LineJoin.Bevel, StartCap = LineCap.Square, EndCap = LineCap.Square },
        };

        var colors = new[]
        {
            RgbaColor.FromBytes(240, 110, 110),
            RgbaColor.FromBytes(110, 170, 240),
            RgbaColor.FromBytes(110, 240, 160),
        };

        for (var i = 0; i < styles.Length; i++)
        {
            var offset = Matrix3x2.CreateTranslation(i * 220f, 0f);
            scene.StrokePath(builder, styles[i], offset * transform, colors[i]);
        }
    }

    private static ExampleScene TrickyStrokes()
        => new("tricky_strokes", false, (scene, parameters) =>
        {
            var transform = parameters.ViewTransform;
            var builder = new PathBuilder();
            builder.MoveTo(100, 100).LineTo(300, 100).LineTo(380, 180).LineTo(100, 220).Close();
            var stroke = new StrokeStyle
            {
                Width = 30,
                LineJoin = LineJoin.Miter,
                MiterLimit = 1.5,
            };
            scene.StrokePath(builder, stroke, transform, RgbaColor.FromBytes(70, 160, 240));
        });

    private static ExampleScene FillTypes()
        => new("fill_types", false, (scene, parameters) =>
        {
            var transform = parameters.ViewTransform;
            var builder = new PathBuilder();
            builder.MoveTo(100, 100).LineTo(300, 100).LineTo(300, 300).LineTo(100, 300).Close();
            builder.MoveTo(150, 150).LineTo(250, 150).LineTo(250, 250).LineTo(150, 250).Close();
            scene.FillPath(builder, FillRule.EvenOdd, transform, RgbaColor.FromBytes(230, 110, 180));
        });

    private static ExampleScene CardioidAndFriends()
        => new("cardioid_and_friends", false, (scene, parameters) =>
        {
            var transform = parameters.ViewTransform;
            var builder = new PathBuilder();
            var center = new Vector2(400, 280);
            const int segments = 180;
            var radius = 180f;
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var theta = t * MathF.Tau;
                var r = radius * (1 - MathF.Sin(theta));
                var x = center.X + r * MathF.Cos(theta);
                var y = center.Y + r * MathF.Sin(theta);
                if (i == 0)
                {
                    builder.MoveTo(x, y);
                }
                else
                {
                    builder.LineTo(x, y);
                }
            }
            builder.Close();
            scene.FillPath(builder, FillRule.NonZero, transform, RgbaColor.FromBytes(255, 120, 120));
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
            var t = (float)(parameters.Time * 0.8);
            var transform = parameters.ViewTransform;
            var clip = new PathBuilder();
            clip.MoveTo(240, 200).LineTo(520, 200).LineTo(520, 420).LineTo(240, 420).Close();
            scene.PushLayer(clip, new LayerBlend(LayerMix.Normal, LayerCompose.SrcOver), transform, 1f);

            var builder = new PathBuilder();
            builder.MoveTo(120, 120).LineTo(640, 120).LineTo(640, 460).LineTo(120, 460).Close();
            var gradient = new LinearGradientBrush(new Vector2(120, 120), new Vector2(640, 460), new[]
            {
                new GradientStop(0f, RgbaColor.FromBytes(255, 200, 120)),
                new GradientStop(1f, RgbaColor.FromBytes(80, 140, 255)),
            });
            scene.FillPath(builder, FillRule.NonZero, transform, gradient);

            builder.Clear();
            builder.MoveTo(280 + 120 * MathF.Cos(t), 260 + 120 * MathF.Sin(t))
                   .LineTo(420 + 120 * MathF.Cos(t + MathF.PI * 2 / 3), 260 + 120 * MathF.Sin(t + MathF.PI * 2 / 3))
                   .LineTo(360, 480)
                   .Close();
            scene.FillPath(builder, FillRule.NonZero, transform, RgbaColor.FromBytes(255, 255, 255, 180));

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

    private static ExampleScene MotionMark()
        => new("mmark", true, (scene, parameters) =>
        {
            var transform = parameters.ViewTransform;
            var rng = new Random(1);
            var elementCount = Math.Clamp(parameters.Complexity * 1000, 1000, 120_000);
            var builder = new PathBuilder();
            for (var i = 0; i < elementCount; i++)
            {
                builder.Clear();
                var x = rng.NextDouble() * 800;
                var y = rng.NextDouble() * 600;
                builder.MoveTo(x, y);
                for (var j = 0; j < 3; j++)
                {
                    var nx = rng.NextDouble() * 800;
                    var ny = rng.NextDouble() * 600;
                    builder.QuadraticTo((x + nx) / 2, (y + ny) / 2, nx, ny);
                    x = nx;
                    y = ny;
                }
                scene.StrokePath(builder, new StrokeStyle { Width = 1.5 }, transform, RandomColor(rng, 0.3f));
            }
        });

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

    private static ExampleScene Spinner()
        => new("spinner", true, (scene, parameters) =>
        {
            var builder = new PathBuilder();
            var center = new Vector2(400f, 300f);
            var radius = 220f;
            var blades = 8;
            var angle = (float)parameters.Time * 0.8f;
            for (var i = 0; i < blades; i++)
            {
                var theta = angle + i * MathF.Tau / blades;
                builder.MoveTo(center.X, center.Y);
                var tip = center + new Vector2(MathF.Cos(theta), MathF.Sin(theta)) * radius;
                var ctrl = center + new Vector2(MathF.Cos(theta + 0.4f), MathF.Sin(theta + 0.4f)) * (radius * 0.4f);
                builder.QuadraticTo(ctrl.X, ctrl.Y, tip.X, tip.Y);
                builder.Close();
            }

            var bladeColor = RgbaColor.FromBytes(156, 220, 255, 210);
            var transform = parameters.ViewTransform;
            scene.FillPath(builder, FillRule.NonZero, transform, bladeColor);

            var stroke = new StrokeStyle { Width = 4.0, LineJoin = LineJoin.Round };
            builder.Clear();
            const int segments = 72;
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var theta = t * MathF.Tau;
                var point = center + new Vector2(MathF.Cos(theta), MathF.Sin(theta)) * radius;
                if (i == 0)
                {
                    builder.MoveTo(point.X, point.Y);
                }
                else
                {
                    builder.LineTo(point.X, point.Y);
                }
            }
            builder.Close();
            scene.StrokePath(builder, stroke, transform, RgbaColor.FromBytes(40, 42, 48));
        });

    private static RgbaColor RandomColor(Random rng, float alpha)
    {
        var r = (byte)rng.Next(30, 255);
        var g = (byte)rng.Next(30, 255);
        var b = (byte)rng.Next(30, 255);
        return new RgbaColor(r / 255f, g / 255f, b / 255f, alpha);
    }
}
