using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.Controls;

public sealed class SkiaCanvas : Control
{
    public static readonly StyledProperty<ISkiaGalleryScene?> SceneProperty =
        AvaloniaProperty.Register<SkiaCanvas, ISkiaGalleryScene?>(nameof(Scene));

    static SkiaCanvas()
    {
        AffectsRender<SkiaCanvas>(SceneProperty);
    }

    public ISkiaGalleryScene? Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Scene is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        context.Custom(new SkiaDrawOperation(this, Scene, Bounds));
    }

    private sealed class SkiaDrawOperation : ICustomDrawOperation
    {
        private readonly SkiaCanvas _owner;
        private readonly ISkiaGalleryScene _scene;
        private readonly Rect _bounds;

        public SkiaDrawOperation(SkiaCanvas owner, ISkiaGalleryScene scene, Rect bounds)
        {
            _owner = owner;
            _scene = scene;
            _bounds = bounds;
        }

        public Rect Bounds => _bounds;

        public void Dispose()
        {
        }

        public bool HitTest(Point p) => _bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) is not ISkiaSharpApiLeaseFeature feature)
            {
                return;
            }

            using var lease = feature.Lease();
            if (lease?.SkCanvas is null)
            {
                return;
            }

            var bounds = _bounds;
            var scaling = _owner.VisualRoot?.RenderScaling ?? 1.0;
            if (scaling <= 0)
            {
                scaling = 1.0;
            }

            var pixelWidth = Math.Max(1, (int)Math.Ceiling(bounds.Width * scaling));
            var pixelHeight = Math.Max(1, (int)Math.Ceiling(bounds.Height * scaling));
            if (pixelWidth <= 0 || pixelHeight <= 0)
            {
                return;
            }

            using var surface = SKSurface.Create(new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul));
            var surfaceCanvas = surface.Canvas;
            surfaceCanvas.Clear(SKColors.Transparent);
            surfaceCanvas.Scale((float)scaling);

            var info = new SKImageInfo(
                Math.Max(1, (int)Math.Round(bounds.Width)),
                Math.Max(1, (int)Math.Round(bounds.Height)),
                SKColorType.Bgra8888,
                SKAlphaType.Premul);

            _scene.Render(surfaceCanvas, info);
            surfaceCanvas.Flush();

            var canvas = lease.SkCanvas;
            var left = (float)(bounds.X * scaling);
            var top = (float)(bounds.Y * scaling);
            var right = left + pixelWidth;
            var bottom = top + pixelHeight;

            canvas.Save();
            canvas.ClipRect(new SKRect(left, top, right, bottom));
            canvas.DrawSurface(surface, left, top);
            canvas.Restore();
        }
    }
}
