using System;
using System.IO;
using System.Linq;
using SkiaSharp;
using VelloSharp;

var info = new SKImageInfo(256, 256, SKColorType.Bgra8888, SKAlphaType.Premul);

var checkerInfo = new SKImageInfo(32, 32, SKColorType.Bgra8888, SKAlphaType.Premul);
var checkerPixels = new byte[checkerInfo.Width * checkerInfo.Height * 4];
for (var y = 0; y < checkerInfo.Height; y++)
{
    for (var x = 0; x < checkerInfo.Width; x++)
    {
        var isDark = ((x / 4) + (y / 4)) % 2 == 0;
        var offset = (y * checkerInfo.Width + x) * 4;
        checkerPixels[offset + 0] = isDark ? (byte)30 : (byte)220;
        checkerPixels[offset + 1] = isDark ? (byte)30 : (byte)210;
        checkerPixels[offset + 2] = (byte)(isDark ? 30 : 205);
        checkerPixels[offset + 3] = 255;
    }
}

using var checkerImage = SKImage.FromPixels(checkerInfo, checkerPixels, checkerInfo.Width * 4);

using var picture = RecordScene(info, checkerImage);

using var surface = SKSurface.Create(info);
var canvas = surface.Canvas;

canvas.Clear(SKColors.White);
canvas.DrawPicture(picture);

using var renderer = new Renderer((uint)info.Width, (uint)info.Height);
var renderParams = new RenderParams(
    Width: (uint)info.Width,
    Height: (uint)info.Height,
    BaseColor: RgbaColor.FromBytes(255, 255, 255, 255));

var stride = info.Width * 4;
var buffer = new byte[stride * info.Height];
renderer.Render(surface.Scene, renderParams, buffer, stride);

var nonZeroPixels = buffer.Chunk(4).Count(pixel => pixel.Any(component => component != 255));

ulong checksum = 0;
foreach (var value in buffer)
{
    checksum = (checksum + value) * 1099511628211UL;
}

const ulong ExpectedChecksum = 2249980945708938622UL;

if (nonZeroPixels == 0)
{
    Console.Error.WriteLine("Smoke test failed: rendered buffer is blank.");
    Environment.ExitCode = 1;
}
else if (checksum != ExpectedChecksum)
{
    Console.Error.WriteLine($"Smoke test failed: checksum mismatch. Expected {ExpectedChecksum}, actual {checksum}.");
    Environment.ExitCode = 1;
}
else
{
    var outputPath = Path.Combine(AppContext.BaseDirectory, "skia-shim-smoke.raw");
    File.WriteAllBytes(outputPath, buffer);
    Console.WriteLine($"Smoke test passed. Non-white pixels: {nonZeroPixels}. Checksum: {checksum}. Raw buffer saved to {outputPath}.");
}

static SKPicture RecordScene(SKImageInfo info, SKImage checker)
{
    using var recorder = new SKPictureRecorder();
    var recordingCanvas = recorder.BeginRecording(SKRect.Create(0, 0, info.Width, info.Height));
    DrawScene(recordingCanvas, checker);
    return recorder.EndRecording();
}

static void DrawScene(SKCanvas canvas, SKImage checker)
{
    var rect = SKRect.Create(32, 32, 192, 112);

    using (var fillPaint = new SKPaint
           {
               Color = new SKColor(34, 139, 230, 255),
               Style = SKPaintStyle.Fill,
               IsAntialias = true,
           })
    using (var rectPath = new SKPath())
    {
        rectPath.MoveTo(rect.Left, rect.Top);
        rectPath.LineTo(rect.Right, rect.Top);
        rectPath.LineTo(rect.Right, rect.Bottom);
        rectPath.LineTo(rect.Left, rect.Bottom);
        rectPath.Close();

        canvas.DrawPath(rectPath, fillPaint);

        using (var gradientPaint = new SKPaint
               {
                   Style = SKPaintStyle.Fill,
                   IsAntialias = true,
                   Shader = SKShader.CreateLinearGradient(
                       new SKPoint(rect.Left, rect.Top),
                       new SKPoint(rect.Right, rect.Bottom),
                       new[]
                       {
                           new SKColor(255, 255, 255, 160),
                           new SKColor(34, 139, 230, 0),
                       },
                       new[] { 0f, 1f },
                       SKShaderTileMode.Clamp),
                   Opacity = 0.85f,
               })
        {
            canvas.DrawPath(rectPath, gradientPaint);
        }

        using (var strokePaint = new SKPaint
               {
                   Color = new SKColor(20, 20, 20, 255),
                   Style = SKPaintStyle.Stroke,
                   StrokeWidth = 6f,
                   StrokeJoin = SKStrokeJoin.Round,
                   StrokeCap = SKStrokeCap.Round,
                   IsAntialias = true,
               })
        {
            canvas.DrawPath(rectPath, strokePaint);
        }
    }

    using (var textPaint = new SKPaint
           {
               Color = new SKColor(15, 15, 20, 255),
               TextSize = 32f,
               IsAntialias = true,
           })
    {
        canvas.DrawText("Hello Vello", 48f, 120f, textPaint);
    }

    canvas.DrawImage(checker, SKRect.Create(160, 160, 64, 64));
}
