using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using VelloSharp;

namespace SkiaSharp;

public sealed class SKSurface : IDisposable
{
    private const AntialiasingMode DefaultAntialiasing = AntialiasingMode.Area;

    private readonly Scene _scene;
    private readonly SKCanvas _canvas;
    private readonly GRContext? _context;
    private readonly bool _ownsScene;
    private readonly SurfaceBackend _backend;
    private readonly SKSurfaceProperties? _properties;
    private readonly RenderFormat _renderFormat;
    private readonly RgbaColor _baseColor;
    private readonly int _sampleCount;
    private bool _disposed;

    private SKSurface(
        Scene scene,
        bool ownsScene,
        SKImageInfo info,
        Matrix3x2 initialTransform,
        SurfaceBackend backend,
        SKSurfaceProperties? properties = null,
        GRContext? context = null,
        int sampleCount = 1)
    {
        Info = info;
        _context = context;
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _ownsScene = ownsScene;
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _properties = properties;
        _renderFormat = ResolveRenderFormat(info.ColorType);
        _baseColor = RgbaColor.FromBytes(0, 0, 0, 0);
        _sampleCount = Math.Max(1, sampleCount);
        _canvas = new SKCanvas(_scene, info.Width, info.Height, ownsScene, initialTransform);
    }

    public SKImageInfo Info { get; }

    public SKCanvas Canvas
    {
        get
        {
            ThrowIfDisposed();
            return _canvas;
        }
    }

    public Scene Scene
    {
        get
        {
            ThrowIfDisposed();
            return _scene;
        }
    }

    public bool IsWrappedScene => !_ownsScene;

    public static SKSurface Create(SKImageInfo info)
        => new(new Scene(), true, info, Matrix3x2.Identity, SceneSurfaceBackend.Instance);

    public static SKSurface Create(Scene scene, SKImageInfo info, Matrix3x2 initialTransform)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return new SKSurface(scene, ownsScene: false, info, initialTransform, SceneSurfaceBackend.Instance);
    }

    public static SKSurface Create(SKImageInfo info, SKSurfaceProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return new SKSurface(new Scene(), true, info, Matrix3x2.Identity, SceneSurfaceBackend.Instance, properties);
    }

    public static SKSurface Create(GRContext context, bool budgeted, SKImageInfo info)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = budgeted;
        return CreateGpuSurface(info, SurfaceHandle.Headless, context, sampleCount: 1, properties: null);
    }

    public static SKSurface Create(GRContext context, bool budgeted, SKImageInfo info, SKSurfaceProperties properties)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(properties);
        _ = budgeted;
        return CreateGpuSurface(info, SurfaceHandle.Headless, context, sampleCount: 1, properties: properties);
    }

    public static SKSurface Create(GRContext context, SKImageInfo info, SurfaceHandle surfaceHandle)
        => Create(context, info, surfaceHandle, sampleCount: 1, properties: null);

    public static SKSurface Create(GRContext context, SKImageInfo info, SurfaceHandle surfaceHandle, int sampleCount)
        => Create(context, info, surfaceHandle, sampleCount, properties: null);

    public static SKSurface Create(GRContext context, SKImageInfo info, SurfaceHandle surfaceHandle, int sampleCount, SKSurfaceProperties? properties)
    {
        ArgumentNullException.ThrowIfNull(context);
        return CreateGpuSurface(info, surfaceHandle, context, Math.Max(1, sampleCount), properties);
    }

    public static SKSurface Create(GRContext context, GRBackendRenderTarget renderTarget, GRSurfaceOrigin origin, SKColorType colorType)
        => Create(context, renderTarget, origin, colorType, default(SKColorSpace?), default(SKSurfaceProperties?));

    public static SKSurface Create(GRContext context, GRBackendRenderTarget renderTarget, GRSurfaceOrigin origin, SKColorType colorType, SKSurfaceProperties? props)
        => Create(context, renderTarget, origin, colorType, default(SKColorSpace?), props);

    public static SKSurface Create(GRContext context, GRBackendRenderTarget renderTarget, GRSurfaceOrigin origin, SKColorType colorType, SKColorSpace? colorspace)
        => Create(context, renderTarget, origin, colorType, colorspace, default(SKSurfaceProperties?));

    public static SKSurface Create(GRContext context, GRBackendRenderTarget renderTarget, GRSurfaceOrigin origin, SKColorType colorType, SKColorSpace? colorspace, SKSurfaceProperties? props)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(renderTarget);
        _ = origin;
        _ = colorspace;
        var info = new SKImageInfo(renderTarget.Width, renderTarget.Height, colorType, SKAlphaType.Premul);
        var handle = SurfaceHandle.Headless;
        var sampleCount = Math.Max(1, renderTarget.Samples);
        return CreateGpuSurface(info, handle, context, sampleCount, props);
    }

    public static SKSurface Create(GRContext context, GRBackendTexture texture, GRSurfaceOrigin origin, SKColorType colorType)
        => Create(context, texture, origin, 0, colorType, default(SKColorSpace?), default(SKSurfaceProperties?));

    public static SKSurface Create(GRContext context, GRBackendTexture texture, GRSurfaceOrigin origin, SKColorType colorType, SKSurfaceProperties? props)
        => Create(context, texture, origin, 0, colorType, default(SKColorSpace?), props);

    public static SKSurface Create(GRContext context, GRBackendTexture texture, GRSurfaceOrigin origin, int sampleCount, SKColorType colorType)
        => Create(context, texture, origin, sampleCount, colorType, default(SKColorSpace?), default(SKSurfaceProperties?));

    public static SKSurface Create(GRContext context, GRBackendTexture texture, GRSurfaceOrigin origin, int sampleCount, SKColorType colorType, SKColorSpace? colorspace)
        => Create(context, texture, origin, sampleCount, colorType, colorspace, default(SKSurfaceProperties?));

    public static SKSurface Create(GRContext context, GRBackendTexture texture, GRSurfaceOrigin origin, int sampleCount, SKColorType colorType, SKColorSpace? colorspace, SKSurfaceProperties? props)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(texture);
        _ = origin;
        _ = sampleCount;
        _ = colorspace;
        var info = new SKImageInfo(texture.Width, texture.Height, colorType, SKAlphaType.Premul);
        var handle = SurfaceHandle.Headless;
        return CreateGpuSurface(info, handle, context, Math.Max(1, sampleCount), props);
    }

    public static SKSurface Create(SKImageInfo info, IntPtr pixels, int rowBytes)
        => Create(info, pixels, rowBytes, null);

    public static SKSurface Create(SKImageInfo info, IntPtr pixels, int rowBytes, SKSurfaceProperties? properties)
    {
        if (pixels == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(pixels));
        }

        if (info.BytesPerPixel is not 4 and not 8 and not 16)
        {
            throw new NotSupportedException("Only 32-bit, 64-bit, or 128-bit colour surfaces are currently supported.");
        }

        var stride = rowBytes <= 0 ? info.RowBytes : rowBytes;
        if (stride < info.RowBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(rowBytes), $"Row bytes must be at least {info.RowBytes}.");
        }

        var backend = new CpuPixelSurfaceBackend(pixels, stride);
        return new SKSurface(new Scene(), true, info, Matrix3x2.Identity, backend, properties);
    }

    public SKImage Snapshot()
    {
        ThrowIfDisposed();
        _canvas.Flush();
        return _backend.Snapshot(this, _scene);
    }

    public void Draw(SKCanvas canvas, float x, float y, SKPaint? paint)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(canvas);
        using var image = Snapshot();
        var dest = SKRect.Create(x, y, image.Width, image.Height);
        canvas.DrawImage(image, dest);
    }

    public void Flush() => Flush(true);

    public void Flush(bool submit, bool synchronous = false)
    {
        ThrowIfDisposed();
        _canvas.Flush();
        _backend.Flush(this, _scene, submit, synchronous);
        _context?.Flush(submit, synchronous);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _backend.Dispose();

        if (_ownsScene)
        {
            _scene.Dispose();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private RenderParams CreateRenderParams()
    {
        var width = checked((uint)Info.Width);
        var height = checked((uint)Info.Height);
        var antialias = ResolveAntialiasingMode();
        return new RenderParams(width, height, _baseColor, antialias, _renderFormat);
    }

    private static RenderFormat ResolveRenderFormat(SKColorType colorType) => colorType switch
    {
        SKColorType.Rgba8888 or SKColorType.RgbaF16 or SKColorType.RgbaF32 => RenderFormat.Rgba8,
        SKColorType.Bgra8888 => RenderFormat.Bgra8,
        _ => RenderFormat.Bgra8,
    };

    private AntialiasingMode ResolveAntialiasingMode()
    {
        var mode = AntialiasingMode.Area;

        if (_sampleCount >= 8)
        {
            mode = AntialiasingMode.Msaa16;
        }
        else if (_sampleCount >= 4)
        {
            mode = AntialiasingMode.Msaa8;
        }

        if (_properties is { } props)
        {
            mode = props.PixelGeometry switch
            {
                SKPixelGeometry.RgbVertical or SKPixelGeometry.BgrVertical => MaxAntialias(mode, AntialiasingMode.Msaa16),
                SKPixelGeometry.RgbHorizontal or SKPixelGeometry.BgrHorizontal => MaxAntialias(mode, AntialiasingMode.Msaa8),
                _ => mode,
            };
        }

        return mode;
    }

    private static AntialiasingMode MaxAntialias(AntialiasingMode a, AntialiasingMode b)
        => (AntialiasingMode)Math.Max((int)a, (int)b);

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SKSurface));
        }
    }

    private static SKSurface CreateGpuSurface(SKImageInfo info, SurfaceHandle handle, GRContext context, int sampleCount, SKSurfaceProperties? properties)
    {
        var backend = GpuSurfaceBackend.Create(info, handle, context, sampleCount);
        return new SKSurface(new Scene(), true, info, Matrix3x2.Identity, backend, properties, context, sampleCount);
    }

    private abstract class SurfaceBackend : IDisposable
    {
        public abstract void Flush(SKSurface owner, Scene scene, bool submit, bool synchronous);

        public abstract SKImage Snapshot(SKSurface owner, Scene scene);

        public virtual void Dispose()
        {
        }
    }

    private sealed class SceneSurfaceBackend : SurfaceBackend
    {
        public static SceneSurfaceBackend Instance { get; } = new SceneSurfaceBackend();

        private SceneSurfaceBackend()
        {
        }

        public override void Flush(SKSurface owner, Scene scene, bool submit, bool synchronous)
        {
            _ = owner;
            _ = scene;
            _ = submit;
            _ = synchronous;
        }

        public override SKImage Snapshot(SKSurface owner, Scene scene)
        {
            var info = owner.Info;
            var width = info.Width;
            var height = info.Height;
            if (width == 0 || height == 0)
            {
                throw new InvalidOperationException("Cannot snapshot an empty surface.");
            }

            using var renderer = new Renderer((uint)width, (uint)height);
            var stride = info.RowBytes;
            if (stride <= 0)
            {
                stride = width * InfoBytesPerPixel(info.ColorType);
            }

            var tempStride = checked(width * 4);
            var tempBuffer = new byte[checked(tempStride * height)];
            renderer.Render(scene, owner.CreateRenderParams(), tempBuffer, tempStride);
            var snapshotBuffer = new byte[checked(stride * height)];
            ConvertRenderedPixels(owner._renderFormat, tempBuffer, tempStride, snapshotBuffer, stride, info);
            return SKImage.FromPixels(info, snapshotBuffer, stride);
        }
    }

    private sealed class CpuPixelSurfaceBackend : SurfaceBackend
    {
        private readonly IntPtr _pixels;
        private readonly int _rowBytes;
        private Renderer? _renderer;

        public CpuPixelSurfaceBackend(IntPtr pixels, int rowBytes)
        {
            if (pixels == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(pixels));
            }

            if (rowBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rowBytes));
            }

            _pixels = pixels;
            _rowBytes = rowBytes;
        }

        public override void Flush(SKSurface owner, Scene scene, bool submit, bool synchronous)
        {
            _ = submit;
            _ = synchronous;
            FlushToPixels(owner, scene);
        }

        public override SKImage Snapshot(SKSurface owner, Scene scene)
        {
            FlushToPixels(owner, scene);
            var info = owner.Info;
            return SKImage.FromPixels(info, _pixels, _rowBytes);
        }

        public override void Dispose()
        {
            _renderer?.Dispose();
            _renderer = null;
        }

        private void FlushToPixels(SKSurface owner, Scene scene)
        {
            var info = owner.Info;
            if (info.Width <= 0 || info.Height <= 0)
            {
                return;
            }

            EnsureRenderer(info.Width, info.Height);
            var destination = SKImageInfo.SpanFromPointer(_pixels, info, _rowBytes);
            var renderParams = owner.CreateRenderParams();

            if (SupportsDirectWrite(info, renderParams.Format, _rowBytes))
            {
                _renderer!.Render(scene, renderParams, destination, _rowBytes);
                return;
            }

            var tempStride = checked(info.Width * 4);
            var totalBytes = checked(tempStride * info.Height);
            var buffer = ArrayPool<byte>.Shared.Rent(totalBytes);
            try
            {
                var span = buffer.AsSpan(0, totalBytes);
                _renderer!.Render(scene, renderParams, span, tempStride);
                ConvertRenderedPixels(renderParams.Format, span, tempStride, destination, _rowBytes, info);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private void EnsureRenderer(int width, int height)
        {
            if (_renderer is null)
            {
                _renderer = new Renderer((uint)width, (uint)height);
                return;
            }

            _renderer.Resize((uint)width, (uint)height);
        }

        private static bool SupportsDirectWrite(SKImageInfo info, RenderFormat format, int rowBytes)
        {
            if (rowBytes < info.RowBytes)
            {
                return false;
            }

            return info.ColorType switch
            {
                SKColorType.Bgra8888 => format == RenderFormat.Bgra8 && info.BytesPerPixel == 4,
                SKColorType.Rgba8888 => format == RenderFormat.Rgba8 && info.BytesPerPixel == 4,
                _ => false,
            };
        }
    }

    private sealed class GpuSurfaceBackend : SurfaceBackend
    {
        private readonly VelloSurfaceContext _context;
        private readonly VelloSurface _surface;
        private readonly VelloSurfaceRenderer _renderer;

        private GpuSurfaceBackend(VelloSurfaceContext context, VelloSurface surface, VelloSurfaceRenderer renderer)
        {
            _context = context;
            _surface = surface;
            _renderer = renderer;
        }

        public static GpuSurfaceBackend Create(SKImageInfo info, SurfaceHandle handle, GRContext context, int sampleCount)
        {
            var descriptor = new SurfaceDescriptor
            {
                Width = checked((uint)info.Width),
                Height = checked((uint)info.Height),
                PresentMode = PresentMode.AutoVsync,
                Handle = handle,
            };

            var rendererOptions = CreateRendererOptions(sampleCount, context.Options);

            var surfaceContext = new VelloSurfaceContext();
            var surface = new VelloSurface(surfaceContext, in descriptor);
            var renderer = new VelloSurfaceRenderer(surface, rendererOptions);
            return new GpuSurfaceBackend(surfaceContext, surface, renderer);
        }

        public override void Flush(SKSurface owner, Scene scene, bool submit, bool synchronous)
        {
            _renderer.Render(_surface, scene, owner.CreateRenderParams());
            _ = submit;
            _ = synchronous;
        }

        public override SKImage Snapshot(SKSurface owner, Scene scene)
            => SceneSurfaceBackend.Instance.Snapshot(owner, scene);

        public override void Dispose()
        {
            _renderer.Dispose();
            _surface.Dispose();
            _context.Dispose();
        }

        private static RendererOptions? CreateRendererOptions(int sampleCount, GRContextOptions options)
        {
            var supportMsaa8 = sampleCount >= 4;
            var supportMsaa16 = sampleCount >= 8 && !options.AvoidStencilBuffers;

            return new RendererOptions(
                useCpu: false,
                supportArea: true,
                supportMsaa8: supportMsaa8,
                supportMsaa16: supportMsaa16);
        }
    }

    private static void ConvertRenderedPixels(
        RenderFormat sourceFormat,
        ReadOnlySpan<byte> source,
        int sourceRowBytes,
        Span<byte> destination,
        int destinationRowBytes,
        SKImageInfo info)
    {
        var width = info.Width;
        var height = info.Height;
        var sourceIsRgba = sourceFormat == RenderFormat.Rgba8;

        var destPixelStride = info.BytesPerPixel;
        var destRowLength = info.RowBytes;
        var sourceStride = sourceRowBytes;
        var destinationStride = destinationRowBytes;

        for (var y = 0; y < height; y++)
        {
            var srcRow = source.Slice(y * sourceStride, Math.Min(sourceStride, source.Length - y * sourceStride));
            var dstRow = destination.Slice(y * destinationStride, Math.Min(destinationStride, destination.Length - y * destinationStride));

            switch (info.ColorType)
            {
                case SKColorType.Rgba8888:
                    ConvertToRgba8(srcRow, dstRow, width, sourceIsRgba);
                    break;

                case SKColorType.Bgra8888:
                    ConvertToBgra8(srcRow, dstRow, width, sourceIsRgba);
                    break;

                case SKColorType.RgbaF16:
                    ConvertToFloatPixels(srcRow, dstRow, width, destPixelStride, sourceIsRgba, isHalf: true);
                    break;

                case SKColorType.RgbaF32:
                    ConvertToFloatPixels(srcRow, dstRow, width, destPixelStride, sourceIsRgba, isHalf: false);
                    break;

                case SKColorType.Alpha8:
                    ConvertToAlpha8(srcRow, dstRow, width);
                    break;

                case SKColorType.Rgb565:
                    ConvertToRgb565(srcRow, dstRow, width, sourceIsRgba);
                    break;

                case SKColorType.Rgb888x:
                    ConvertToRgb888x(srcRow, dstRow, width, sourceIsRgba);
                    break;

                default:
                    throw new NotSupportedException($"Surfaces with colour type '{info.ColorType}' are not supported.");
            }

            if (destinationStride > destRowLength)
            {
                dstRow.Slice(destRowLength).Clear();
            }
        }
    }

    private static void ConvertToRgba8(ReadOnlySpan<byte> srcRow, Span<byte> dstRow, int width, bool sourceIsRgba)
    {
        for (var x = 0; x < width; x++)
        {
            var sourceIndex = x * 4;
            var destIndex = sourceIndex;

            byte r, g, b, a;
            if (sourceIsRgba)
            {
                r = srcRow[sourceIndex + 0];
                g = srcRow[sourceIndex + 1];
                b = srcRow[sourceIndex + 2];
            }
            else
            {
                b = srcRow[sourceIndex + 0];
                g = srcRow[sourceIndex + 1];
                r = srcRow[sourceIndex + 2];
            }

            a = srcRow[sourceIndex + 3];

            dstRow[destIndex + 0] = r;
            dstRow[destIndex + 1] = g;
            dstRow[destIndex + 2] = b;
            dstRow[destIndex + 3] = a;
        }
    }

    private static void ConvertToBgra8(ReadOnlySpan<byte> srcRow, Span<byte> dstRow, int width, bool sourceIsRgba)
    {
        for (var x = 0; x < width; x++)
        {
            var sourceIndex = x * 4;
            var destIndex = sourceIndex;

            byte r, g, b, a;
            if (sourceIsRgba)
            {
                r = srcRow[sourceIndex + 0];
                g = srcRow[sourceIndex + 1];
                b = srcRow[sourceIndex + 2];
            }
            else
            {
                b = srcRow[sourceIndex + 0];
                g = srcRow[sourceIndex + 1];
                r = srcRow[sourceIndex + 2];
            }

            a = srcRow[sourceIndex + 3];

            dstRow[destIndex + 0] = b;
            dstRow[destIndex + 1] = g;
            dstRow[destIndex + 2] = r;
            dstRow[destIndex + 3] = a;
        }
    }

    private static void ConvertToFloatPixels(ReadOnlySpan<byte> srcRow, Span<byte> dstRow, int width, int destPixelStride, bool sourceIsRgba, bool isHalf)
    {
        var inv = 1f / 255f;

        for (var x = 0; x < width; x++)
        {
            var sourceIndex = x * 4;
            var destIndex = x * destPixelStride;

            byte rByte, gByte, bByte, aByte;
            if (sourceIsRgba)
            {
                rByte = srcRow[sourceIndex + 0];
                gByte = srcRow[sourceIndex + 1];
                bByte = srcRow[sourceIndex + 2];
            }
            else
            {
                bByte = srcRow[sourceIndex + 0];
                gByte = srcRow[sourceIndex + 1];
                rByte = srcRow[sourceIndex + 2];
            }

            aByte = srcRow[sourceIndex + 3];

            if (isHalf)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(dstRow.Slice(destIndex + 0, 2), BitConverter.HalfToUInt16Bits((Half)(rByte * inv)));
                BinaryPrimitives.WriteUInt16LittleEndian(dstRow.Slice(destIndex + 2, 2), BitConverter.HalfToUInt16Bits((Half)(gByte * inv)));
                BinaryPrimitives.WriteUInt16LittleEndian(dstRow.Slice(destIndex + 4, 2), BitConverter.HalfToUInt16Bits((Half)(bByte * inv)));
                BinaryPrimitives.WriteUInt16LittleEndian(dstRow.Slice(destIndex + 6, 2), BitConverter.HalfToUInt16Bits((Half)(aByte * inv)));
            }
            else
            {
                BinaryPrimitives.WriteSingleLittleEndian(dstRow.Slice(destIndex + 0, 4), rByte * inv);
                BinaryPrimitives.WriteSingleLittleEndian(dstRow.Slice(destIndex + 4, 4), gByte * inv);
                BinaryPrimitives.WriteSingleLittleEndian(dstRow.Slice(destIndex + 8, 4), bByte * inv);
                BinaryPrimitives.WriteSingleLittleEndian(dstRow.Slice(destIndex + 12, 4), aByte * inv);
            }
        }
    }

    private static void ConvertToAlpha8(ReadOnlySpan<byte> srcRow, Span<byte> dstRow, int width)
    {
        for (var x = 0; x < width; x++)
        {
            var sourceIndex = x * 4;
            dstRow[x] = srcRow[sourceIndex + 3];
        }
    }

    private static void ConvertToRgb565(ReadOnlySpan<byte> srcRow, Span<byte> dstRow, int width, bool sourceIsRgba)
    {
        for (var x = 0; x < width; x++)
        {
            var sourceIndex = x * 4;
            var destIndex = x * 2;

            byte r = sourceIsRgba ? srcRow[sourceIndex + 0] : srcRow[sourceIndex + 2];
            byte g = srcRow[sourceIndex + 1];
            byte b = sourceIsRgba ? srcRow[sourceIndex + 2] : srcRow[sourceIndex + 0];

            var packed = (ushort)(((r & 0xF8) << 8) | ((g & 0xFC) << 3) | (b >> 3));
            BinaryPrimitives.WriteUInt16LittleEndian(dstRow.Slice(destIndex, 2), packed);
        }
    }

    private static void ConvertToRgb888x(ReadOnlySpan<byte> srcRow, Span<byte> dstRow, int width, bool sourceIsRgba)
    {
        for (var x = 0; x < width; x++)
        {
            var sourceIndex = x * 4;
            var destIndex = x * 4;

            dstRow[destIndex + 0] = sourceIsRgba ? srcRow[sourceIndex + 0] : srcRow[sourceIndex + 2];
            dstRow[destIndex + 1] = srcRow[sourceIndex + 1];
            dstRow[destIndex + 2] = sourceIsRgba ? srcRow[sourceIndex + 2] : srcRow[sourceIndex + 0];
            dstRow[destIndex + 3] = 255;
        }
    }

    private static int InfoBytesPerPixel(SKColorType colorType) => colorType switch
    {
        SKColorType.Alpha8 => 1,
        SKColorType.Rgb565 => 2,
        SKColorType.RgbaF32 => 16,
        SKColorType.RgbaF16 => 8,
        _ => 4,
    };
}
