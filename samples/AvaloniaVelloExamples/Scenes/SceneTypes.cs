using System;
using System.Collections.Generic;
using System.Numerics;
using VelloSharp;

namespace AvaloniaVelloExamples.Scenes;

public sealed class SceneParams
{
    public SceneParams(ImageCache images, SimpleText text)
    {
        Images = images ?? throw new ArgumentNullException(nameof(images));
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public double Time { get; set; }
    public bool Interactive { get; set; }
    public ImageCache Images { get; }
    public SimpleText Text { get; }
    public Vector2? Resolution { get; set; }
    public RgbaColor? BaseColor { get; set; }
    public int Complexity { get; set; }
    public Matrix3x2 ViewTransform { get; set; } = Matrix3x2.Identity;
}

public sealed class ExampleScene
{
    public ExampleScene(string name, bool animated, Action<Scene, SceneParams> render)
    {
        Name = name;
        Animated = animated;
        Render = render ?? throw new ArgumentNullException(nameof(render));
    }

    public string Name { get; }
    public bool Animated { get; }
    public Action<Scene, SceneParams> Render { get; }
}

public sealed class SceneCollection
{
    public SceneCollection(IReadOnlyList<ExampleScene> scenes, ImageCache images, SimpleText text)
    {
        Scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
        Images = images ?? throw new ArgumentNullException(nameof(images));
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public IReadOnlyList<ExampleScene> Scenes { get; }
    public ImageCache Images { get; }
    public SimpleText Text { get; }
}

public readonly struct ExampleRenderResult
{
    public ExampleRenderResult(Vector2? resolution, RgbaColor? baseColor)
    {
        Resolution = resolution;
        BaseColor = baseColor;
    }

    public Vector2? Resolution { get; }
    public RgbaColor? BaseColor { get; }
}
