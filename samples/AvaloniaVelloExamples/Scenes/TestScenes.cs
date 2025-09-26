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

        var baseTransform = Matrix3x2.CreateScale(0.11f) * Matrix3x2.CreateTranslation(-90f, -50f);
        var view = baseTransform * parameters.ViewTransform;

        for (var i = 0; i < lines.Length; i++)
        {
            var size = i == 0 ? 60f : 40f;
            var lineTransform = Matrix3x2.CreateTranslation(100f, 100f + 60f * i);
            var finalTransform = lineTransform * view;
            text.Add(scene, size, new RgbaColor(1f, 1f, 1f, 1f), finalTransform, null, lines[i]);
        }
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
}
