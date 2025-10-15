using System;
using System.Collections.Generic;
using AvaloniaVelloSkiaSharpSample.Diagnostics;
using AvaloniaVelloSkiaSharpSample.Rendering;
using AvaloniaVelloSkiaSharpSample.Services;
using MiniMvvm;
using SkiaSharp;
using SampleSkiaBackendService = AvaloniaVelloSkiaSharpSample.Services.SkiaBackendService;

namespace AvaloniaVelloSkiaSharpSample.ViewModels.Pages;

public sealed class RuntimeEffectForgeViewModel : SamplePageViewModel
{
    private readonly IReadOnlyList<ShaderPresetOption> _presets =
    [
        new("Aurora Waves", """
uniform float u_time;
uniform float u_intensity;
uniform float2 u_resolution;

half4 main(float2 coord) {
    float2 uv = coord / u_resolution;
    float wave = sin((uv.x + u_time) * 6.2831) * 0.5 +
                 sin((uv.y + u_time * 1.3) * 8.0) * 0.35;
    wave *= u_intensity;

    half3 baseColor = half3(0.1 + uv.y * 0.7, 0.25 + wave, 0.55 + uv.x * 0.3);
    return half4(baseColor, 1.0);
}
"""),
        new("Plasma Bands", """
uniform float u_time;
uniform float u_intensity;
uniform float2 u_resolution;

half4 main(float2 coord) {
    float2 uv = coord / u_resolution;
    float t = u_time * 0.8;
    float bands = sin((uv.x + uv.y * 0.6 + t) * 12.0);
    bands += sin((uv.y - t) * 16.0);
    bands *= u_intensity;
    half3 color = half3(0.35 + 0.35 * sin(bands + 2.0),
                        0.25 + 0.4 * sin(bands + 4.0),
                        0.55 + 0.45 * sin(bands));
    return half4(color, 1.0);
}
""")
    ];

    private ShaderPresetOption _selectedPreset;
    private double _intensity = 0.7;
    private double _speed = 1.0;
    private bool _animate = true;
    private string _compilationStatus = string.Empty;
    private string _shaderSource = string.Empty;

    private SKRuntimeEffect? _effect;

    public RuntimeEffectForgeViewModel(
        SkiaCaptureRecorder? captureRecorder = null,
        SampleSkiaBackendService? backendService = null,
        SkiaResourceService? resourceService = null)
        : base(
            "Runtime Effect Forge",
            "Toggle minimal SkSL shaders, uniforms, and fallback diagnostics for the shimmed runtime effect pipeline.",
            null,
            captureRecorder,
            backendService,
            resourceService)
    {
        _selectedPreset = _presets[0];
        _shaderSource = _selectedPreset.Source;

        CompileShaderCommand = MiniCommand.Create(CompileShader);
        ResetShaderCommand = MiniCommand.Create(ResetShaderToPreset);

        CompileEffect(_shaderSource);
    }

    protected override string CaptureLabel => "runtime-effect-forge";

    public IReadOnlyList<ShaderPresetOption> Presets => _presets;

    public string ShaderSource
    {
        get => _shaderSource;
        set
        {
            if (RaiseAndSetIfChanged(ref _shaderSource, value))
            {
                RaisePropertyChanged(nameof(HasShaderChanges));
                RaisePropertyChanged(nameof(CanCompile));
            }
        }
    }

    public bool HasShaderChanges => !string.Equals(_shaderSource, SelectedPreset.Source, StringComparison.Ordinal);

    public bool CanCompile => !string.IsNullOrWhiteSpace(_shaderSource);

    public MiniCommand CompileShaderCommand { get; }

    public MiniCommand ResetShaderCommand { get; }

    public ShaderPresetOption SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetAndRequestRender(ref _selectedPreset, value))
            {
                ShaderSource = value.Source;
                CompileEffect(ShaderSource);
            }
        }
    }

    public double Intensity
    {
        get => _intensity;
        set => SetAndRequestRender(ref _intensity, Math.Clamp(value, 0.1, 2.0));
    }

    public double Speed
    {
        get => _speed;
        set => SetAndRequestRender(ref _speed, Math.Clamp(value, 0.0, 2.5));
    }

    public bool Animate
    {
        get => _animate;
        set => SetAndRequestRender(ref _animate, value);
    }

    public string CompilationStatus
    {
        get => _compilationStatus;
        private set => RaiseAndSetIfChanged(ref _compilationStatus, value);
    }

    public bool EffectAvailable => _effect is not null;

    public override void Render(in SkiaLeaseRenderContext context)
    {
        var canvas = context.Canvas;
        var info = context.ImageInfo;

        canvas.Clear(new SKColor(10, 14, 26, 255));

        using var backgroundPaint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(info.Width * 0.5f, info.Height * 0.5f),
                info.Width * 0.65f,
                new[]
                {
                    new SKColor(20, 30, 52, 255),
                    new SKColor(10, 14, 24, 255),
                },
                null,
                SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(SKRect.Create(0, 0, info.Width, info.Height), backgroundPaint);

        if (_effect is null)
        {
            DrawFallbackMessage(canvas, info);
            ProcessCapture(context);
            return;
        }

        var uniforms = new SKRuntimeEffectUniforms(_effect)
        {
            ["u_time"] = (float)(context.Elapsed.TotalSeconds * Speed),
            ["u_intensity"] = (float)Intensity,
            ["u_resolution"] = new SKPoint(info.Width, info.Height),
        };

        SKShader? shader = null;
        try
        {
            shader = _effect.ToShader(uniforms);
        }
        catch (Exception ex)
        {
            CompilationStatus = $"Shader activation failed: {ex.Message}";
            _effect.Dispose();
            _effect = null;
            RaisePropertyChanged(nameof(EffectAvailable));
            DrawFallbackMessage(canvas, info);
            ProcessCapture(context);
            return;
        }

        using var effectPaint = new SKPaint
        {
            Shader = shader,
            IsAntialias = true,
        };
        canvas.DrawRoundRect(
            new SKRect(info.Width * 0.1f, info.Height * 0.12f, info.Width * 0.9f, info.Height * 0.88f),
            36,
            36,
            effectPaint);
        shader?.Dispose();

        using var framePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(255, 255, 255, 60),
            StrokeWidth = 2f,
            IsAntialias = true,
        };

        canvas.DrawRoundRect(
            new SKRect(info.Width * 0.12f, info.Height * 0.14f, info.Width * 0.88f, info.Height * 0.86f),
            30,
            30,
            framePaint);

        if (Animate)
        {
            RequestRender();
        }

        ProcessCapture(context);
    }

    private void DrawFallbackMessage(SKCanvas canvas, SKImageInfo info)
    {
        using var captionPaint = new SKPaint
        {
            Color = new SKColor(236, 242, 255, 210),
            TextSize = 20f,
            Typeface = SKTypeface.Default,
            IsAntialias = true,
        };

        var message = string.IsNullOrEmpty(CompilationStatus)
            ? "Runtime effects are not available on this backend."
            : CompilationStatus;

        canvas.DrawText(message, info.Width * 0.08f, info.Height * 0.5f, captionPaint);
    }

    private void CompileShader()
    {
        CompileEffect(ShaderSource);
    }

    private void ResetShaderToPreset()
    {
        ShaderSource = SelectedPreset.Source;
        CompileEffect(ShaderSource);
    }

    private void CompileEffect(string? source)
    {
        _effect?.Dispose();
        _effect = null;

        if (string.IsNullOrWhiteSpace(source))
        {
            CompilationStatus = "Provide SkSL source and compile to preview the effect.";
            RaisePropertyChanged(nameof(EffectAvailable));
            RequestRender();
            return;
        }

        CompilationStatus = "Compiling shader…";
        RaisePropertyChanged(nameof(EffectAvailable));

        try
        {
            var effect = SKRuntimeEffect.CreateShader(source, out var errors);
            if (effect is null)
            {
                CompilationStatus = string.IsNullOrWhiteSpace(errors)
                    ? "Runtime effect backend rejected the shader."
                    : errors!;
                RaisePropertyChanged(nameof(EffectAvailable));
                RequestRender();
                return;
            }

            _effect = effect;
            CompilationStatus = $"Shader compiled successfully at {DateTime.Now:T}.";
            RaisePropertyChanged(nameof(EffectAvailable));
            RequestRender();
        }
        catch (Exception ex)
        {
            CompilationStatus = $"Compilation failed: {ex.Message}";
            RaisePropertyChanged(nameof(EffectAvailable));
            RequestRender();
        }
    }

    public sealed record ShaderPresetOption(string Name, string Source);
}
