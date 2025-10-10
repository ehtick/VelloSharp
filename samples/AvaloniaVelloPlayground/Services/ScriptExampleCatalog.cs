using System.Collections.Generic;
using AvaloniaVelloPlayground.ViewModels;

namespace AvaloniaVelloPlayground.Services;

public sealed class ScriptExampleCatalog
{
    private static readonly IReadOnlyList<ScriptExample> Examples =
    [
        new ScriptExample(
            "Animated Fill",
            "Basics",
            ExampleCode.AnimatedFill,
            "FillPath with animated geometry and solid colours."),
        new ScriptExample(
            "Stroke Playground",
            "Basics",
            ExampleCode.StrokePlayground,
            "StrokePath with dash patterns, caps, and animated dash phase."),
        new ScriptExample(
            "Gradient Panel",
            "Brushes",
            ExampleCode.LinearGradientPanel,
            "LinearGradientBrush plus per-frame transforms."),
        new ScriptExample(
            "Peniko Sweep",
            "Brushes",
            ExampleCode.PenikoSweep,
            "Peniko sweep gradient rendered through FillPath."),
        new ScriptExample(
            "Layered Bloom",
            "Layers",
            ExampleCode.LayerBlur,
            "PushLayer with multiple DrawBlurredRoundedRect calls."),
        new ScriptExample(
            "Luminance Mask",
            "Layers",
            ExampleCode.LuminanceMask,
            "PushLuminanceMaskLayer masking a sweep gradient."),
        new ScriptExample(
            "Image Playground",
            "Images",
            ExampleCode.ImageAndBrush,
            "DrawImage and ImageBrush using a cached procedural texture."),
        new ScriptExample(
            "Glyph Fill & Stroke",
            "Text",
            ExampleCode.GlyphFill,
            "DrawGlyphRun with shaped text, fill and outline."),
        new ScriptExample(
            "GPU Surface Clear",
            "GPU Lease",
            ExampleCode.GpuSurfaceClear,
            "ScheduleWgpuSurfaceRender to clear the swapchain."),
        new ScriptExample(
            "Orbital Trails",
            "Scenes",
            ExampleCode.OrbitalTrails,
            "Composite scene mixing fills, strokes and transforms."),
    ];

    public IReadOnlyList<ScriptExample> GetExamples() => Examples;

    private static class ExampleCode
    {
        public const string AnimatedFill = """
        // FillPath with animated geometry using solid colours.
        var outer = new PathBuilder();
        outer.MoveTo(width * 0.18f, height * 0.2f);
        outer.LineTo(width * 0.82f, height * 0.2f);
        outer.LineTo(width * 0.82f, height * 0.8f);
        outer.LineTo(width * 0.18f, height * 0.8f);
        outer.Close();

        scene.FillPath(outer, FillRule.NonZero, Matrix3x2.Identity, new RgbaColor(0.09f, 0.13f, 0.2f, 1f));

        var center = new Vector2(width / 2f, height / 2f);
        var radius = MathF.Min(width, height) * 0.35f;
        var swirl = new PathBuilder();
        const int segments = 220;

        for (var i = 0; i <= segments; i++)
        {
            var progress = i / (float)segments;
            var angle = progress * MathF.Tau;
            var wobble = MathF.Sin(angle * 6f + (float)time * 1.35f) * radius * 0.12f;
            var r = radius + wobble;
            var x = center.X + MathF.Cos(angle) * r;
            var y = center.Y + MathF.Sin(angle) * r;

            if (i == 0)
            {
                swirl.MoveTo(x, y);
            }
            else
            {
                swirl.LineTo(x, y);
            }
        }

        swirl.Close();

        scene.FillPath(swirl, FillRule.NonZero, Matrix3x2.Identity, new RgbaColor(0.26f, 0.67f, 1f, 0.85f));

        var outline = new StrokeStyle
        {
            Width = Math.Max(2.0, radius * 0.06f),
            LineJoin = LineJoin.Round,
        };

        scene.StrokePath(swirl, outline, Matrix3x2.Identity, new RgbaColor(0f, 0f, 0f, 0.25f));
        """;

        public const string StrokePlayground = """
        // StrokePath with caps, joins, dash patterns, and animated dash phase.
        var wave = new PathBuilder();
        var amplitude = MathF.Min(width, height) * 0.18f;
        var baseline = height / 2f;
        const int steps = 200;

        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            var x = width * 0.1f + t * width * 0.8f;
            var y = baseline + MathF.Sin(t * MathF.Tau * 2f + (float)time) * amplitude;
            if (i == 0)
            {
                wave.MoveTo(x, y);
            }
            else
            {
                wave.LineTo(x, y);
            }
        }

        var mainStroke = new StrokeStyle
        {
            Width = Math.Max(6.0, Math.Min(width, height) * 0.018f),
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            DashPattern = new[] { 24.0, 12.0, 6.0, 12.0 },
            DashPhase = time * 120,
        };

        scene.StrokePath(wave, mainStroke, Matrix3x2.Identity, new RgbaColor(1f, 0.55f, 0.3f, 0.9f));

        var accentStroke = new StrokeStyle
        {
            Width = mainStroke.Width * 0.45,
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            DashPattern = new[] { 4.0, 8.0 },
            DashPhase = time * -160,
        };

        scene.StrokePath(wave, accentStroke, Matrix3x2.Identity, new RgbaColor(0.1f, 0.16f, 0.25f, 1f));
        """;

        public const string LinearGradientPanel = """
        // LinearGradientBrush fill combined with animated transform.
        var panel = new PathBuilder();
        panel.MoveTo(width * 0.18f, height * 0.22f);
        panel.LineTo(width * 0.82f, height * 0.22f);
        panel.LineTo(width * 0.82f, height * 0.78f);
        panel.LineTo(width * 0.18f, height * 0.78f);
        panel.Close();

        var gradient = new LinearGradientBrush(
            new Vector2(width * 0.2f, height * 0.25f),
            new Vector2(width * 0.8f, height * 0.75f),
            new[]
            {
                new GradientStop(0f, new RgbaColor(0.07f, 0.18f, 0.35f, 1f)),
                new GradientStop(0.5f, new RgbaColor(0.28f, 0.7f, 0.86f, 1f)),
                new GradientStop(1f, new RgbaColor(0.98f, 0.82f, 0.45f, 1f)),
            },
            ExtendMode.Reflect);

        scene.FillPath(panel, FillRule.NonZero, Matrix3x2.Identity, gradient);

        var ring = CreateCirclePath(width / 2f, height / 2f, MathF.Min(width, height) * 0.32f);
        var ringTransform =
            Matrix3x2.CreateRotation((float)time * 0.4f, new Vector2(width / 2f, height / 2f)) *
            Matrix3x2.CreateScale(1.02f, 1.0f, new Vector2(width / 2f, height / 2f));

        scene.StrokePath(ring, new StrokeStyle { Width = 6.0, LineJoin = LineJoin.Round }, ringTransform, new RgbaColor(0f, 0f, 0f, 0.45f));

        static PathBuilder CreateCirclePath(float cx, float cy, float radius)
        {
            var builder = new PathBuilder();
            const int segments = 120;
            for (var i = 0; i <= segments; i++)
            {
                var angle = MathF.Tau * i / segments;
                var x = cx + MathF.Cos(angle) * radius;
                var y = cy + MathF.Sin(angle) * radius;
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
            return builder;
        }
        """;

        public const string PenikoSweep = """
        // PenikoBrush sweep gradient with animated rotation.
        var center = new Vector2(width / 2f, height / 2f);
        var star = new PathBuilder();
        const int points = 10;
        var outerRadius = MathF.Min(width, height) * 0.42f;
        var innerRadius = outerRadius * 0.45f;

        for (var i = 0; i < points; i++)
        {
            var angle = i / (float)points * MathF.Tau;
            var radius = i % 2 == 0 ? outerRadius : innerRadius;
            var x = center.X + MathF.Cos(angle + (float)time * 0.3f) * radius;
            var y = center.Y + MathF.Sin(angle + (float)time * 0.3f) * radius;
            if (i == 0)
            {
                star.MoveTo(x, y);
            }
            else
            {
                star.LineTo(x, y);
            }
        }

        star.Close();

        var sweep = new PenikoSweepGradient
        {
            Center = new PenikoPoint { X = center.X, Y = center.Y },
            StartAngle = 0f,
            EndAngle = MathF.Tau,
        };

        var stops = new[]
        {
            new PenikoColorStop { Offset = 0f, Color = new VelloColor { R = 1f, G = 0.2f, B = 0.4f, A = 1f } },
            new PenikoColorStop { Offset = 0.45f, Color = new VelloColor { R = 0.15f, G = 0.6f, B = 1f, A = 1f } },
            new PenikoColorStop { Offset = 1f, Color = new VelloColor { R = 0.95f, G = 0.95f, B = 0.4f, A = 1f } },
        };

        using var brush = PenikoBrush.CreateSweep(sweep, PenikoExtend.Reflect, stops);
        var rotation = Matrix3x2.CreateRotation((float)time * 0.55f, center);
        scene.FillPath(star, FillRule.NonZero, rotation, brush);
        """;

        public const string LayerBlur = """
        // PushLayer and DrawBlurredRoundedRect blending multiple highlights.
        var bounds = new Rect(width * 0.22f, height * 0.22f, width * 0.56f, height * 0.56f);
        var center = new Vector2((float)bounds.Center.X, (float)bounds.Center.Y);
        var clipPath = CreateRoundedRect(bounds, MathF.Min(width, height) * 0.08f);

        scene.FillPath(clipPath, FillRule.NonZero, Matrix3x2.Identity, new RgbaColor(0.04f, 0.07f, 0.12f, 1f));
        scene.PushLayer(clipPath, new LayerBlend(LayerMix.Normal, LayerCompose.SrcOver), Matrix3x2.Identity, 1f);

        var wobble = new Vector2(
            MathF.Sin((float)time * 0.8f) * (float)bounds.Width * 0.18f,
            MathF.Cos((float)time * 1.1f) * (float)bounds.Height * 0.18f);

        scene.DrawBlurredRoundedRect(
            new Vector2((float)bounds.X + wobble.X, (float)bounds.Y + wobble.Y),
            new Vector2((float)bounds.Width * 0.75f, (float)bounds.Height * 0.6f),
            Matrix3x2.CreateRotation((float)time * 0.15f, center),
            new RgbaColor(0.18f, 0.55f, 1f, 0.55f),
            radius: Math.Min(bounds.Width, bounds.Height) * 0.28f,
            stdDev: Math.Min(width, height) * 0.02f);

        scene.DrawBlurredRoundedRect(
            new Vector2((float)bounds.X + bounds.Width * 0.25f, (float)bounds.Y + bounds.Height * 0.3f),
            new Vector2((float)bounds.Width * 0.5f, (float)bounds.Height * 0.55f),
            Matrix3x2.CreateRotation(-(float)time * 0.2f, center),
            new RgbaColor(1f, 0.35f, 0.45f, 0.45f),
            radius: Math.Min(bounds.Width, bounds.Height) * 0.22f,
            stdDev: Math.Min(width, height) * 0.018f);

        scene.PopLayer();

        static PathBuilder CreateRoundedRect(Rect rect, float radius)
        {
            var builder = new PathBuilder();
            var r = Math.Max(0, radius);

            builder.MoveTo(rect.X + r, rect.Y);
            builder.LineTo(rect.Right - r, rect.Y);
            AddArc(builder, rect.Right - r, rect.Y + r, r, -MathF.PI / 2f, 0f);

            builder.LineTo(rect.Right, rect.Bottom - r);
            AddArc(builder, rect.Right - r, rect.Bottom - r, r, 0f, MathF.PI / 2f);

            builder.LineTo(rect.X + r, rect.Bottom);
            AddArc(builder, rect.X + r, rect.Bottom - r, r, MathF.PI / 2f, MathF.PI);

            builder.LineTo(rect.X, rect.Y + r);
            AddArc(builder, rect.X + r, rect.Y + r, r, MathF.PI, MathF.PI * 1.5f);

            builder.Close();
            return builder;

            static void AddArc(PathBuilder builder, double cx, double cy, double radius, double start, double end)
            {
                const int segments = 12;
                for (var i = 1; i <= segments; i++)
                {
                    var t = i / (double)segments;
                    var angle = start + (end - start) * t;
                    var x = cx + Math.Cos(angle) * radius;
                    var y = cy + Math.Sin(angle) * radius;
                    builder.LineTo(x, y);
                }
            }
        }
        """;

        public const string LuminanceMask = """
        // PushLuminanceMaskLayer to punch gradients through a mask shape.
        var center = new Vector2(width / 2f, height / 2f);
        var outerRadius = MathF.Min(width, height) * 0.42f;
        var innerRadius = outerRadius * 0.6f;

        var mask = CreateRing(center, outerRadius, innerRadius);
        scene.PushLuminanceMaskLayer(mask, Matrix3x2.Identity, 1f);

        var sweep = new SweepGradientBrush(center, 0f, MathF.Tau,
            new[]
            {
                new GradientStop(0f, new RgbaColor(1f, 0.85f, 0.3f, 1f)),
                new GradientStop(0.5f, new RgbaColor(0.2f, 0.75f, 1f, 1f)),
                new GradientStop(1f, new RgbaColor(1f, 0.3f, 0.75f, 1f)),
            },
            ExtendMode.Reflect);

        var sweepTransform = Matrix3x2.CreateRotation((float)time * 0.65f, center);
        var circle = CreateCircle(center, outerRadius * 0.95f);
        scene.FillPath(circle, FillRule.NonZero, sweepTransform, sweep);

        scene.PopLayer();

        static PathBuilder CreateCircle(Vector2 c, float radius)
        {
            var builder = new PathBuilder();
            const int segments = 160;
            for (var i = 0; i <= segments; i++)
            {
                var angle = MathF.Tau * i / segments;
                var x = c.X + MathF.Cos(angle) * radius;
                var y = c.Y + MathF.Sin(angle) * radius;
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
            return builder;
        }

        static PathBuilder CreateRing(Vector2 c, float outerRadius, float innerRadius)
        {
            var builder = new PathBuilder();
            const int segments = 200;
            for (var i = 0; i <= segments; i++)
            {
                var angle = MathF.Tau * i / segments;
                var x = c.X + MathF.Cos(angle) * outerRadius;
                var y = c.Y + MathF.Sin(angle) * outerRadius;
                if (i == 0)
                {
                    builder.MoveTo(x, y);
                }
                else
                {
                    builder.LineTo(x, y);
                }
            }

            for (var i = segments; i >= 0; i--)
            {
                var angle = MathF.Tau * i / segments;
                var x = c.X + MathF.Cos(angle) * innerRadius;
                var y = c.Y + MathF.Sin(angle) * innerRadius;
                builder.LineTo(x, y);
            }

            builder.Close();
            return builder;
        }
        """;

        public const string ImageAndBrush = """
        // DrawImage and ImageBrush using a cached procedural texture.
        var image = PlaygroundAssets.NoiseImage;
        var info = image.GetInfo();
        var scale = Math.Min(width / info.Width, height / info.Height) * 0.55f;

        var baseTransform =
            Matrix3x2.CreateTranslation(-info.Width / 2f, -info.Height / 2f) *
            Matrix3x2.CreateRotation((float)time * 0.35f) *
            Matrix3x2.CreateScale(scale) *
            Matrix3x2.CreateTranslation(width / 2f, height / 2f);

        // Direct image draw.
        scene.DrawImage(image, baseTransform);

        var brush = new ImageBrush(image)
        {
            XExtend = ExtendMode.Reflect,
            YExtend = ExtendMode.Reflect,
            Quality = ImageQuality.High,
            Alpha = 0.85f,
        };

        var brushTransform =
            Matrix3x2.CreateScale(0.75f, 0.75f, new Vector2(width / 2f, height / 2f)) *
            Matrix3x2.CreateRotation(-(float)time * 0.2f, new Vector2(width / 2f, height / 2f));

        var hull = CreateRoundedRect(new Rect(width * 0.2f, height * 0.2f, width * 0.6f, height * 0.6f), MathF.Min(width, height) * 0.1f);
        scene.FillPath(hull, FillRule.NonZero, brushTransform, brush);

        var frameStroke = new StrokeStyle
        {
            Width = 8.0,
            LineJoin = LineJoin.Round,
        };

        scene.StrokePath(hull, frameStroke, Matrix3x2.Identity, new RgbaColor(0f, 0f, 0f, 0.5f));

        static PathBuilder CreateRoundedRect(Rect rect, float radius)
        {
            var builder = new PathBuilder();
            var r = Math.Max(0, radius);

            builder.MoveTo(rect.X + r, rect.Y);
            builder.LineTo(rect.Right - r, rect.Y);
            AddArc(builder, rect.Right - r, rect.Y + r, r, -MathF.PI / 2f, 0f);

            builder.LineTo(rect.Right, rect.Bottom - r);
            AddArc(builder, rect.Right - r, rect.Bottom - r, r, 0f, MathF.PI / 2f);

            builder.LineTo(rect.X + r, rect.Bottom);
            AddArc(builder, rect.X + r, rect.Bottom - r, r, MathF.PI / 2f, MathF.PI);

            builder.LineTo(rect.X, rect.Y + r);
            AddArc(builder, rect.X + r, rect.Y + r, r, MathF.PI, MathF.PI * 1.5f);

            builder.Close();
            return builder;

            static void AddArc(PathBuilder builder, double cx, double cy, double radius, double start, double end)
            {
                const int segments = 10;
                for (var i = 1; i <= segments; i++)
                {
                    var t = i / (double)segments;
                    var angle = start + (end - start) * t;
                    var x = cx + Math.Cos(angle) * radius;
                    var y = cy + Math.Sin(angle) * radius;
                    builder.LineTo(x, y);
                }
            }
        }
        """;

        public const string GlyphFill = """
        // DrawGlyphRun fills and strokes shaped text using cached Inter font data.
        const string text = "VelloSharp";
        var fontSize = MathF.Min(width, height) * 0.12f;
        var glyphs = PlaygroundAssets.ShapeText(text, fontSize);

        if (glyphs.Length == 0)
        {
            return;
        }

        var advance = glyphs[^1].X + fontSize * 0.2f;
        var baseline = height * 0.58f;
        var transform = Matrix3x2.CreateTranslation((width - advance) / 2f, baseline);

        var fillOptions = new GlyphRunOptions
        {
            Brush = new SolidColorBrush(new RgbaColor(0.92f, 0.96f, 1f, 1f)),
            FontSize = fontSize,
            Transform = transform,
        };

        scene.DrawGlyphRun(PlaygroundAssets.InterFont, glyphs, fillOptions);

        var strokeOptions = new GlyphRunOptions
        {
            Brush = new SolidColorBrush(new RgbaColor(0.1f, 0.32f, 0.68f, 1f)),
            FontSize = fontSize,
            Transform = transform,
            Style = GlyphRunStyle.Stroke,
            Stroke = new StrokeStyle
            {
                Width = Math.Max(1.5, fontSize * 0.05f),
                LineJoin = LineJoin.Round,
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            },
            BrushAlpha = 0.85f,
        };

        scene.DrawGlyphRun(PlaygroundAssets.InterFont, glyphs, strokeOptions);
        """;

        public const string GpuSurfaceClear = """
        // ScheduleWgpuSurfaceRender to clear the swapchain before compositing the scene.
        var baseColor = new WgpuColor
        {
            R = 0.02 + Math.Sin(time * 0.45) * 0.01,
            G = 0.06 + Math.Cos(time * 0.4) * 0.02,
            B = 0.12,
            A = 1.0,
        };

        ctx.Lease.ScheduleWgpuSurfaceRender(context =>
        {
            using var encoder = context.Device.CreateCommandEncoder();
            var attachment = new WgpuRenderPassColorAttachment
            {
                View = context.TargetView,
                Load = WgpuLoadOp.Clear,
                Store = WgpuStoreOp.Store,
                ClearColor = baseColor,
            };

            using (var pass = encoder.BeginRenderPass(new WgpuRenderPassDescriptor
            {
                ColorAttachments = new[] { attachment },
            }))
            {
                // Clearing only; no geometry submitted.
            }

            using var commandBuffer = encoder.Finish();
            context.Queue.Submit(new[] { commandBuffer });
        });

        var ring = CreateRing(new Vector2(width / 2f, height / 2f), MathF.Min(width, height) * 0.46f, MathF.Min(width, height) * 0.4f);
        scene.StrokePath(ring, new StrokeStyle { Width = 3.0, LineJoin = LineJoin.Round }, Matrix3x2.Identity, new RgbaColor(1f, 1f, 1f, 0.08f));

        static PathBuilder CreateRing(Vector2 center, float outerRadius, float innerRadius)
        {
            var builder = new PathBuilder();
            const int segments = 120;
            for (var i = 0; i <= segments; i++)
            {
                var angle = MathF.Tau * i / segments;
                var x = center.X + MathF.Cos(angle) * outerRadius;
                var y = center.Y + MathF.Sin(angle) * outerRadius;
                if (i == 0)
                {
                    builder.MoveTo(x, y);
                }
                else
                {
                    builder.LineTo(x, y);
                }
            }

            for (var i = segments; i >= 0; i--)
            {
                var angle = MathF.Tau * i / segments;
                var x = center.X + MathF.Cos(angle) * innerRadius;
                var y = center.Y + MathF.Sin(angle) * innerRadius;
                builder.LineTo(x, y);
            }

            builder.Close();
            return builder;
        }
        """;

        public const string OrbitalTrails = """
        // Multi-layered composition mixing fills, strokes, and transformation matrices.
        var center = new Vector2(width / 2f, height / 2f);
        var baseRadius = MathF.Min(width, height) * 0.32f;

        DrawOrbit(baseRadius, new RgbaColor(0.4f, 0.8f, 1f, 0.25f), 0.6f, 0f);
        DrawOrbit(baseRadius * 0.7f, new RgbaColor(1f, 0.6f, 0.3f, 0.35f), -0.9f, MathF.PI / 4f);
        DrawOrbit(baseRadius * 1.2f, new RgbaColor(0.7f, 0.4f, 1f, 0.25f), 0.4f, MathF.PI / 2f);

        for (var i = 0; i < 4; i++)
        {
            var angle = (float)time * 0.6f + i * MathF.Tau / 4f;
            var orbitRadius = baseRadius * (0.65f + i * 0.1f);
            var position = new Vector2(
                center.X + MathF.Cos(angle) * orbitRadius,
                center.Y + MathF.Sin(angle) * orbitRadius);

            var nodePath = CreateCircle(position, MathF.Min(width, height) * 0.04f);
            var pulse = 0.6f + MathF.Sin((float)time * 2f + i) * 0.2f;
            scene.FillPath(nodePath, FillRule.NonZero, Matrix3x2.Identity, new RgbaColor(0.9f, 0.95f, 1f, pulse));
        }

        void DrawOrbit(float radius, RgbaColor color, float speed, float phase)
        {
            var path = new PathBuilder();
            const int segments = 160;
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var angle = t * MathF.Tau;
                var modulation = 1f + 0.05f * MathF.Sin(angle * 5f + (float)time * speed);
                var x = center.X + MathF.Cos(angle + phase) * radius * modulation;
                var y = center.Y + MathF.Sin(angle + phase) * radius * modulation;
                if (i == 0)
                {
                    path.MoveTo(x, y);
                }
                else
                {
                    path.LineTo(x, y);
                }
            }

            path.Close();

            var stroke = new StrokeStyle
            {
                Width = Math.Max(2.5, radius * 0.015f),
                LineJoin = LineJoin.Round,
            };

            scene.StrokePath(path, stroke, Matrix3x2.Identity, color);
        }

        static PathBuilder CreateCircle(Vector2 position, float radius)
        {
            var builder = new PathBuilder();
            const int segments = 60;
            for (var i = 0; i <= segments; i++)
            {
                var angle = i / (float)segments * MathF.Tau;
                var x = position.X + MathF.Cos(angle) * radius;
                var y = position.Y + MathF.Sin(angle) * radius;
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
            return builder;
        }
        """;
    }
}
