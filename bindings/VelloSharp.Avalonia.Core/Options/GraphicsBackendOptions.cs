using System.Collections.ObjectModel;
using System.Linq;

namespace VelloSharp.Avalonia.Core.Options;

/// <summary>
/// Top-level configuration consumed by Avalonia host applications when selecting a renderer.
/// </summary>
public sealed class GraphicsBackendOptions
{
    private readonly IReadOnlyList<GraphicsBackendKind> _preferredBackends;

    /// <summary>
    /// Initializes a new instance of <see cref="GraphicsBackendOptions"/>.
    /// </summary>
    public GraphicsBackendOptions(
        IEnumerable<GraphicsBackendKind> preferredBackends,
        GraphicsFeatureSet features,
        GraphicsPresentationOptions presentation)
    {
        if (preferredBackends is null)
        {
            throw new ArgumentNullException(nameof(preferredBackends));
        }

        Features = features ?? throw new ArgumentNullException(nameof(features));
        Presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));

        _preferredBackends = preferredBackends as IReadOnlyList<GraphicsBackendKind>
                             ?? new ReadOnlyCollection<GraphicsBackendKind>(preferredBackends.ToArray());
        if (_preferredBackends.Count == 0)
        {
            throw new ArgumentException("At least one backend preference must be specified.", nameof(preferredBackends));
        }
    }

    /// <summary>
    /// Gets the ordered list of preferred backends.
    /// </summary>
    public IReadOnlyList<GraphicsBackendKind> PreferredBackends => _preferredBackends;

    /// <summary>
    /// Gets the feature toggle configuration.
    /// </summary>
    public GraphicsFeatureSet Features { get; }

    /// <summary>
    /// Gets the presentation preferences shared by all devices.
    /// </summary>
    public GraphicsPresentationOptions Presentation { get; }

    /// <summary>
    /// Provides a canonical configuration favouring Vello first, then Skia GPU, then Skia CPU.
    /// </summary>
    public static GraphicsBackendOptions Default { get; } = new(
        new[]
        {
            GraphicsBackendKind.VelloWgpu,
            GraphicsBackendKind.SkiaGpu,
            GraphicsBackendKind.SkiaCpu,
        },
        new GraphicsFeatureSet(),
        GraphicsPresentationOptions.Default);

    /// <summary>
    /// Creates device options for the requested backend using the shared features and presentation.
    /// </summary>
    public GraphicsDeviceOptions ToDeviceOptions(GraphicsBackendKind backend) =>
        new(backend, Features, Presentation);
}
