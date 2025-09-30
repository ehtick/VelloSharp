using System;

namespace SkiaSharp;

public abstract class SKDrawable : IDisposable
{
    public abstract void Draw(SKCanvas canvas);

    public virtual SKRect Bounds => new(0, 0, 0, 0);

    public virtual void Dispose()
    {
    }

    public SKPicture ToPicture()
    {
        return ToPicture(Bounds);
    }

    public SKPicture ToPicture(SKRect cullRect)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(cullRect);
        Draw(canvas);
        return recorder.EndRecording();
    }

    public virtual SKPicture? NewPictureSnapshot()
    {
        return ToPicture(Bounds);
    }
}
