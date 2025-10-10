namespace SkiaGallery.SharedScenes;

public enum SkiaSceneFeature
{
    CoreCanvas,
    Paint,
    Gradients,
    BlendModes,
    ImageCodecs,
    Text,
    Geometry,
    Resources,
}

public static class SkiaSceneFeatureExtensions
{
    public static string ToDisplayName(this SkiaSceneFeature feature) => feature switch
    {
        SkiaSceneFeature.CoreCanvas => "Core Canvas",
        SkiaSceneFeature.Paint => "Paint & Stroke",
        SkiaSceneFeature.Gradients => "Gradients",
        SkiaSceneFeature.BlendModes => "Blend Modes",
        SkiaSceneFeature.ImageCodecs => "Image Codecs",
        SkiaSceneFeature.Text => "Text & Hinting",
        SkiaSceneFeature.Geometry => "Geometry",
        SkiaSceneFeature.Resources => "Resources",
        _ => feature.ToString(),
    };
}
