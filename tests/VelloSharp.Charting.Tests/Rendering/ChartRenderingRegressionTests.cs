#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using VelloSharp;
using VelloSharp.ChartEngine;
using VelloSharp.ChartEngine.Annotations;
using VelloSharp.Charting.Rendering;
using VelloSharp.Charting.Styling;
using Xunit;

namespace VelloSharp.Charting.Tests.Rendering;

public sealed class ChartRenderingRegressionTests
{
    private const int Width = 960;
    private const int Height = 600;
    private const double PlotLeft = 96.0;
    private const double PlotTop = 48.0;
    private const double PlotWidth = Width - PlotLeft - 48.0;
    private const double PlotHeight = Height - PlotTop - 64.0;
    private static readonly string BaselineDirectory =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Baselines"));

    private static readonly ConstructorInfo MetadataConstructor =
        typeof(ChartFrameMetadata).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            new[]
            {
                typeof(double), typeof(double), typeof(double), typeof(double),
                typeof(double), typeof(double), typeof(double), typeof(double),
                typeof(ChartFrameMetadata.AxisTickMetadata[]),
                typeof(ChartFrameMetadata.AxisTickMetadata[]),
                typeof(ChartFrameMetadata.SeriesMetadata[]),
                typeof(ChartFrameMetadata.PaneMetadata[])
            },
            modifiers: null)!;

    [Fact]
    public void MultiPaneCompositionWithAnnotations_MatchesBaseline()
    {
        var metadata = BuildMetadata();
        var composition = BuildComposition();
        var annotations = BuildAnnotations();

        using var scene = new Scene();
        using var overlay = new ChartOverlayRenderer();
        overlay.Render(
            scene,
            metadata,
            Width,
            Height,
            devicePixelRatio: 1.0,
            ChartTheme.Dark,
            legendDefinition: null,
            composition: composition,
            annotations: annotations,
            renderAxes: true);

        var pixels = RenderScene(scene, Width, Height);
        AssertFrameMatchesBaseline("multi-pane-annotations", Width, Height, pixels);
    }

    private static ChartFrameMetadata BuildMetadata()
    {
        var timeTicks = new[]
        {
            new ChartFrameMetadata.AxisTickMetadata(0.0, "12:00"),
            new ChartFrameMetadata.AxisTickMetadata(0.25, "12:15"),
            new ChartFrameMetadata.AxisTickMetadata(0.50, "12:30"),
            new ChartFrameMetadata.AxisTickMetadata(0.75, "12:45"),
            new ChartFrameMetadata.AxisTickMetadata(1.0, "13:00"),
        };

        var priceTicks = new[]
        {
            new ChartFrameMetadata.AxisTickMetadata(0.0, "96"),
            new ChartFrameMetadata.AxisTickMetadata(0.25, "100"),
            new ChartFrameMetadata.AxisTickMetadata(0.50, "104"),
            new ChartFrameMetadata.AxisTickMetadata(0.75, "108"),
            new ChartFrameMetadata.AxisTickMetadata(1.0, "112"),
        };

        var latencyTicks = new[]
        {
            new ChartFrameMetadata.AxisTickMetadata(0.0, "40"),
            new ChartFrameMetadata.AxisTickMetadata(0.25, "80"),
            new ChartFrameMetadata.AxisTickMetadata(0.50, "120"),
            new ChartFrameMetadata.AxisTickMetadata(0.75, "160"),
            new ChartFrameMetadata.AxisTickMetadata(1.0, "200"),
        };

        var panes = new[]
        {
            new ChartFrameMetadata.PaneMetadata(
                "price",
                true,
                PlotLeft,
                PlotTop,
                PlotWidth,
                PlotHeight * 0.68,
                96.0,
                112.0,
                null,
                null,
                null,
                null,
                priceTicks),
            new ChartFrameMetadata.PaneMetadata(
                "latency",
                true,
                PlotLeft,
                PlotTop + PlotHeight * 0.68 + 12.0,
                PlotWidth,
                PlotHeight * 0.32,
                40.0,
                200.0,
                null,
                null,
                null,
                null,
                latencyTicks),
        };

        var series = new[]
        {
            new ChartFrameMetadata.SeriesMetadata(0, new ChartColor(0x3A, 0xB8, 0xFF, 0xFF), 2.0, "Price", ChartSeriesKind.Line, 0.18, 6.0, 8.0, 0.0, 0, 0, 0, 0),
            new ChartFrameMetadata.SeriesMetadata(1, new ChartColor(0x20, 0xCE, 0x8F, 0xFF), 1.0, "Latency", ChartSeriesKind.Area, 0.45, 4.0, 0.0, 0.0, 1, 0, 0, 0),
            new ChartFrameMetadata.SeriesMetadata(2, new ChartColor(0xFF, 0x9E, 0x7B, 0xFF), 1.0, "Markers", ChartSeriesKind.Scatter, 0.0, 6.0, 0.0, 0.0, 0, 0, 0, 0),
            new ChartFrameMetadata.SeriesMetadata(3, new ChartColor(0x47, 0xE2, 0xC2, 0xFF), 1.0, "Δ Price", ChartSeriesKind.Bar, 0.0, 4.0, 6.0, 0.0, 0, 0, 0, 0),
        };

        return (ChartFrameMetadata)MetadataConstructor.Invoke(new object[]
        {
            BaseTimestamp,
            BaseTimestamp + 180,
            40.0,
            200.0,
            PlotLeft,
            PlotTop,
            PlotWidth,
            PlotHeight,
            timeTicks,
            priceTicks,
            series,
            panes,
        });
    }

    private static ChartComposition BuildComposition()
    {
        CompositionAnnotationLayer? priceWindowLayer = null;

        var composition = ChartComposition.Create(builder =>
        {
            builder.Pane("price")
                .WithSeries(0, 2, 3)
                .WithHeightRatio(3.0)
                .ShareXAxisWithPrimary()
                .Done();

            builder.Pane("latency")
                .WithSeries(1)
                .WithHeightRatio(1.2)
                .ShareXAxisWithPrimary()
                .Done();

            builder.AnnotationLayer("latency-target", AnnotationZOrder.BelowSeries, layer =>
            {
                layer.ForPanes("latency");
                layer.Annotations.Add(new ValueZoneAnnotation(80, 160, "Latency target")
                {
                    Fill = new ChartColor(0x2B, 0x8A, 0x3A, 0x32),
                    Border = new ChartColor(0x2B, 0x8A, 0x3A, 0x96),
                    TargetPaneId = "latency",
                });
            });

            builder.AnnotationLayer("recent-activity", AnnotationZOrder.BelowSeries, layer =>
            {
                layer.ForPanes("price");
                priceWindowLayer = layer;
            });
        });

        if (priceWindowLayer is not null)
        {
            priceWindowLayer.Annotations.Add(new TimeRangeAnnotation(BaseTimestamp + 120, BaseTimestamp + 180, "Last hour")
            {
                Fill = new ChartColor(0x3A, 0xB8, 0xFF, 0x28),
                Border = new ChartColor(0x3A, 0xB8, 0xFF, 0x80),
                TargetPaneId = "price",
            });
        }

        return composition;
    }

    private static IReadOnlyList<ChartAnnotation> BuildAnnotations()
    {
        var accent = new ChartColor(0x3A, 0xB8, 0xFF, 0xFF);
        const double latestPrice = 104.2;
        var timestamp = BaseTimestamp + 180;

        return new ChartAnnotation[]
        {
            new HorizontalLineAnnotation(latestPrice, "Last trade")
            {
                Color = accent,
                Thickness = 1.5,
                TargetPaneId = "price",
            },
            new CalloutAnnotation(timestamp, latestPrice, $"{latestPrice:0.00}")
            {
                Color = accent,
                Background = new ChartColor(0x10, 0x15, 0x1F, 0xE6),
                Border = accent,
                TextColor = new ChartColor(0xEC, 0xEF, 0xF4, 0xFF),
                TargetPaneId = "price",
                PointerLength = 12.0,
            },
            new VerticalLineAnnotation(timestamp - 60, "Event")
            {
                Color = new ChartColor(0xF4, 0x5E, 0x8C, 0xFF),
                TargetPaneId = "price",
            },
        };
    }

    private static byte[] RenderScene(Scene scene, int width, int height)
    {
        using var renderer = new Renderer((uint)width, (uint)height);
        var buffer = new byte[width * height * 4];
        renderer.Render(
            scene,
            new RenderParams((uint)width, (uint)height, RgbaColor.FromBytes(0x10, 0x15, 0x1F, 0xFF))
            {
                Format = RenderFormat.Bgra8,
            },
            buffer,
            strideBytes: width * 4);
        return buffer;
    }

    private static void AssertFrameMatchesBaseline(string name, int width, int height, byte[] actual)
    {
        Directory.CreateDirectory(BaselineDirectory);
        var baselinePath = Path.Combine(BaselineDirectory, $"{name}.ppm");
        if (ShouldUpdateBaselines() || !File.Exists(baselinePath))
        {
            SaveImage(baselinePath, width, height, actual);
        }

        if (!File.Exists(baselinePath))
        {
            Assert.Fail($"Baseline '{baselinePath}' not found. Set UPDATE_RENDER_BASELINES=1 to generate it.");
        }

        var expected = LoadImage(baselinePath, out var expectedWidth, out var expectedHeight);
        Assert.Equal(width, expectedWidth);
        Assert.Equal(height, expectedHeight);

        if (!PixelsEqual(expected, actual))
        {
            var diffPath = Path.Combine(BaselineDirectory, $"{name}.diff.ppm");
            SaveDiff(diffPath, width, height, expected, actual);
            SaveImage(Path.Combine(BaselineDirectory, $"{name}.actual.ppm"), width, height, actual);
            Assert.Fail($"Rendered output for '{name}' differed from baseline. See '{diffPath}'.");
        }
    }

    private static bool PixelsEqual(byte[] expected, byte[] actual)
    {
        if (expected.Length != actual.Length)
        {
            return false;
        }

        for (var i = 0; i < expected.Length; i++)
        {
            if (expected[i] != actual[i])
            {
                return false;
            }
        }

        return true;
    }

    private static void SaveImage(string path, int width, int height, byte[] buffer)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);
        writer.Write(Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n"));

        for (var i = 0; i < width * height; i++)
        {
            var offset = i * 4;
            writer.Write(buffer[offset + 2]);
            writer.Write(buffer[offset + 1]);
            writer.Write(buffer[offset + 0]);
        }
    }

    private static byte[] LoadImage(string path, out int width, out int height)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);
        var magic = ReadToken(reader);
        if (!string.Equals(magic, "P6", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported PPM magic '{magic}'.");
        }

        width = int.Parse(ReadToken(reader), CultureInfo.InvariantCulture);
        height = int.Parse(ReadToken(reader), CultureInfo.InvariantCulture);
        var maxValue = int.Parse(ReadToken(reader), CultureInfo.InvariantCulture);
        if (maxValue != 255)
        {
            throw new InvalidDataException($"Unsupported max value '{maxValue}'.");
        }

        var pixelCount = width * height;
        var rgb = reader.ReadBytes(pixelCount * 3);
        if (rgb.Length != pixelCount * 3)
        {
            throw new EndOfStreamException("Unexpected end of file while reading PPM payload.");
        }

        var buffer = new byte[pixelCount * 4];
        for (var i = 0; i < pixelCount; i++)
        {
            var rgbOffset = i * 3;
            var offset = i * 4;
            buffer[offset + 2] = rgb[rgbOffset];
            buffer[offset + 1] = rgb[rgbOffset + 1];
            buffer[offset + 0] = rgb[rgbOffset + 2];
            buffer[offset + 3] = 0xFF;
        }

        return buffer;
    }

    private static void SaveDiff(string path, int width, int height, byte[] expected, byte[] actual)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);
        writer.Write(Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n"));

        for (var i = 0; i < width * height; i++)
        {
            var offset = i * 4;
            var diffR = Math.Abs(actual[offset + 2] - expected[offset + 2]);
            var diffG = Math.Abs(actual[offset + 1] - expected[offset + 1]);
            var diffB = Math.Abs(actual[offset + 0] - expected[offset + 0]);
            var intensity = (byte)Math.Clamp((diffR + diffG + diffB) / 3, 0, 255);
            writer.Write(intensity);
            writer.Write(intensity);
            writer.Write(intensity);
        }
    }

    private static string ReadToken(BinaryReader reader)
    {
        var sb = new StringBuilder();

        int value;
        do
        {
            value = reader.ReadByte();
        }
        while (char.IsWhiteSpace((char)value));

        while (true)
        {
            sb.Append((char)value);

            if (reader.BaseStream.Position >= reader.BaseStream.Length)
            {
                break;
            }

            value = reader.ReadByte();
            if (char.IsWhiteSpace((char)value))
            {
                break;
            }
        }

        return sb.ToString();
    }

    private static bool ShouldUpdateBaselines()
        => string.Equals(Environment.GetEnvironmentVariable("UPDATE_RENDER_BASELINES"), "1", StringComparison.OrdinalIgnoreCase);

    private static double BaseTimestamp => new DateTimeOffset(2024, 01, 01, 12, 00, 00, TimeSpan.Zero).ToUnixTimeSeconds();
}
