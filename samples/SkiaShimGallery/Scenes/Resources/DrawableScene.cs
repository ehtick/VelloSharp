using System;
using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Resources;

internal sealed class DrawableScene : ISkiaGalleryScene
{
    public string Title => "Drawable: Custom Implementation";

    public string Description => "Invokes a custom SKDrawable, converts it to a picture, and replays it with transforms.";

    public SkiaSceneFeature Feature => SkiaSceneFeature.Resources;

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(250, 248, 252));

#if SKIA_SHIM
        using var drawable = new SampleDrawable();
        drawable.Draw(canvas);
        using var picture = drawable.ToPicture();
#else
        using var drawable = new SampleDrawable();
        drawable.Draw(canvas, 0, 0);
        using var picture = drawable.Snapshot();
#endif
        var matrix = SKMatrix.CreateTranslation(200, 40);
#if REAL_SKIA
        canvas.DrawPicture(picture, ref matrix);
#else
        canvas.DrawPicture(picture, matrix);
#endif

#if SKIA_SHIM
        using var snapshot = drawable.NewPictureSnapshot();
#else
        using var snapshot = drawable.Snapshot();
#endif
        if (snapshot is not null)
        {
            var cull = snapshot.CullRect;
            var cullCenterX = cull.Left + (cull.Right - cull.Left) * 0.5f;
            var cullCenterY = cull.Top + (cull.Bottom - cull.Top) * 0.5f;
            var rotated = SKMatrix.CreateRotationDegrees(15, cullCenterX, cullCenterY);
            rotated = rotated.PreConcat(SKMatrix.CreateTranslation(380, 60));
#if REAL_SKIA
            canvas.DrawPicture(snapshot, ref rotated);
#else
            canvas.DrawPicture(snapshot, rotated);
#endif
        }
    }

    private sealed partial class SampleDrawable : SKDrawable
    {
        private static readonly SKRect SampleBounds = SKRect.Create(0, 0, 120, 120);

#if SKIA_SHIM
        public override SKRect Bounds => SampleBounds;
        public override void Draw(SKCanvas canvas) => DrawCore(canvas);
#elif REAL_SKIA
        protected override void OnDraw(SKCanvas canvas) => DrawCore(canvas);
        protected override SKRect OnGetBounds() => SampleBounds;
#endif

        private static void DrawCore(SKCanvas canvas)
        {
            using var paint = new SKPaint
            {
                Color = new SKColor(130, 190, 255),
                IsAntialias = true,
            };
            canvas.DrawCircle(60, 60, 50, paint);

            using var stroke = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 6,
                Color = new SKColor(40, 80, 120),
                IsAntialias = true,
            };
            canvas.DrawRect(SKRect.Create(24, 24, 72, 72), stroke);
        }
    }
}
