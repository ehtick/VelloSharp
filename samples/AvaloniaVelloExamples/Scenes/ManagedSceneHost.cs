using System;
using System.Collections.Generic;
using VelloSharp;

namespace AvaloniaVelloExamples.Scenes;

public sealed class ManagedSceneHost : IDisposable
{
    private readonly SceneCollection _collection;
    private bool _disposed;

    private ManagedSceneHost(SceneCollection collection)
    {
        _collection = collection;
    }

    public static ManagedSceneHost Create(string? assetRoot)
    {
        var images = new ImageCache(assetRoot);
        var text = SimpleText.Create(assetRoot);
        var resources = new List<IDisposable>();
        var scenes = TestScenes.BuildScenes(images, text, assetRoot, resources);
        var collection = new SceneCollection(scenes, images, text, resources);
        return new ManagedSceneHost(collection);
    }

    public IReadOnlyList<ExampleScene> Scenes => _collection.Scenes;

    public ExampleRenderResult Render(
        int index,
        Scene target,
        double time,
        bool interactive,
        int complexity,
        System.Numerics.Matrix3x2 viewTransform)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ManagedSceneHost));
        }

        ArgumentNullException.ThrowIfNull(target);
        if (index < 0 || index >= _collection.Scenes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var sceneParams = new SceneParams(_collection.Images, _collection.Text)
        {
            Time = time,
            Interactive = interactive,
            Complexity = Math.Max(1, complexity),
            ViewTransform = viewTransform,
        };

        target.Reset();
        _collection.Scenes[index].Render(target, sceneParams);
        return new ExampleRenderResult(sceneParams.Resolution, sceneParams.BaseColor);
    }

    public ExampleRenderResult Render(
        int index,
        Scene target,
        double time,
        bool interactive,
        int complexity)
        => Render(index, target, time, interactive, complexity, System.Numerics.Matrix3x2.Identity);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _collection.Images.Dispose();
        _collection.Text.Dispose();
        foreach (var resource in _collection.Resources)
        {
            resource.Dispose();
        }
        _disposed = true;
    }
}
