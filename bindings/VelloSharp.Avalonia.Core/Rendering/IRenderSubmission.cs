namespace VelloSharp.Avalonia.Core.Rendering;

/// <summary>
/// Submits recorded scenes to backend-specific render pipelines.
/// </summary>
public interface IRenderSubmission
{
    /// <summary>
    /// Submits a scene for rendering.
    /// </summary>
    /// <param name="context">Context describing the target device and surface.</param>
    /// <param name="scene">The recorded scene to render.</param>
    /// <param name="commandCallbacks">
    /// Optional callbacks that can encode backend-specific work (compute passes, extra drawing).
    /// </param>
    void SubmitScene(
        in RenderSubmissionContext context,
        Scene scene,
        IReadOnlyList<Action<IGraphicsCommandEncoder>>? commandCallbacks = null);
}
