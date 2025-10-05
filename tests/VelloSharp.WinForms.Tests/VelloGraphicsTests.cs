using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using GdiLineCap = System.Drawing.Drawing2D.LineCap;
using GdiLineJoin = System.Drawing.Drawing2D.LineJoin;
using VelloSharp;
using VelloSharp.WinForms;
using Xunit;

namespace VelloSharp.WinForms.Tests;

public class VelloGraphicsTests
{
    [Fact]
    public void FillRectangleProducesNonWhitePixels()
    {
        try
        {
            using var device = new VelloGraphicsDevice(32, 32);
            using var session = device.BeginSession(32, 32);
            var graphics = new VelloGraphics(session);
            graphics.FillRectangle(Color.Red, new RectangleF(0, 0, 32, 32));

            var buffer = new byte[32 * 32 * 4];
            session.Submit(buffer, 32 * 4);
            Assert.Contains(buffer, b => b != 0);
        }
        catch (DllNotFoundException)
        {
            // Skip on environments without native Vello binaries.
        }
    }

    [Fact]
    public void SaveRestoreRoundTripsTransform()
    {
        try
        {
            using var device = new VelloGraphicsDevice(16, 16);
            using var session = device.BeginSession(16, 16);
            var graphics = new VelloGraphics(session);

            var initial = graphics.Transform;
            var state = graphics.Save();
            graphics.TranslateTransform(4, 4, MatrixOrder.Append);
            Assert.NotEqual(initial, graphics.Transform);

            graphics.Restore(state);
            Assert.Equal(initial, graphics.Transform);
        }
        catch (DllNotFoundException)
        {
            // Skip on environments without native Vello binaries.
        }
    }

    [Fact]
    public void DrawRectangleHonoursPenState()
    {
        try
        {
            using var device = new VelloGraphicsDevice(16, 16);
            using var session = device.BeginSession(16, 16);
            var graphics = new VelloGraphics(session);

            using var pen = new VelloPen(Color.Blue, 2f)
            {
                LineJoin = GdiLineJoin.Round,
                StartCap = GdiLineCap.Round,
                EndCap = GdiLineCap.Square,
            };

            graphics.DrawRectangle(pen, new RectangleF(1, 1, 10, 10));

            pen.Dispose();
            Assert.Throws<ObjectDisposedException>(() => graphics.DrawRectangle(pen, new RectangleF(0, 0, 4, 4)));
        }
        catch (DllNotFoundException)
        {
            // Skip on environments without native Vello binaries.
        }
    }

    [Fact]
    public void FillRectangleAcceptsCustomBrush()
    {
        try
        {
            using var device = new VelloGraphicsDevice(8, 8);
            using var session = device.BeginSession(8, 8);
            var graphics = new VelloGraphics(session);

            using var brush = new VelloSolidBrush(Color.Lime);
            graphics.FillRectangle(brush, new RectangleF(0, 0, 8, 8));

            var buffer = new byte[8 * 8 * 4];
            session.Submit(buffer, 8 * 4);
            Assert.Contains(buffer, b => b != 0);
        }
        catch (DllNotFoundException)
        {
            // Skip on environments without native Vello binaries.
        }
    }
    [Fact]
    public void DrawLineProducesStroke()
    {
        try
        {
            using var device = new VelloGraphicsDevice(32, 32);
            using var session = device.BeginSession(32, 32);
            var graphics = new VelloGraphics(session);
            using var pen = new VelloPen(Color.Blue, 1f);

            graphics.DrawLine(pen, new PointF(0, 0), new PointF(31, 31));

            var buffer = new byte[32 * 32 * 4];
            session.Submit(buffer, 32 * 4);
            Assert.Contains(buffer, b => b != 0);
        }
        catch (DllNotFoundException)
        {
            // Skip when native binaries are unavailable.
        }
    }

    [Fact]
    public void ClipRestrictsFill()
    {
        try
        {
            using var device = new VelloGraphicsDevice(32, 32);
            using var session = device.BeginSession(32, 32);
            var graphics = new VelloGraphics(session);

            graphics.SetClip(new RectangleF(0, 0, 16, 32));
            graphics.FillRectangle(Color.Red, new RectangleF(0, 0, 32, 32));

            var buffer = new byte[32 * 32 * 4];
            session.Submit(buffer, 32 * 4);

            var rightHalfZero = Enumerable.Range(16, 16)
                .SelectMany(x => Enumerable.Range(0, 32).Select(y => (x, y)))
                .All(pixel =>
                {
                    var index = (pixel.y * 32 + pixel.x) * 4;
                    return buffer[index] == 0 && buffer[index + 1] == 0 && buffer[index + 2] == 0 && buffer[index + 3] == 0;
                });

            Assert.True(rightHalfZero);
        }
        catch (DllNotFoundException)
        {
            // Skip when native binaries are unavailable.
        }
    }

    [Fact]
    public void DrawImagePlacesPixels()
    {
        try
        {
            using var device = new VelloGraphicsDevice(32, 32);
            using var session = device.BeginSession(32, 32);
            var graphics = new VelloGraphics(session);

            var pixels = new byte[]
            {
                0, 255, 0, 255, 0, 255, 0, 255,
                0, 255, 0, 255, 0, 255, 0, 255,
            };

            using var bitmap = VelloBitmap.FromPixels(pixels, 2, 2, RenderFormat.Bgra8, ImageAlphaMode.Premultiplied, stride: 8);
            graphics.DrawImage(bitmap, new RectangleF(4, 4, 4, 4));

            var buffer = new byte[32 * 32 * 4];
            session.Submit(buffer, 32 * 4);
            var index = (5 * 32 + 5) * 4;
            Assert.True(buffer[index + 1] > 0);
        }
        catch (DllNotFoundException)
        {
            // Skip when native binaries are unavailable.
        }
    }

    [Fact]
    public void DrawStringProducesContentAndMeasures()
    {
        try
        {
            using var device = new VelloGraphicsDevice(64, 32);
            using var session = device.BeginSession(64, 32);
            var graphics = new VelloGraphics(session);
            using var font = new VelloFont("Arial", 12f);
            using var brush = new VelloSolidBrush(Color.White);

            graphics.DrawString("Hi", font, brush, new PointF(2, 2));
            var size = graphics.MeasureString("Hi", font);

            var buffer = new byte[64 * 32 * 4];
            session.Submit(buffer, 64 * 4);
            Assert.Contains(buffer, b => b != 0);
            Assert.True(size.Width > 0f);
            Assert.True(size.Height > 0f);
        }
        catch (DllNotFoundException)
        {
            // Skip when native binaries are unavailable.
        }
    }
}





