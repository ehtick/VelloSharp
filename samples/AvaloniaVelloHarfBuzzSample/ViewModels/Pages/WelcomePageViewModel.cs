using System;
using System.Numerics;
using AvaloniaVelloHarfBuzzSample.Rendering;
using AvaloniaVelloHarfBuzzSample.Services;
using AvaloniaVelloHarfBuzzSample.Views.Pages;
using VelloSharp;

namespace AvaloniaVelloHarfBuzzSample.ViewModels.Pages;

public sealed class WelcomePageViewModel : SamplePageViewModel
{
    public WelcomePageViewModel(HarfBuzzSampleServices services)
        : base(
            services,
            "Welcome",
            "Start here to understand the goals of the HarfBuzz sample and shared tooling.",
            "\uE17A",
            () => new WelcomePage())
    {
        Renderer = new WelcomeLeaseRenderer(services);
    }

    public IHarfBuzzLeaseRenderer Renderer { get; }

    public string Introduction =>
        "The HarfBuzz gallery demonstrates how the Vello lease pipeline shapes and renders text across scripts, " +
        "fonts, and feature configurations. Use the navigation on the left to explore specific scenarios.";

    private sealed class WelcomeLeaseRenderer : IHarfBuzzLeaseRenderer
    {
        private readonly HarfBuzzSampleServices _services;
        private FontAssetService.FontAssetReference? _roboto;
        private FontAssetService.FontAssetReference? _mono;
        private FontAssetService.FontAssetReference? _emoji;
        private bool _captureNoted;

        public WelcomeLeaseRenderer(HarfBuzzSampleServices services)
        {
            _services = services;
        }

        public void Render(HarfBuzzLeaseRenderContext context)
        {
            EnsureFonts();

            var scene = context.Scene;
            var globalTransform = context.GlobalTransform;
            var shapeService = context.ShapeService;

            var heading = shapeService.ShapeText(
                _roboto!,
                "HarfBuzz meets Vello",
                48f,
                new ShapeTextOptions
                {
                    Brush = new SolidColorBrush(RgbaColor.FromBytes(255, 245, 235)),
                    Transform = Matrix3x2.CreateTranslation(36f, 86f),
                });
            heading.Render(scene, globalTransform);

            var tagline = shapeService.ShapeText(
                _mono!,
                "Lease-driven shaping pipeline · multi-script coverage · diagnostics ready",
                20f,
                new ShapeTextOptions
                {
                    Brush = new SolidColorBrush(RgbaColor.FromBytes(184, 198, 220)),
                    Transform = Matrix3x2.CreateTranslation(40f, 140f),
                    UnitsPerEmScale = 64f,
                });
            tagline.Render(scene, globalTransform);

            var emoji = shapeService.ShapeText(
                _emoji!,
                "😀  😎  🚀",
                36f,
                new ShapeTextOptions
                {
                    Brush = new SolidColorBrush(RgbaColor.FromBytes(255, 214, 120)),
                    Transform = Matrix3x2.CreateTranslation(40f, 188f),
                });
            emoji.Render(scene, globalTransform);

            if (!_captureNoted && context.FrameIndex == 0)
            {
                try
                {
                    context.CaptureRecorder.Capture(heading, "welcome-heading");
                }
                catch (Exception)
                {
                    // Diagnostics capture is best effort; ignore failures during startup.
                }

                _captureNoted = true;
            }
        }

        private void EnsureFonts()
        {
            if (_roboto is null)
            {
                _roboto = _services.FontAssets.GetFontAsync("Roboto-Regular.ttf").GetAwaiter().GetResult();
            }

            if (_mono is null)
            {
                _mono = _services.FontAssets.GetFontAsync("Inconsolata.ttf").GetAwaiter().GetResult();
            }

            if (_emoji is null)
            {
                _emoji = _services.FontAssets.GetFontAsync("NotoColorEmoji-CBTF-Subset.ttf").GetAwaiter().GetResult();
            }
        }
    }
}
