using System;
using System.Collections.Generic;
using System.IO;
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
    private readonly IPlatformRenderInterface _fallback;

    public VelloPlatformRenderInterface(VelloGraphicsDevice graphicsDevice, VelloPlatformOptions options)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _fallback = CreateFallbackInterface();
        DefaultPixelFormat = _fallback.DefaultPixelFormat;
        DefaultAlphaFormat = _fallback.DefaultAlphaFormat;
        SupportsIndividualRoundRects = _fallback.SupportsIndividualRoundRects;
        SupportsRegions = _fallback.SupportsRegions;
    }

    public bool SupportsIndividualRoundRects { get; }

    public AlphaFormat DefaultAlphaFormat { get; }

    public PixelFormat DefaultPixelFormat { get; }

    public bool SupportsRegions { get; }

    public IPlatformRenderInterfaceContext CreateBackendContext(IPlatformGraphicsContext? graphicsContext)
    {
        var fallbackContext = _fallback.CreateBackendContext(graphicsContext);
        return new VelloPlatformRenderInterfaceContext(_graphicsDevice, _options, fallbackContext);
    }

    public bool IsSupportedBitmapPixelFormat(PixelFormat format) => _fallback.IsSupportedBitmapPixelFormat(format);

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
            return _fallback.BuildGlyphRunGeometry(glyphRun);
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
                return _fallback.BuildGlyphRunGeometry(glyphRun);
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
        _fallback.CreateRenderTargetBitmap(size, dpi);

    public IWriteableBitmapImpl CreateWriteableBitmap(PixelSize size, Vector dpi, PixelFormat format, AlphaFormat alphaFormat) =>
        _fallback.CreateWriteableBitmap(size, dpi, format, alphaFormat);

    public IBitmapImpl LoadBitmap(string fileName) => _fallback.LoadBitmap(fileName);

    public IBitmapImpl LoadBitmap(Stream stream) => _fallback.LoadBitmap(stream);

    public IWriteableBitmapImpl LoadWriteableBitmapToWidth(Stream stream, int width, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality) =>
        _fallback.LoadWriteableBitmapToWidth(stream, width, interpolationMode);

    public IWriteableBitmapImpl LoadWriteableBitmapToHeight(Stream stream, int height, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality) =>
        _fallback.LoadWriteableBitmapToHeight(stream, height, interpolationMode);

    public IWriteableBitmapImpl LoadWriteableBitmap(string fileName) => _fallback.LoadWriteableBitmap(fileName);

    public IWriteableBitmapImpl LoadWriteableBitmap(Stream stream) => _fallback.LoadWriteableBitmap(stream);

    public IBitmapImpl LoadBitmap(PixelFormat format, AlphaFormat alphaFormat, IntPtr data, PixelSize size, Vector dpi, int stride) =>
        _fallback.LoadBitmap(format, alphaFormat, data, size, dpi, stride);

    public IBitmapImpl LoadBitmapToWidth(Stream stream, int width, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality) =>
        _fallback.LoadBitmapToWidth(stream, width, interpolationMode);

    public IBitmapImpl LoadBitmapToHeight(Stream stream, int height, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality) =>
        _fallback.LoadBitmapToHeight(stream, height, interpolationMode);

    public IBitmapImpl ResizeBitmap(IBitmapImpl bitmapImpl, PixelSize destinationSize, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality) =>
        _fallback.ResizeBitmap(bitmapImpl, destinationSize, interpolationMode);

    public IGlyphRunImpl CreateGlyphRun(IGlyphTypeface glyphTypeface, double fontRenderingEmSize, IReadOnlyList<GlyphInfo> glyphInfos, Point baselineOrigin) =>
        new VelloGlyphRunImpl(glyphTypeface, fontRenderingEmSize, glyphInfos, baselineOrigin);

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

    private static IPlatformRenderInterface CreateFallbackInterface()
    {
        var type = Type.GetType("Avalonia.Skia.PlatformRenderInterface, Avalonia.Skia", throwOnError: true);
        if (type is null)
        {
            throw new InvalidOperationException("Unable to load Avalonia.Skia.PlatformRenderInterface.");
        }

        var instance = Activator.CreateInstance(
            type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            args: new object?[] { null },
            culture: null);
        if (instance is null)
        {
            throw new InvalidOperationException("Failed to create fallback render interface instance.");
        }

        return (IPlatformRenderInterface)instance;
    }
}
