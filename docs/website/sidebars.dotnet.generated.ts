import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = {
  "dotnetApi": [
    "index",
    {
      "type": "category",
      "label": "Avalonia.Winit",
      "items": [
        "Avalonia.Winit.IVelloWinitSurfaceProvider",
        "Avalonia.Winit.WinitApplicationExtensions",
        "Avalonia.Winit.WinitPlatformOptions"
      ],
      "link": {
        "type": "doc",
        "id": "Avalonia.Winit"
      }
    },
    {
      "type": "category",
      "label": "HarfBuzzSharp",
      "items": [
        "HarfBuzzSharp.Blob",
        "HarfBuzzSharp.Buffer",
        "HarfBuzzSharp.BufferDiffFlags",
        "HarfBuzzSharp.BufferFlags",
        "HarfBuzzSharp.ClusterLevel",
        "HarfBuzzSharp.CombiningClassDelegate",
        "HarfBuzzSharp.ComposeDelegate",
        "HarfBuzzSharp.ContentType",
        "HarfBuzzSharp.DecomposeDelegate",
        "HarfBuzzSharp.Direction",
        "HarfBuzzSharp.Face",
        "HarfBuzzSharp.Feature",
        "HarfBuzzSharp.Font",
        "HarfBuzzSharp.FontExtents",
        "HarfBuzzSharp.FontExtentsDelegate",
        "HarfBuzzSharp.FontFunctions",
        "HarfBuzzSharp.FontVariation",
        "HarfBuzzSharp.GeneralCategoryDelegate",
        "HarfBuzzSharp.GlyphAdvanceDelegate",
        "HarfBuzzSharp.GlyphAdvancesDelegate",
        "HarfBuzzSharp.GlyphContourPointDelegate",
        "HarfBuzzSharp.GlyphExtents",
        "HarfBuzzSharp.GlyphExtentsDelegate",
        "HarfBuzzSharp.GlyphFlags",
        "HarfBuzzSharp.GlyphFromNameDelegate",
        "HarfBuzzSharp.GlyphInfo",
        "HarfBuzzSharp.GlyphKerningDelegate",
        "HarfBuzzSharp.GlyphNameDelegate",
        "HarfBuzzSharp.GlyphOriginDelegate",
        "HarfBuzzSharp.GlyphPosition",
        "HarfBuzzSharp.IFaceTableProvider",
        "HarfBuzzSharp.Language",
        "HarfBuzzSharp.MemoryMode",
        "HarfBuzzSharp.MirroringDelegate",
        "HarfBuzzSharp.NativeObject",
        "HarfBuzzSharp.NominalGlyphDelegate",
        "HarfBuzzSharp.NominalGlyphsDelegate",
        "HarfBuzzSharp.OpenTypeMetrics",
        "HarfBuzzSharp.OpenTypeMetricsTag",
        "HarfBuzzSharp.OpenTypeVarAxisFlags",
        "HarfBuzzSharp.ReleaseDelegate",
        "HarfBuzzSharp.Script",
        "HarfBuzzSharp.ScriptDelegate",
        "HarfBuzzSharp.SerializeFlag",
        "HarfBuzzSharp.SerializeFormat",
        "HarfBuzzSharp.Tag",
        "HarfBuzzSharp.UnicodeCombiningClass",
        "HarfBuzzSharp.UnicodeFunctions",
        "HarfBuzzSharp.UnicodeGeneralCategory",
        "HarfBuzzSharp.VariationAxis",
        "HarfBuzzSharp.VariationGlyphDelegate"
      ],
      "link": {
        "type": "doc",
        "id": "HarfBuzzSharp"
      }
    },
    {
      "type": "category",
      "label": "SkiaSharp",
      "items": [
        "SkiaSharp.CpuSkiaBackendConfiguration",
        "SkiaSharp.SKAlphaType",
        "SkiaSharp.SKBitmap",
        "SkiaSharp.SKBitmapReleaseDelegate",
        "SkiaSharp.SKBlendMode",
        "SkiaSharp.SKCanvas",
        "SkiaSharp.SKClipOperation",
        "SkiaSharp.SKCodec",
        "SkiaSharp.SKColor",
        "SkiaSharp.SKColorF",
        "SkiaSharp.SKColorFilter",
        "SkiaSharp.SKColorSpace",
        "SkiaSharp.SKColorType",
        "SkiaSharp.SKColors",
        "SkiaSharp.SKCubicResampler",
        "SkiaSharp.SKData",
        "SkiaSharp.SKDocument",
        "SkiaSharp.SKDrawable",
        "SkiaSharp.SKEncodedImageFormat",
        "SkiaSharp.SKFilterMode",
        "SkiaSharp.SKFont",
        "SkiaSharp.SKFontEdging",
        "SkiaSharp.SKFontHinting",
        "SkiaSharp.SKFontManager",
        "SkiaSharp.SKFontStyle",
        "SkiaSharp.SKFontStyleSet",
        "SkiaSharp.SKFontStyleSlant",
        "SkiaSharp.SKFontStyleWeight",
        "SkiaSharp.SKFontStyleWidth",
        "SkiaSharp.SKImage",
        "SkiaSharp.SKImageCachingHint",
        "SkiaSharp.SKImageFilter",
        "SkiaSharp.SKImageInfo",
        "SkiaSharp.SKManagedStream",
        "SkiaSharp.SKMatrix",
        "SkiaSharp.SKMatrix44",
        "SkiaSharp.SKMatrix4x4",
        "SkiaSharp.SKMipmapMode",
        "SkiaSharp.SKPaint",
        "SkiaSharp.SKPaintStyle",
        "SkiaSharp.SKPath",
        "SkiaSharp.SKPath.Iterator",
        "SkiaSharp.SKPathArcSize",
        "SkiaSharp.SKPathDirection",
        "SkiaSharp.SKPathEffect",
        "SkiaSharp.SKPathFillType",
        "SkiaSharp.SKPathMeasure",
        "SkiaSharp.SKPathOp",
        "SkiaSharp.SKPathVerb",
        "SkiaSharp.SKPicture",
        "SkiaSharp.SKPictureRecorder",
        "SkiaSharp.SKPixelGeometry",
        "SkiaSharp.SKPixmap",
        "SkiaSharp.SKPoint",
        "SkiaSharp.SKRect",
        "SkiaSharp.SKRectI",
        "SkiaSharp.SKRegion",
        "SkiaSharp.SKRegionOperation",
        "SkiaSharp.SKRegionRectIterator",
        "SkiaSharp.SKRoundRect",
        "SkiaSharp.SKSamplingOptions",
        "SkiaSharp.SKShader",
        "SkiaSharp.SKShaderTileMode",
        "SkiaSharp.SKSizeI",
        "SkiaSharp.SKStreamAsset",
        "SkiaSharp.SKStrokeCap",
        "SkiaSharp.SKStrokeJoin",
        "SkiaSharp.SKSurface",
        "SkiaSharp.SKSurfaceProperties",
        "SkiaSharp.SKTextAlign",
        "SkiaSharp.SKTextBlob",
        "SkiaSharp.SKTextBlobBuilder",
        "SkiaSharp.SKTextBlobBuilder.PositionedRunBuffer",
        "SkiaSharp.SKTypeface"
      ],
      "link": {
        "type": "doc",
        "id": "SkiaSharp"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp",
      "items": [
        "VelloSharp.AccessKitActionRequest",
        "VelloSharp.AccessKitJson",
        "VelloSharp.AccessKitTreeUpdate",
        "VelloSharp.AdapterLuid",
        "VelloSharp.AntialiasingMode",
        "VelloSharp.Brush",
        "VelloSharp.BrushFactory",
        "VelloSharp.ExtendMode",
        "VelloSharp.FillRule",
        "VelloSharp.Font",
        "VelloSharp.Glyph",
        "VelloSharp.GlyphMetrics",
        "VelloSharp.GlyphRunOptions",
        "VelloSharp.GlyphRunStyle",
        "VelloSharp.GpuProfilerFrame",
        "VelloSharp.GpuProfilerSlice",
        "VelloSharp.GradientStop",
        "VelloSharp.IWinitEventHandler",
        "VelloSharp.Image",
        "VelloSharp.ImageAlphaMode",
        "VelloSharp.ImageBrush",
        "VelloSharp.ImageInfo",
        "VelloSharp.ImageQuality",
        "VelloSharp.KurboAffine",
        "VelloSharp.KurboPath",
        "VelloSharp.KurboPathElement",
        "VelloSharp.KurboPathVerb",
        "VelloSharp.KurboPoint",
        "VelloSharp.KurboRect",
        "VelloSharp.KurboVec2",
        "VelloSharp.LayerBlend",
        "VelloSharp.LayerCompose",
        "VelloSharp.LayerMix",
        "VelloSharp.LineCap",
        "VelloSharp.LineJoin",
        "VelloSharp.LinearGradientBrush",
        "VelloSharp.PathBuilder",
        "VelloSharp.PathElement",
        "VelloSharp.PathVerb",
        "VelloSharp.PenikoBrush",
        "VelloSharp.PenikoBrushAdapter",
        "VelloSharp.PenikoBrushKind",
        "VelloSharp.PenikoColorStop",
        "VelloSharp.PenikoExtend",
        "VelloSharp.PenikoGradientKind",
        "VelloSharp.PenikoLinearGradient",
        "VelloSharp.PenikoLinearGradientInfo",
        "VelloSharp.PenikoPoint",
        "VelloSharp.PenikoRadialGradient",
        "VelloSharp.PenikoRadialGradientInfo",
        "VelloSharp.PenikoStatus",
        "VelloSharp.PenikoSweepGradient",
        "VelloSharp.PenikoSweepGradientInfo",
        "VelloSharp.PresentMode",
        "VelloSharp.RadialGradientBrush",
        "VelloSharp.RenderFormat",
        "VelloSharp.RenderParams",
        "VelloSharp.Renderer",
        "VelloSharp.RendererOptions",
        "VelloSharp.RendererOptionsExtensions",
        "VelloSharp.RendererPipelineCache",
        "VelloSharp.RgbaColor",
        "VelloSharp.Scene",
        "VelloSharp.SharedGpuTexture",
        "VelloSharp.SolidColorBrush",
        "VelloSharp.SparseRenderContext",
        "VelloSharp.SparseRenderContextHandle",
        "VelloSharp.SparseRenderContextOptions",
        "VelloSharp.SparseRenderMode",
        "VelloSharp.SparseSimdLevel",
        "VelloSharp.StrokeStyle",
        "VelloSharp.SurfaceDescriptor",
        "VelloSharp.SurfaceHandle",
        "VelloSharp.SweepGradientBrush",
        "VelloSharp.VelatoComposition",
        "VelloSharp.VelatoCompositionInfo",
        "VelloSharp.VelatoRenderer",
        "VelloSharp.VelloAndroidNativeWindowHandle",
        "VelloSharp.VelloAppKitWindowHandle",
        "VelloSharp.VelloColor",
        "VelloSharp.VelloCoreAnimationLayerHandle",
        "VelloSharp.VelloCoreWindowHandle",
        "VelloSharp.VelloSurface",
        "VelloSharp.VelloSurfaceContext",
        "VelloSharp.VelloSurfaceRenderer",
        "VelloSharp.VelloSvg",
        "VelloSharp.VelloSwapChainPanelHandle",
        "VelloSharp.VelloWaylandWindowHandle",
        "VelloSharp.VelloWin32WindowHandle",
        "VelloSharp.VelloWindowHandle",
        "VelloSharp.VelloWindowHandleKind",
        "VelloSharp.VelloWindowHandlePayload",
        "VelloSharp.VelloXlibWindowHandle",
        "VelloSharp.WgpuAdapter",
        "VelloSharp.WgpuAdapterInfo",
        "VelloSharp.WgpuAddressMode",
        "VelloSharp.WgpuBackend",
        "VelloSharp.WgpuBackendType",
        "VelloSharp.WgpuBindGroup",
        "VelloSharp.WgpuBindGroupDescriptor",
        "VelloSharp.WgpuBindGroupEntry",
        "VelloSharp.WgpuBindGroupEntryType",
        "VelloSharp.WgpuBindGroupLayout",
        "VelloSharp.WgpuBindGroupLayoutDescriptor",
        "VelloSharp.WgpuBindGroupLayoutEntry",
        "VelloSharp.WgpuBindingLayoutType",
        "VelloSharp.WgpuBlendComponent",
        "VelloSharp.WgpuBlendFactor",
        "VelloSharp.WgpuBlendOperation",
        "VelloSharp.WgpuBlendState",
        "VelloSharp.WgpuBuffer",
        "VelloSharp.WgpuBufferBinding",
        "VelloSharp.WgpuBufferBindingLayout",
        "VelloSharp.WgpuBufferBindingType",
        "VelloSharp.WgpuBufferDescriptor",
        "VelloSharp.WgpuBufferUsage",
        "VelloSharp.WgpuColor",
        "VelloSharp.WgpuColorTargetState",
        "VelloSharp.WgpuColorWriteMask",
        "VelloSharp.WgpuCommandBuffer",
        "VelloSharp.WgpuCommandBufferDescriptor",
        "VelloSharp.WgpuCommandEncoder",
        "VelloSharp.WgpuCommandEncoderDescriptor",
        "VelloSharp.WgpuCompareFunction",
        "VelloSharp.WgpuCompositeAlphaMode",
        "VelloSharp.WgpuCullMode",
        "VelloSharp.WgpuDepthStencilState",
        "VelloSharp.WgpuDevice",
        "VelloSharp.WgpuDeviceDescriptor",
        "VelloSharp.WgpuDeviceType",
        "VelloSharp.WgpuDx12Compiler",
        "VelloSharp.WgpuExtent3D",
        "VelloSharp.WgpuFeature",
        "VelloSharp.WgpuFilterMode",
        "VelloSharp.WgpuFragmentState",
        "VelloSharp.WgpuFrontFace",
        "VelloSharp.WgpuImageCopyTexture",
        "VelloSharp.WgpuIndexFormat",
        "VelloSharp.WgpuInstance",
        "VelloSharp.WgpuInstanceOptions",
        "VelloSharp.WgpuLimitsPreset",
        "VelloSharp.WgpuLoadOp",
        "VelloSharp.WgpuMultisampleState",
        "VelloSharp.WgpuOrigin3D",
        "VelloSharp.WgpuPipelineCache",
        "VelloSharp.WgpuPipelineCacheDescriptor",
        "VelloSharp.WgpuPipelineLayout",
        "VelloSharp.WgpuPipelineLayoutDescriptor",
        "VelloSharp.WgpuPolygonMode",
        "VelloSharp.WgpuPowerPreference",
        "VelloSharp.WgpuPrimitiveState",
        "VelloSharp.WgpuPrimitiveTopology",
        "VelloSharp.WgpuQueue",
        "VelloSharp.WgpuRenderPass",
        "VelloSharp.WgpuRenderPassColorAttachment",
        "VelloSharp.WgpuRenderPassDepthStencilAttachment",
        "VelloSharp.WgpuRenderPassDescriptor",
        "VelloSharp.WgpuRenderPipeline",
        "VelloSharp.WgpuRenderPipelineDescriptor",
        "VelloSharp.WgpuRenderer",
        "VelloSharp.WgpuRequestAdapterOptions",
        "VelloSharp.WgpuSampler",
        "VelloSharp.WgpuSamplerBindingLayout",
        "VelloSharp.WgpuSamplerBindingType",
        "VelloSharp.WgpuSamplerDescriptor",
        "VelloSharp.WgpuShaderModule",
        "VelloSharp.WgpuShaderModuleDescriptor",
        "VelloSharp.WgpuShaderModuleSourceKind",
        "VelloSharp.WgpuShaderStage",
        "VelloSharp.WgpuStencilFaceState",
        "VelloSharp.WgpuStencilOperation",
        "VelloSharp.WgpuStorageTextureAccess",
        "VelloSharp.WgpuStorageTextureBindingLayout",
        "VelloSharp.WgpuStoreOp",
        "VelloSharp.WgpuSurface",
        "VelloSharp.WgpuSurfaceConfiguration",
        "VelloSharp.WgpuSurfaceTexture",
        "VelloSharp.WgpuTexture",
        "VelloSharp.WgpuTextureAspect",
        "VelloSharp.WgpuTextureBindingLayout",
        "VelloSharp.WgpuTextureDataLayout",
        "VelloSharp.WgpuTextureDescriptor",
        "VelloSharp.WgpuTextureDimension",
        "VelloSharp.WgpuTextureFormat",
        "VelloSharp.WgpuTextureSampleType",
        "VelloSharp.WgpuTextureUsage",
        "VelloSharp.WgpuTextureView",
        "VelloSharp.WgpuTextureViewDescriptor",
        "VelloSharp.WgpuTextureViewDimension",
        "VelloSharp.WgpuVertexAttribute",
        "VelloSharp.WgpuVertexBufferLayout",
        "VelloSharp.WgpuVertexFormat",
        "VelloSharp.WgpuVertexState",
        "VelloSharp.WgpuVertexStepMode",
        "VelloSharp.WinitAccessKitEventKind",
        "VelloSharp.WinitControlFlow",
        "VelloSharp.WinitCursorIcon",
        "VelloSharp.WinitElementState",
        "VelloSharp.WinitEventArgs",
        "VelloSharp.WinitEventKind",
        "VelloSharp.WinitEventLoop",
        "VelloSharp.WinitEventLoopContext",
        "VelloSharp.WinitKeyLocation",
        "VelloSharp.WinitModifiers",
        "VelloSharp.WinitMouseButton",
        "VelloSharp.WinitMouseScrollDeltaKind",
        "VelloSharp.WinitResizeDirection",
        "VelloSharp.WinitRunConfiguration",
        "VelloSharp.WinitStartCause",
        "VelloSharp.WinitStatus",
        "VelloSharp.WinitTouchPhaseKind",
        "VelloSharp.WinitWindow",
        "VelloSharp.WinitWindowButtons",
        "VelloSharp.WinitWindowLevel",
        "VelloSharp.WinitWindowOptions"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Avalonia.Controls",
      "items": [
        "VelloSharp.Avalonia.Controls.VelloAnimatedCanvasControl",
        "VelloSharp.Avalonia.Controls.VelloCanvasControl",
        "VelloSharp.Avalonia.Controls.VelloDrawEventArgs",
        "VelloSharp.Avalonia.Controls.VelloSvgControl"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Avalonia.Controls"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Avalonia.Vello",
      "items": [
        "VelloSharp.Avalonia.Vello.VelloApplicationExtensions",
        "VelloSharp.Avalonia.Vello.VelloPlatformOptions",
        "VelloSharp.Avalonia.Vello.VelloTextServices"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Avalonia.Vello"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Avalonia.Vello.Rendering",
      "items": [
        "VelloSharp.Avalonia.Vello.Rendering.IVelloApiLease",
        "VelloSharp.Avalonia.Vello.Rendering.IVelloApiLeaseFeature",
        "VelloSharp.Avalonia.Vello.Rendering.IVelloPlatformGraphicsLease",
        "VelloSharp.Avalonia.Vello.Rendering.WgpuSurfaceRenderContext"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Avalonia.Vello.Rendering"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.ChartData",
      "items": [
        "VelloSharp.ChartData.ChartDataBus",
        "VelloSharp.ChartData.ChartDataSlice",
        "VelloSharp.ChartData.ChartSamplePoint"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.ChartData"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.ChartDiagnostics",
      "items": [
        "VelloSharp.ChartDiagnostics.ChartMetric",
        "VelloSharp.ChartDiagnostics.DashboardTelemetrySink",
        "VelloSharp.ChartDiagnostics.FrameDiagnosticsCollector",
        "VelloSharp.ChartDiagnostics.FrameStats",
        "VelloSharp.ChartDiagnostics.IChartTelemetrySink"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.ChartDiagnostics"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.ChartEngine",
      "items": [
        "VelloSharp.ChartEngine.AnnotationZOrder",
        "VelloSharp.ChartEngine.AreaSeriesDefinition",
        "VelloSharp.ChartEngine.BarSeriesDefinition",
        "VelloSharp.ChartEngine.ChartAnimationProfile",
        "VelloSharp.ChartEngine.ChartAnimationTimeline",
        "VelloSharp.ChartEngine.ChartColor",
        "VelloSharp.ChartEngine.ChartComposition",
        "VelloSharp.ChartEngine.ChartCompositionBuilder",
        "VelloSharp.ChartEngine.ChartCursorUpdate",
        "VelloSharp.ChartEngine.ChartEngine",
        "VelloSharp.ChartEngine.ChartEngineOptions",
        "VelloSharp.ChartEngine.ChartFrameMetadata",
        "VelloSharp.ChartEngine.ChartFrameMetadata.AxisTickMetadata",
        "VelloSharp.ChartEngine.ChartFrameMetadata.ChartAnnotationOverlay",
        "VelloSharp.ChartEngine.ChartFrameMetadata.ChartCursorOverlay",
        "VelloSharp.ChartEngine.ChartFrameMetadata.ChartStreamingOverlay",
        "VelloSharp.ChartEngine.ChartFrameMetadata.PaneMetadata",
        "VelloSharp.ChartEngine.ChartFrameMetadata.SeriesMetadata",
        "VelloSharp.ChartEngine.ChartPaneBuilder",
        "VelloSharp.ChartEngine.ChartPaneDefinition",
        "VelloSharp.ChartEngine.ChartSeriesDefinition",
        "VelloSharp.ChartEngine.ChartSeriesKind",
        "VelloSharp.ChartEngine.ChartSeriesOverride",
        "VelloSharp.ChartEngine.ChartStreamingAnimationPreset",
        "VelloSharp.ChartEngine.ChartStreamingEventKind",
        "VelloSharp.ChartEngine.ChartStreamingUpdate",
        "VelloSharp.ChartEngine.CompositionAnnotationLayer",
        "VelloSharp.ChartEngine.HeatmapSeriesDefinition",
        "VelloSharp.ChartEngine.LineSeriesDefinition",
        "VelloSharp.ChartEngine.PolylineBandSeriesDefinition",
        "VelloSharp.ChartEngine.ScatterSeriesDefinition"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.ChartEngine"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.ChartEngine.Annotations",
      "items": [
        "VelloSharp.ChartEngine.Annotations.AnnotationCalloutPlacement",
        "VelloSharp.ChartEngine.Annotations.AnnotationKind",
        "VelloSharp.ChartEngine.Annotations.AnnotationSnapMode",
        "VelloSharp.ChartEngine.Annotations.CalloutAnnotation",
        "VelloSharp.ChartEngine.Annotations.ChartAnnotation",
        "VelloSharp.ChartEngine.Annotations.GradientZoneAnnotation",
        "VelloSharp.ChartEngine.Annotations.HorizontalLineAnnotation",
        "VelloSharp.ChartEngine.Annotations.TimeRangeAnnotation",
        "VelloSharp.ChartEngine.Annotations.ValueZoneAnnotation",
        "VelloSharp.ChartEngine.Annotations.VerticalLineAnnotation"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.ChartEngine.Annotations"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.ChartRuntime",
      "items": [
        "VelloSharp.ChartRuntime.FrameTick",
        "VelloSharp.ChartRuntime.FrameTickCallback",
        "VelloSharp.ChartRuntime.IFrameTickSource",
        "VelloSharp.ChartRuntime.RenderScheduler"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.ChartRuntime"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.ChartRuntime.Windows.WinForms",
      "items": [
        "VelloSharp.ChartRuntime.Windows.WinForms.WinFormsTickSource"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.ChartRuntime.Windows.WinForms"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.ChartRuntime.Windows.Wpf",
      "items": [
        "VelloSharp.ChartRuntime.Windows.Wpf.WpfCompositionTargetTickSource"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.ChartRuntime.Windows.Wpf"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Charting.Avalonia",
      "items": [
        "VelloSharp.Charting.Avalonia.ChartView"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Charting.Avalonia"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Charting.Axis",
      "items": [
        "VelloSharp.Charting.Axis.AxisComposer",
        "VelloSharp.Charting.Axis.AxisDefinition",
        "VelloSharp.Charting.Axis.AxisRenderModel",
        "VelloSharp.Charting.Axis.AxisRenderSurface"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Charting.Axis"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Charting.Coordinates",
      "items": [
        "VelloSharp.Charting.Coordinates.ChartPoint"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Charting.Coordinates"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Charting.Layout",
      "items": [
        "VelloSharp.Charting.Layout.AxisLayout",
        "VelloSharp.Charting.Layout.AxisLayoutRequest",
        "VelloSharp.Charting.Layout.AxisOrientation",
        "VelloSharp.Charting.Layout.ChartLayoutEngine",
        "VelloSharp.Charting.Layout.ChartLayoutPreset",
        "VelloSharp.Charting.Layout.ChartLayoutRequest",
        "VelloSharp.Charting.Layout.ChartLayoutResult",
        "VelloSharp.Charting.Layout.LayoutGallery",
        "VelloSharp.Charting.Layout.LayoutRect"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Charting.Layout"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Charting.Legend",
      "items": [
        "VelloSharp.Charting.Legend.LegendDefinition",
        "VelloSharp.Charting.Legend.LegendItem",
        "VelloSharp.Charting.Legend.LegendItemVisual",
        "VelloSharp.Charting.Legend.LegendOrientation",
        "VelloSharp.Charting.Legend.LegendPosition",
        "VelloSharp.Charting.Legend.LegendRenderer",
        "VelloSharp.Charting.Legend.LegendVisual"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Charting.Legend"
      }
    },
    "VelloSharp.Charting.Primitives",
    {
      "type": "category",
      "label": "VelloSharp.Charting.Rendering",
      "items": [
        "VelloSharp.Charting.Rendering.AxisLabelVisual",
        "VelloSharp.Charting.Rendering.AxisLineVisual",
        "VelloSharp.Charting.Rendering.AxisRenderResult",
        "VelloSharp.Charting.Rendering.AxisRenderer",
        "VelloSharp.Charting.Rendering.AxisTickVisual",
        "VelloSharp.Charting.Rendering.AxisVisual",
        "VelloSharp.Charting.Rendering.ChartOverlayRenderer",
        "VelloSharp.Charting.Rendering.TextAlignment"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Charting.Rendering"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Charting.Scales",
      "items": [
        "VelloSharp.Charting.Scales.IScale",
        "VelloSharp.Charting.Scales.LinearScale",
        "VelloSharp.Charting.Scales.LogarithmicScale",
        "VelloSharp.Charting.Scales.ScaleKind",
        "VelloSharp.Charting.Scales.TimeScale"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Charting.Scales"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Charting.Styling",
      "items": [
        "VelloSharp.Charting.Styling.AxisStyle",
        "VelloSharp.Charting.Styling.ChartPalette",
        "VelloSharp.Charting.Styling.ChartTheme",
        "VelloSharp.Charting.Styling.ChartTypography",
        "VelloSharp.Charting.Styling.LegendStyle",
        "VelloSharp.Charting.Styling.RgbaColor",
        "VelloSharp.Charting.Styling.ValueColorGradient"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Charting.Styling"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Charting.Styling.Configuration",
      "items": [
        "VelloSharp.Charting.Styling.Configuration.AxisStyleDefinition",
        "VelloSharp.Charting.Styling.Configuration.ChartPaletteDefinition",
        "VelloSharp.Charting.Styling.Configuration.ChartThemeDefinition",
        "VelloSharp.Charting.Styling.Configuration.ChartThemeLoader",
        "VelloSharp.Charting.Styling.Configuration.ChartThemeRegistry",
        "VelloSharp.Charting.Styling.Configuration.ChartTypographyDefinition",
        "VelloSharp.Charting.Styling.Configuration.LegendStyleDefinition"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Charting.Styling.Configuration"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Charting.Ticks",
      "items": [
        "VelloSharp.Charting.Ticks.AxisTickGeneratorRegistry",
        "VelloSharp.Charting.Ticks.AxisTickInfo",
        "VelloSharp.Charting.Ticks.LinearTickGenerator",
        "VelloSharp.Charting.Ticks.TimeTickGenerator"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Charting.Ticks"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Charting.Units",
      "items": [
        "VelloSharp.Charting.Units.UnitRange"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Charting.Units"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Charting.WinForms",
      "items": [
        "VelloSharp.Charting.WinForms.ChartView"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Charting.WinForms"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Charting.Wpf",
      "items": [
        "VelloSharp.Charting.Wpf.ChartView"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Charting.Wpf"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Composition",
      "items": [
        "VelloSharp.Composition.ColumnSlice",
        "VelloSharp.Composition.ColumnViewportMetrics",
        "VelloSharp.Composition.CompositionColor",
        "VelloSharp.Composition.CompositionInterop",
        "VelloSharp.Composition.CompositionInterop.CompositionVirtualizer",
        "VelloSharp.Composition.CompositionInterop.LinearLayoutChild",
        "VelloSharp.Composition.CompositionInterop.LinearLayoutResult",
        "VelloSharp.Composition.CompositionMaterialDescriptor",
        "VelloSharp.Composition.CompositionMaterialRegistry",
        "VelloSharp.Composition.CompositionShaderDescriptor",
        "VelloSharp.Composition.CompositionShaderKind",
        "VelloSharp.Composition.CompositionShaderRegistry",
        "VelloSharp.Composition.DirtyRegion",
        "VelloSharp.Composition.DockLayoutChild",
        "VelloSharp.Composition.DockLayoutOptions",
        "VelloSharp.Composition.DockSide",
        "VelloSharp.Composition.FrozenKind",
        "VelloSharp.Composition.GridLayoutChild",
        "VelloSharp.Composition.GridLayoutOptions",
        "VelloSharp.Composition.GridTrack",
        "VelloSharp.Composition.GridTrackKind",
        "VelloSharp.Composition.LabelMetrics",
        "VelloSharp.Composition.LayoutAlignment",
        "VelloSharp.Composition.LayoutConstraints",
        "VelloSharp.Composition.LayoutOrientation",
        "VelloSharp.Composition.LayoutRect",
        "VelloSharp.Composition.LayoutSize",
        "VelloSharp.Composition.LayoutThickness",
        "VelloSharp.Composition.PlotArea",
        "VelloSharp.Composition.RenderLayer",
        "VelloSharp.Composition.RowAction",
        "VelloSharp.Composition.RowPlanEntry",
        "VelloSharp.Composition.RowViewportMetrics",
        "VelloSharp.Composition.RowWindow",
        "VelloSharp.Composition.ScalarConstraint",
        "VelloSharp.Composition.SceneCache",
        "VelloSharp.Composition.ScenePartitioner",
        "VelloSharp.Composition.StackLayoutChild",
        "VelloSharp.Composition.StackLayoutOptions",
        "VelloSharp.Composition.TimelineDirtyBinding",
        "VelloSharp.Composition.TimelineDirtyKind",
        "VelloSharp.Composition.TimelineEasing",
        "VelloSharp.Composition.TimelineEasingTrackDescriptor",
        "VelloSharp.Composition.TimelineGroupConfig",
        "VelloSharp.Composition.TimelineRepeat",
        "VelloSharp.Composition.TimelineSample",
        "VelloSharp.Composition.TimelineSampleFlags",
        "VelloSharp.Composition.TimelineSpringTrackDescriptor",
        "VelloSharp.Composition.TimelineSystem",
        "VelloSharp.Composition.VirtualColumnStrip",
        "VelloSharp.Composition.VirtualRowMetric",
        "VelloSharp.Composition.VirtualizerTelemetry",
        "VelloSharp.Composition.WrapLayoutChild",
        "VelloSharp.Composition.WrapLayoutLine",
        "VelloSharp.Composition.WrapLayoutOptions",
        "VelloSharp.Composition.WrapLayoutSolveResult"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Composition"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Composition.Accessibility",
      "items": [
        "VelloSharp.Composition.Accessibility.AccessibilityAction",
        "VelloSharp.Composition.Accessibility.AccessibilityActionEventArgs",
        "VelloSharp.Composition.Accessibility.AccessibilityAnnouncementEventArgs",
        "VelloSharp.Composition.Accessibility.AccessibilityChangedEventArgs",
        "VelloSharp.Composition.Accessibility.AccessibilityLiveSetting",
        "VelloSharp.Composition.Accessibility.AccessibilityProperties",
        "VelloSharp.Composition.Accessibility.AccessibilityRole"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Composition.Accessibility"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Composition.Controls",
      "items": [
        "VelloSharp.Composition.Controls.AccessText",
        "VelloSharp.Composition.Controls.Border",
        "VelloSharp.Composition.Controls.Button",
        "VelloSharp.Composition.Controls.CheckBox",
        "VelloSharp.Composition.Controls.CompositionElement",
        "VelloSharp.Composition.Controls.CompositionTemplate",
        "VelloSharp.Composition.Controls.ContentControl",
        "VelloSharp.Composition.Controls.Decorator",
        "VelloSharp.Composition.Controls.DropDown",
        "VelloSharp.Composition.Controls.Ellipse",
        "VelloSharp.Composition.Controls.GeometryPresenter",
        "VelloSharp.Composition.Controls.InputControl",
        "VelloSharp.Composition.Controls.Panel",
        "VelloSharp.Composition.Controls.Path",
        "VelloSharp.Composition.Controls.RadioButton",
        "VelloSharp.Composition.Controls.Rectangle",
        "VelloSharp.Composition.Controls.Shape",
        "VelloSharp.Composition.Controls.TabControl",
        "VelloSharp.Composition.Controls.TabItem",
        "VelloSharp.Composition.Controls.TemplatedControl",
        "VelloSharp.Composition.Controls.TextBlock",
        "VelloSharp.Composition.Controls.TextBox",
        "VelloSharp.Composition.Controls.UserControl",
        "VelloSharp.Composition.Controls.VisualTreeVirtualizer",
        "VelloSharp.Composition.Controls.VisualTreeVirtualizer.VirtualizationPlan"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Composition.Controls"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Composition.Input",
      "items": [
        "VelloSharp.Composition.Input.CompositionKeyEventArgs",
        "VelloSharp.Composition.Input.CompositionPointerEventArgs",
        "VelloSharp.Composition.Input.CompositionTextInputEventArgs",
        "VelloSharp.Composition.Input.ICompositionInputSink",
        "VelloSharp.Composition.Input.ICompositionInputSource",
        "VelloSharp.Composition.Input.InputModifiers",
        "VelloSharp.Composition.Input.KeyEventType",
        "VelloSharp.Composition.Input.PointerButton",
        "VelloSharp.Composition.Input.PointerDeviceKind",
        "VelloSharp.Composition.Input.PointerEventType"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Composition.Input"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Composition.Telemetry",
      "items": [
        "VelloSharp.Composition.Telemetry.CommandBroker",
        "VelloSharp.Composition.Telemetry.CommandRequest",
        "VelloSharp.Composition.Telemetry.CommandResult",
        "VelloSharp.Composition.Telemetry.CommandStatus",
        "VelloSharp.Composition.Telemetry.GaugeTelemetryConnector",
        "VelloSharp.Composition.Telemetry.ICommandHandler",
        "VelloSharp.Composition.Telemetry.IGaugeTelemetryConsumer",
        "VelloSharp.Composition.Telemetry.ITelemetryObserver",
        "VelloSharp.Composition.Telemetry.ScadaTelemetryRouter",
        "VelloSharp.Composition.Telemetry.TelemetryHub",
        "VelloSharp.Composition.Telemetry.TelemetryQuality",
        "VelloSharp.Composition.Telemetry.TelemetrySample"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Composition.Telemetry"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Editor",
      "items": [
        "VelloSharp.Editor.EditorRuntime"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Editor"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Ffi.Gpu",
      "items": [
        "VelloSharp.Ffi.Gpu.VelloRendererHandle",
        "VelloSharp.Ffi.Gpu.VelloSceneHandle"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Ffi.Gpu"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Gauges",
      "items": [
        "VelloSharp.Gauges.GaugeModule"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Gauges"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Integration.Avalonia",
      "items": [
        "VelloSharp.Integration.Avalonia.AppBuilderExtensions",
        "VelloSharp.Integration.Avalonia.AvaloniaCompositionInputSource",
        "VelloSharp.Integration.Avalonia.VelloRenderFrameContext",
        "VelloSharp.Integration.Avalonia.VelloSurfaceView",
        "VelloSharp.Integration.Avalonia.VelloView"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Integration.Avalonia"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Integration.Skia",
      "items": [
        "VelloSharp.Integration.Skia.SkiaRenderBridge"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Integration.Skia"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Maui",
      "items": [
        "VelloSharp.Maui.Resource",
        "VelloSharp.Maui.VelloViewHandler"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Maui"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Maui.Controls",
      "items": [
        "VelloSharp.Maui.Controls.IVelloView",
        "VelloSharp.Maui.Controls.IVelloViewHandler",
        "VelloSharp.Maui.Controls.VelloView"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Maui.Controls"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Maui.Diagnostics",
      "items": [
        "VelloSharp.Maui.Diagnostics.VelloDiagnosticsChangedEventArgs",
        "VelloSharp.Maui.Diagnostics.VelloDiagnosticsSnapshot",
        "VelloSharp.Maui.Diagnostics.VelloViewDiagnostics"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Maui.Diagnostics"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Maui.Events",
      "items": [
        "VelloSharp.Maui.Events.VelloSurfaceRenderEventArgs"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Maui.Events"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Maui.Hosting",
      "items": [
        "VelloSharp.Maui.Hosting.AppHostBuilderExtensions"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Maui.Hosting"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Maui.Input",
      "items": [
        "VelloSharp.Maui.Input.MauiCompositionInputSource"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Maui.Input"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Maui.Rendering",
      "items": [
        "VelloSharp.Maui.Rendering.RenderLoopDriver",
        "VelloSharp.Maui.Rendering.VelloGraphicsDeviceOptions",
        "VelloSharp.Maui.Rendering.VelloRenderBackend",
        "VelloSharp.Maui.Rendering.VelloRenderMode"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Maui.Rendering"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Rendering",
      "items": [
        "VelloSharp.Rendering.RenderTargetDescriptor",
        "VelloSharp.Rendering.VelloRenderPath"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Rendering"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Scada",
      "items": [
        "VelloSharp.Scada.ScadaRuntime"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Scada"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Text",
      "items": [
        "VelloSharp.Text.ParleyFontInfo",
        "VelloSharp.Text.ParleyFontQuery",
        "VelloSharp.Text.ParleyFontService",
        "VelloSharp.Text.ParleyVariationAxis",
        "VelloSharp.Text.VelloFontStyle",
        "VelloSharp.Text.VelloGlyph",
        "VelloSharp.Text.VelloOpenTypeFeature",
        "VelloSharp.Text.VelloTextShaperCore",
        "VelloSharp.Text.VelloTextShaperOptions",
        "VelloSharp.Text.VelloVariationAxisValue"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Text"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.TreeDataGrid",
      "items": [
        "VelloSharp.TreeDataGrid.TreeAnimationTimeline",
        "VelloSharp.TreeDataGrid.TreeBufferAdoptionDiagnostics",
        "VelloSharp.TreeDataGrid.TreeChromeVisual",
        "VelloSharp.TreeDataGrid.TreeColor",
        "VelloSharp.TreeDataGrid.TreeColumnMetric",
        "VelloSharp.TreeDataGrid.TreeColumnSlice",
        "VelloSharp.TreeDataGrid.TreeColumnSpan",
        "VelloSharp.TreeDataGrid.TreeDataModel",
        "VelloSharp.TreeDataGrid.TreeFrameStats",
        "VelloSharp.TreeDataGrid.TreeFrozenKind",
        "VelloSharp.TreeDataGrid.TreeGpuTimestampSummary",
        "VelloSharp.TreeDataGrid.TreeGroupHeaderVisual",
        "VelloSharp.TreeDataGrid.TreeModelDiff",
        "VelloSharp.TreeDataGrid.TreeModelDiffKind",
        "VelloSharp.TreeDataGrid.TreeNodeDescriptor",
        "VelloSharp.TreeDataGrid.TreeNodeMetadata",
        "VelloSharp.TreeDataGrid.TreeRenderLoop",
        "VelloSharp.TreeDataGrid.TreeRowAction",
        "VelloSharp.TreeDataGrid.TreeRowAnimationProfile",
        "VelloSharp.TreeDataGrid.TreeRowAnimationSnapshot",
        "VelloSharp.TreeDataGrid.TreeRowKind",
        "VelloSharp.TreeDataGrid.TreeRowMetric",
        "VelloSharp.TreeDataGrid.TreeRowPlanEntry",
        "VelloSharp.TreeDataGrid.TreeRowVisual",
        "VelloSharp.TreeDataGrid.TreeRowWindow",
        "VelloSharp.TreeDataGrid.TreeSelectionDiff",
        "VelloSharp.TreeDataGrid.TreeSelectionMode",
        "VelloSharp.TreeDataGrid.TreeSpringAnimationTrack",
        "VelloSharp.TreeDataGrid.TreeSummaryVisual",
        "VelloSharp.TreeDataGrid.TreeViewportMetrics",
        "VelloSharp.TreeDataGrid.TreeVirtualizationPlan",
        "VelloSharp.TreeDataGrid.TreeVirtualizationScheduler",
        "VelloSharp.TreeDataGrid.TreeVirtualizationTelemetry"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.TreeDataGrid"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.TreeDataGrid.Composition",
      "items": [
        "VelloSharp.TreeDataGrid.Composition.TreeColumnDefinition",
        "VelloSharp.TreeDataGrid.Composition.TreeColumnLayoutAnimator",
        "VelloSharp.TreeDataGrid.Composition.TreeColumnPaneDiff",
        "VelloSharp.TreeDataGrid.Composition.TreeColumnPaneSnapshot",
        "VelloSharp.TreeDataGrid.Composition.TreeColumnSizingMode",
        "VelloSharp.TreeDataGrid.Composition.TreeColumnSlot",
        "VelloSharp.TreeDataGrid.Composition.TreeColumnStripCache",
        "VelloSharp.TreeDataGrid.Composition.TreeColumnStripSnapshot",
        "VelloSharp.TreeDataGrid.Composition.TreeNodeLayoutEngine"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.TreeDataGrid.Composition"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.TreeDataGrid.Rendering",
      "items": [
        "VelloSharp.TreeDataGrid.Rendering.TreeMaterialDescriptor",
        "VelloSharp.TreeDataGrid.Rendering.TreeMaterialRegistry",
        "VelloSharp.TreeDataGrid.Rendering.TreePaneSceneBatch",
        "VelloSharp.TreeDataGrid.Rendering.TreePaneSceneBatchSet",
        "VelloSharp.TreeDataGrid.Rendering.TreeRenderHookDescriptor",
        "VelloSharp.TreeDataGrid.Rendering.TreeRenderHookKind",
        "VelloSharp.TreeDataGrid.Rendering.TreeRenderHookRegistry",
        "VelloSharp.TreeDataGrid.Rendering.TreeSceneGraph",
        "VelloSharp.TreeDataGrid.Rendering.TreeShaderDescriptor",
        "VelloSharp.TreeDataGrid.Rendering.TreeShaderKind",
        "VelloSharp.TreeDataGrid.Rendering.TreeShaderRegistry",
        "VelloSharp.TreeDataGrid.Rendering.TreeTemplatePaneBatcher"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.TreeDataGrid.Rendering"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.TreeDataGrid.Templates",
      "items": [
        "VelloSharp.TreeDataGrid.Templates.ITreeTemplateBackend",
        "VelloSharp.TreeDataGrid.Templates.TreeColumnContext",
        "VelloSharp.TreeDataGrid.Templates.TreeCompiledTemplate",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateBindings",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateBuilder",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateCache",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateCacheKey",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateCompileOptions",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateCompiler",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateInstruction",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateManagedBackend",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateNativeBackend",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateNodeKind",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateOpCode",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateRuntime",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateRuntimeContext",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateRuntimeHandle",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateStackOrientation",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateValue",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateValueKind"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.TreeDataGrid.Templates"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Uno.Controls",
      "items": [
        "VelloSharp.Uno.Controls.VelloCoreWindowHost",
        "VelloSharp.Uno.Controls.VelloSwapChainPanel",
        "VelloSharp.Uno.Controls.VelloXamlIslandSwapChainHost"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Uno.Controls"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Uwp.Controls",
      "items": [
        "VelloSharp.Uwp.Controls.VelloSwapChainPanel"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Uwp.Controls"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.WinForms",
      "items": [
        "VelloSharp.WinForms.VelloBitmap",
        "VelloSharp.WinForms.VelloBrush",
        "VelloSharp.WinForms.VelloFont",
        "VelloSharp.WinForms.VelloGraphics",
        "VelloSharp.WinForms.VelloGraphicsPath",
        "VelloSharp.WinForms.VelloGraphicsState",
        "VelloSharp.WinForms.VelloLinearGradientBrush",
        "VelloSharp.WinForms.VelloPathGradientBrush",
        "VelloSharp.WinForms.VelloPen",
        "VelloSharp.WinForms.VelloRegion",
        "VelloSharp.WinForms.VelloSolidBrush",
        "VelloSharp.WinForms.VelloStringFormat",
        "VelloSharp.WinForms.VelloTextureBrush"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.WinForms"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.WinForms.Integration",
      "items": [
        "VelloSharp.WinForms.Integration.VelloPaintSurfaceEventArgs",
        "VelloSharp.WinForms.Integration.VelloPaintSurfaceEventArgsExtensions",
        "VelloSharp.WinForms.Integration.VelloRenderBackend",
        "VelloSharp.WinForms.Integration.VelloRenderControl",
        "VelloSharp.WinForms.Integration.VelloRenderMode"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.WinForms.Integration"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Windows",
      "items": [
        "VelloSharp.Windows.IWindowsSurfaceSource",
        "VelloSharp.Windows.PresentMode",
        "VelloSharp.Windows.RenderLoopDriver",
        "VelloSharp.Windows.VelloGraphicsDevice",
        "VelloSharp.Windows.VelloGraphicsDeviceOptions",
        "VelloSharp.Windows.VelloGraphicsSession",
        "VelloSharp.Windows.VelloSurfaceRenderEventArgs",
        "VelloSharp.Windows.WindowsColorSpace",
        "VelloSharp.Windows.WindowsGpuContext",
        "VelloSharp.Windows.WindowsGpuContextLease",
        "VelloSharp.Windows.WindowsGpuDiagnostics",
        "VelloSharp.Windows.WindowsSurfaceDescriptor",
        "VelloSharp.Windows.WindowsSurfaceFactory",
        "VelloSharp.Windows.WindowsSurfaceKind",
        "VelloSharp.Windows.WindowsSurfaceSize",
        "VelloSharp.Windows.WindowsSwapChainSurface"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Windows"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Windows.Controls",
      "items": [
        "VelloSharp.Windows.Controls.VelloCompositionControl",
        "VelloSharp.Windows.Controls.VelloSwapChainControl"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Windows.Controls"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Windows.Hosting",
      "items": [
        "VelloSharp.Windows.Hosting.HostBuilderExtensions",
        "VelloSharp.Windows.Hosting.VelloWinUIOptions",
        "VelloSharp.Windows.Hosting.VelloWinUIService"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Windows.Hosting"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Windows.Shared.Contracts",
      "items": [
        "VelloSharp.Windows.Shared.Contracts.VelloRenderBackend",
        "VelloSharp.Windows.Shared.Contracts.VelloRenderMode"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Windows.Shared.Contracts"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Windows.Shared.Diagnostics",
      "items": [
        "VelloSharp.Windows.Shared.Diagnostics.IVelloDiagnosticsProvider",
        "VelloSharp.Windows.Shared.Diagnostics.VelloDiagnosticsChangedEventArgs"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Windows.Shared.Diagnostics"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Windows.Shared.Dispatching",
      "items": [
        "VelloSharp.Windows.Shared.Dispatching.IVelloCompositionTarget",
        "VelloSharp.Windows.Shared.Dispatching.IVelloCompositionTargetProvider",
        "VelloSharp.Windows.Shared.Dispatching.IVelloWindowsDispatcher",
        "VelloSharp.Windows.Shared.Dispatching.IVelloWindowsDispatcherProvider",
        "VelloSharp.Windows.Shared.Dispatching.IVelloWindowsDispatcherTimer",
        "VelloSharp.Windows.Shared.Dispatching.VelloCompositionTarget",
        "VelloSharp.Windows.Shared.Dispatching.VelloWindowsDispatcher"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Windows.Shared.Dispatching"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Windows.Shared.Presenters",
      "items": [
        "VelloSharp.Windows.Shared.Presenters.IVelloSwapChainPresenterHost",
        "VelloSharp.Windows.Shared.Presenters.VelloSwapChainPresenter",
        "VelloSharp.Windows.Shared.Presenters.VelloSwapChainRenderEventArgs"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Windows.Shared.Presenters"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Wpf.Integration",
      "items": [
        "VelloSharp.Wpf.Integration.SwapChainLeaseEventArgs",
        "VelloSharp.Wpf.Integration.VelloNativeSwapChainHost",
        "VelloSharp.Wpf.Integration.VelloNativeSwapChainView",
        "VelloSharp.Wpf.Integration.VelloRenderBackend",
        "VelloSharp.Wpf.Integration.VelloRenderMode",
        "VelloSharp.Wpf.Integration.VelloSwapChainRenderEventArgs",
        "VelloSharp.Wpf.Integration.VelloView",
        "VelloSharp.Wpf.Integration.VelloViewDiagnostics",
        "VelloSharp.Wpf.Integration.WpfCompositionInputSource"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Wpf.Integration"
      }
    }
  ]
};

export default sidebars;
