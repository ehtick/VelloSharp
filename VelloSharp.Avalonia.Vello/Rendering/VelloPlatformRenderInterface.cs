using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;
using VelloSharp.Avalonia.Vello.Geometry;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal sealed class VelloPlatformRenderInterface : IPlatformRenderInterface
{
    private readonly VelloGraphicsDevice _graphicsDevice;
    private readonly VelloPlatformOptions _options;

    public VelloPlatformRenderInterface(VelloGraphicsDevice graphicsDevice, VelloPlatformOptions options)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        DefaultPixelFormat = PixelFormat.Rgba8888;
        DefaultAlphaFormat = AlphaFormat.Unpremul;
        SupportsIndividualRoundRects = true;
        SupportsRegions = true;
    }

    public bool SupportsIndividualRoundRects { get; }

    public AlphaFormat DefaultAlphaFormat { get; }

    public PixelFormat DefaultPixelFormat { get; }

    public bool SupportsRegions { get; }

    public IPlatformRenderInterfaceContext CreateBackendContext(IPlatformGraphicsContext? graphicsContext)
    {
        return new VelloPlatformRenderInterfaceContext(_graphicsDevice, _options);
    }

    public bool IsSupportedBitmapPixelFormat(PixelFormat format) =>
        format == PixelFormat.Rgba8888 || format == PixelFormat.Bgra8888;

    public IPlatformRenderInterfaceRegion CreateRegion() => new VelloRegionImpl();

    public IGeometryImpl CreateEllipseGeometry(Rect rect) => new VelloEllipseGeometryImpl(rect);

    public IGeometryImpl CreateLineGeometry(Point p1, Point p2) => new VelloLineGeometryImpl(p1, p2);

    public IGeometryImpl CreateRectangleGeometry(Rect rect) => new VelloRectangleGeometryImpl(rect);

    public IStreamGeometryImpl CreateStreamGeometry() => new VelloStreamGeometryImpl();

    global::Avalonia.Platform.IGeometryImpl global::Avalonia.Platform.IPlatformRenderInterface.CreateGeometryGroup(global::Avalonia.Media.FillRule fillRule, System.Collections.Generic.IReadOnlyList<global::Avalonia.Platform.IGeometryImpl> children)
    {
        return new VelloGeometryGroupImpl(fillRule, children);
    }

    public IGeometryImpl CreateCombinedGeometry(GeometryCombineMode combineMode, IGeometryImpl g1, IGeometryImpl g2) =>
        new VelloCombinedGeometryImpl(combineMode, g1, g2);

    public IGeometryImpl BuildGlyphRunGeometry(GlyphRun glyphRun)
    {
        if (glyphRun is null)
        {
            throw new ArgumentNullException(nameof(glyphRun));
        }

        if (glyphRun.GlyphTypeface is null)
        {
            return new VelloPathGeometryImpl(new VelloPathData());
        }

        var glyphInfos = glyphRun.GlyphInfos;
        if (glyphInfos.Count == 0)
        {
            return new VelloPathGeometryImpl(new VelloPathData());
        }

        var font = VelloFontManager.GetFont(glyphRun.GlyphTypeface);
        var aggregate = new VelloPathData();

        var baseline = glyphRun.BaselineOrigin;
        var currentX = 0.0;

        for (var i = 0; i < glyphInfos.Count; i++)
        {
            var glyphInfo = glyphInfos[i];
            if (!VelloFontManager.TryGetGlyphOutline(font, (ushort)glyphInfo.GlyphIndex, glyphRun.FontRenderingEmSize, out var outline))
            {
                return new VelloPathGeometryImpl(new VelloPathData());
            }

            if (outline.Commands.Length == 0)
            {
                continue;
            }

            var offsetX = baseline.X + currentX + glyphInfo.GlyphOffset.X;
            var offsetY = baseline.Y + glyphInfo.GlyphOffset.Y;
            AppendGlyphOutline(aggregate, outline.Commands, offsetX, offsetY);

            currentX += glyphInfo.GlyphAdvance;
        }

        return new VelloPathGeometryImpl(aggregate);
    }

    public IRenderTargetBitmapImpl CreateRenderTargetBitmap(PixelSize size, Vector dpi) =>
        throw new NotSupportedException("RenderTargetBitmap is not supported by the Vello backend yet.");

    public IWriteableBitmapImpl CreateWriteableBitmap(PixelSize size, Vector dpi, PixelFormat format, AlphaFormat alphaFormat)
    {
        EnsureSupportedPixelFormat(format);
        return VelloBitmapImpl.Create(size, dpi, format, alphaFormat);
    }

    public IBitmapImpl LoadBitmap(string fileName) => VelloBitmapImpl.Load(fileName);

    public IBitmapImpl LoadBitmap(Stream stream)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        return VelloBitmapImpl.Load(stream);
    }

    public IWriteableBitmapImpl LoadWriteableBitmapToWidth(Stream stream, int width, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        var bitmap = VelloBitmapImpl.Load(stream);
        var aspect = (double)bitmap.PixelSize.Height / Math.Max(1, bitmap.PixelSize.Width);
        var targetSize = new PixelSize(width, Math.Max(1, (int)Math.Round(width * aspect)));
        var resized = bitmap.Resize(targetSize, interpolationMode);
        bitmap.Dispose();
        return resized;
    }

    public IWriteableBitmapImpl LoadWriteableBitmapToHeight(Stream stream, int height, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        var bitmap = VelloBitmapImpl.Load(stream);
        var aspect = (double)bitmap.PixelSize.Width / Math.Max(1, bitmap.PixelSize.Height);
        var targetSize = new PixelSize(Math.Max(1, (int)Math.Round(height * aspect)), height);
        var resized = bitmap.Resize(targetSize, interpolationMode);
        bitmap.Dispose();
        return resized;
    }

    public IWriteableBitmapImpl LoadWriteableBitmap(string fileName) => VelloBitmapImpl.Load(fileName);

    public IWriteableBitmapImpl LoadWriteableBitmap(Stream stream)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        return VelloBitmapImpl.Load(stream);
    }

    public IBitmapImpl LoadBitmap(PixelFormat format, AlphaFormat alphaFormat, IntPtr data, PixelSize size, Vector dpi, int stride)
    {
        EnsureSupportedPixelFormat(format);
        var length = stride * size.Height;
        var buffer = new byte[length];
        System.Runtime.InteropServices.Marshal.Copy(data, buffer, 0, length);
        return VelloBitmapImpl.FromPixels(buffer, size, dpi, format, alphaFormat);
    }

    public IBitmapImpl LoadBitmapToWidth(Stream stream, int width, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality)
        => (IBitmapImpl)LoadWriteableBitmapToWidth(stream, width, interpolationMode);

    public IBitmapImpl LoadBitmapToHeight(Stream stream, int height, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality)
        => (IBitmapImpl)LoadWriteableBitmapToHeight(stream, height, interpolationMode);

    public IBitmapImpl ResizeBitmap(IBitmapImpl bitmapImpl, PixelSize destinationSize, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality)
        => bitmapImpl is VelloBitmapImpl velloBitmap
            ? velloBitmap.Resize(destinationSize, interpolationMode)
            : throw new NotSupportedException("Bitmap implementation is not compatible with the Vello backend.");

    public IGlyphRunImpl CreateGlyphRun(IGlyphTypeface glyphTypeface, double fontRenderingEmSize, IReadOnlyList<GlyphInfo> glyphInfos, Point baselineOrigin) =>
        new VelloGlyphRunImpl(glyphTypeface, fontRenderingEmSize, glyphInfos, baselineOrigin);

    private static void EnsureSupportedPixelFormat(PixelFormat format)
    {
        if (format != PixelFormat.Rgba8888 && format != PixelFormat.Bgra8888)
        {
            throw new NotSupportedException($"Pixel format {format} is not supported by the Vello backend.");
        }
    }

    private static void AppendGlyphOutline(VelloPathData target, VelloPathElement[] commands, double offsetX, double offsetY)
    {
        foreach (var command in commands)
        {
            switch (command.Verb)
            {
                case VelloPathVerb.MoveTo:
                    target.MoveTo(command.X0 + offsetX, command.Y0 + offsetY);
                    break;
                case VelloPathVerb.LineTo:
                    target.LineTo(command.X0 + offsetX, command.Y0 + offsetY);
                    break;
                case VelloPathVerb.QuadTo:
                    target.QuadraticTo(
                        command.X0 + offsetX,
                        command.Y0 + offsetY,
                        command.X1 + offsetX,
                        command.Y1 + offsetY);
                    break;
                case VelloPathVerb.CubicTo:
                    target.CubicTo(
                        command.X0 + offsetX,
                        command.Y0 + offsetY,
                        command.X1 + offsetX,
                        command.Y1 + offsetY,
                        command.X2 + offsetX,
                        command.Y2 + offsetY);
                    break;
                case VelloPathVerb.Close:
                    target.Close();
                    break;
            }
        }
    }

}
