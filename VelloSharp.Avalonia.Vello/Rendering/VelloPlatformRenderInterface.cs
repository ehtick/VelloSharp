using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;

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

    public IPlatformRenderInterfaceRegion CreateRegion() => _fallback.CreateRegion();

    public IGeometryImpl CreateEllipseGeometry(Rect rect) => _fallback.CreateEllipseGeometry(rect);

    public IGeometryImpl CreateLineGeometry(Point p1, Point p2) => _fallback.CreateLineGeometry(p1, p2);

    public IGeometryImpl CreateRectangleGeometry(Rect rect) => _fallback.CreateRectangleGeometry(rect);

    public IStreamGeometryImpl CreateStreamGeometry() => _fallback.CreateStreamGeometry();

    global::Avalonia.Platform.IGeometryImpl global::Avalonia.Platform.IPlatformRenderInterface.CreateGeometryGroup(global::Avalonia.Media.FillRule fillRule, System.Collections.Generic.IReadOnlyList<global::Avalonia.Platform.IGeometryImpl> children)
    {
        return _fallback.CreateGeometryGroup(fillRule, children);
    }

    public IGeometryImpl CreateCombinedGeometry(GeometryCombineMode combineMode, IGeometryImpl g1, IGeometryImpl g2) =>
        _fallback.CreateCombinedGeometry(combineMode, g1, g2);

    public IGeometryImpl BuildGlyphRunGeometry(GlyphRun glyphRun) => _fallback.BuildGlyphRunGeometry(glyphRun);

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
        _fallback.CreateGlyphRun(glyphTypeface, fontRenderingEmSize, glyphInfos, baselineOrigin);

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
