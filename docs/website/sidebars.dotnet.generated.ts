import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = {
  "dotnetApi": [
    "index",
    {
      "type": "category",
      "label": "Avalonia.Winit",
      "items": [
        "Avalonia.Winit.WinitApplicationExtensions",
        "Avalonia.Winit.WinitPlatformOptions",
        "Avalonia.Winit.IVelloWinitSurfaceProvider"
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
        "HarfBuzzSharp.Face",
        "HarfBuzzSharp.Font",
        "HarfBuzzSharp.FontFunctions",
        "HarfBuzzSharp.Language",
        "HarfBuzzSharp.NativeObject",
        "HarfBuzzSharp.OpenTypeMetrics",
        "HarfBuzzSharp.UnicodeFunctions",
        "HarfBuzzSharp.Feature",
        "HarfBuzzSharp.FontExtents",
        "HarfBuzzSharp.FontVariation",
        "HarfBuzzSharp.GlyphExtents",
        "HarfBuzzSharp.GlyphInfo",
        "HarfBuzzSharp.GlyphPosition",
        "HarfBuzzSharp.Script",
        "HarfBuzzSharp.Tag",
        "HarfBuzzSharp.VariationAxis",
        "HarfBuzzSharp.IFaceTableProvider",
        "HarfBuzzSharp.BufferDiffFlags",
        "HarfBuzzSharp.BufferFlags",
        "HarfBuzzSharp.ClusterLevel",
        "HarfBuzzSharp.ContentType",
        "HarfBuzzSharp.Direction",
        "HarfBuzzSharp.GlyphFlags",
        "HarfBuzzSharp.MemoryMode",
        "HarfBuzzSharp.OpenTypeMetricsTag",
        "HarfBuzzSharp.OpenTypeVarAxisFlags",
        "HarfBuzzSharp.SerializeFlag",
        "HarfBuzzSharp.SerializeFormat",
        "HarfBuzzSharp.UnicodeCombiningClass",
        "HarfBuzzSharp.UnicodeGeneralCategory",
        "HarfBuzzSharp.CombiningClassDelegate",
        "HarfBuzzSharp.ComposeDelegate",
        "HarfBuzzSharp.DecomposeDelegate",
        "HarfBuzzSharp.FontExtentsDelegate",
        "HarfBuzzSharp.GeneralCategoryDelegate",
        "HarfBuzzSharp.GlyphAdvanceDelegate",
        "HarfBuzzSharp.GlyphAdvancesDelegate",
        "HarfBuzzSharp.GlyphContourPointDelegate",
        "HarfBuzzSharp.GlyphExtentsDelegate",
        "HarfBuzzSharp.GlyphFromNameDelegate",
        "HarfBuzzSharp.GlyphKerningDelegate",
        "HarfBuzzSharp.GlyphNameDelegate",
        "HarfBuzzSharp.GlyphOriginDelegate",
        "HarfBuzzSharp.MirroringDelegate",
        "HarfBuzzSharp.NominalGlyphDelegate",
        "HarfBuzzSharp.NominalGlyphsDelegate",
        "HarfBuzzSharp.ReleaseDelegate",
        "HarfBuzzSharp.ScriptDelegate",
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
        "SkiaSharp.SKBitmap",
        "SkiaSharp.SKCanvas",
        "SkiaSharp.SKCodec",
        "SkiaSharp.SKColorFilter",
        "SkiaSharp.SKColorSpace",
        "SkiaSharp.SKColors",
        "SkiaSharp.SKData",
        "SkiaSharp.SKDocument",
        "SkiaSharp.SKDrawable",
        "SkiaSharp.SKFont",
        "SkiaSharp.SKFontManager",
        "SkiaSharp.SKFontStyle",
        "SkiaSharp.SKFontStyleSet",
        "SkiaSharp.SKImage",
        "SkiaSharp.SKImageFilter",
        "SkiaSharp.SKManagedStream",
        "SkiaSharp.SKPaint",
        "SkiaSharp.SKPath",
        "SkiaSharp.SKPath.Iterator",
        "SkiaSharp.SKPathEffect",
        "SkiaSharp.SKPathMeasure",
        "SkiaSharp.SKPicture",
        "SkiaSharp.SKPictureRecorder",
        "SkiaSharp.SKPixmap",
        "SkiaSharp.SKRegion",
        "SkiaSharp.SKRegionRectIterator",
        "SkiaSharp.SKRoundRect",
        "SkiaSharp.SKShader",
        "SkiaSharp.SKStreamAsset",
        "SkiaSharp.SKSurface",
        "SkiaSharp.SKSurfaceProperties",
        "SkiaSharp.SKTextBlob",
        "SkiaSharp.SKTextBlobBuilder",
        "SkiaSharp.SKTypeface",
        "SkiaSharp.SKColor",
        "SkiaSharp.SKColorF",
        "SkiaSharp.SKCubicResampler",
        "SkiaSharp.SKImageInfo",
        "SkiaSharp.SKMatrix",
        "SkiaSharp.SKMatrix44",
        "SkiaSharp.SKMatrix4x4",
        "SkiaSharp.SKPoint",
        "SkiaSharp.SKRect",
        "SkiaSharp.SKRectI",
        "SkiaSharp.SKSamplingOptions",
        "SkiaSharp.SKSizeI",
        "SkiaSharp.SKTextBlobBuilder.PositionedRunBuffer",
        "SkiaSharp.SKAlphaType",
        "SkiaSharp.SKBlendMode",
        "SkiaSharp.SKClipOperation",
        "SkiaSharp.SKColorType",
        "SkiaSharp.SKEncodedImageFormat",
        "SkiaSharp.SKFilterMode",
        "SkiaSharp.SKFontEdging",
        "SkiaSharp.SKFontHinting",
        "SkiaSharp.SKFontStyleSlant",
        "SkiaSharp.SKFontStyleWeight",
        "SkiaSharp.SKFontStyleWidth",
        "SkiaSharp.SKImageCachingHint",
        "SkiaSharp.SKMipmapMode",
        "SkiaSharp.SKPaintStyle",
        "SkiaSharp.SKPathArcSize",
        "SkiaSharp.SKPathDirection",
        "SkiaSharp.SKPathFillType",
        "SkiaSharp.SKPathOp",
        "SkiaSharp.SKPathVerb",
        "SkiaSharp.SKPixelGeometry",
        "SkiaSharp.SKRegionOperation",
        "SkiaSharp.SKShaderTileMode",
        "SkiaSharp.SKStrokeCap",
        "SkiaSharp.SKStrokeJoin",
        "SkiaSharp.SKTextAlign",
        "SkiaSharp.SKBitmapReleaseDelegate"
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
        "VelloSharp.Brush",
        "VelloSharp.BrushFactory",
        "VelloSharp.Font",
        "VelloSharp.GlyphRunOptions",
        "VelloSharp.Image",
        "VelloSharp.ImageBrush",
        "VelloSharp.KurboPath",
        "VelloSharp.LinearGradientBrush",
        "VelloSharp.PathBuilder",
        "VelloSharp.PenikoBrush",
        "VelloSharp.PenikoBrushAdapter",
        "VelloSharp.RadialGradientBrush",
        "VelloSharp.Renderer",
        "VelloSharp.RendererOptionsExtensions",
        "VelloSharp.Scene",
        "VelloSharp.SharedGpuTexture",
        "VelloSharp.SolidColorBrush",
        "VelloSharp.SparseRenderContext",
        "VelloSharp.SparseRenderContextHandle",
        "VelloSharp.SparseRenderContextOptions",
        "VelloSharp.StrokeStyle",
        "VelloSharp.SweepGradientBrush",
        "VelloSharp.VelatoComposition",
        "VelloSharp.VelatoRenderer",
        "VelloSharp.VelloSurface",
        "VelloSharp.VelloSurfaceContext",
        "VelloSharp.VelloSurfaceRenderer",
        "VelloSharp.VelloSvg",
        "VelloSharp.WgpuAdapter",
        "VelloSharp.WgpuBindGroup",
        "VelloSharp.WgpuBindGroupLayout",
        "VelloSharp.WgpuBuffer",
        "VelloSharp.WgpuCommandBuffer",
        "VelloSharp.WgpuCommandEncoder",
        "VelloSharp.WgpuDevice",
        "VelloSharp.WgpuInstance",
        "VelloSharp.WgpuPipelineCache",
        "VelloSharp.WgpuPipelineLayout",
        "VelloSharp.WgpuQueue",
        "VelloSharp.WgpuRenderPass",
        "VelloSharp.WgpuRenderPipeline",
        "VelloSharp.WgpuRenderer",
        "VelloSharp.WgpuSampler",
        "VelloSharp.WgpuShaderModule",
        "VelloSharp.WgpuSurface",
        "VelloSharp.WgpuSurfaceTexture",
        "VelloSharp.WgpuTexture",
        "VelloSharp.WgpuTextureView",
        "VelloSharp.WinitEventLoop",
        "VelloSharp.WinitWindow",
        "VelloSharp.AdapterLuid",
        "VelloSharp.Glyph",
        "VelloSharp.GlyphMetrics",
        "VelloSharp.GpuProfilerFrame",
        "VelloSharp.GpuProfilerSlice",
        "VelloSharp.GradientStop",
        "VelloSharp.ImageInfo",
        "VelloSharp.KurboAffine",
        "VelloSharp.KurboPathElement",
        "VelloSharp.KurboPoint",
        "VelloSharp.KurboRect",
        "VelloSharp.KurboVec2",
        "VelloSharp.LayerBlend",
        "VelloSharp.PathElement",
        "VelloSharp.PenikoColorStop",
        "VelloSharp.PenikoLinearGradient",
        "VelloSharp.PenikoLinearGradientInfo",
        "VelloSharp.PenikoPoint",
        "VelloSharp.PenikoRadialGradient",
        "VelloSharp.PenikoRadialGradientInfo",
        "VelloSharp.PenikoSweepGradient",
        "VelloSharp.PenikoSweepGradientInfo",
        "VelloSharp.RenderParams",
        "VelloSharp.RendererOptions",
        "VelloSharp.RendererPipelineCache",
        "VelloSharp.RgbaColor",
        "VelloSharp.SurfaceDescriptor",
        "VelloSharp.SurfaceHandle",
        "VelloSharp.VelatoCompositionInfo",
        "VelloSharp.VelloAndroidNativeWindowHandle",
        "VelloSharp.VelloAppKitWindowHandle",
        "VelloSharp.VelloColor",
        "VelloSharp.VelloCoreAnimationLayerHandle",
        "VelloSharp.VelloCoreWindowHandle",
        "VelloSharp.VelloSwapChainPanelHandle",
        "VelloSharp.VelloWaylandWindowHandle",
        "VelloSharp.VelloWin32WindowHandle",
        "VelloSharp.VelloWindowHandle",
        "VelloSharp.VelloWindowHandlePayload",
        "VelloSharp.VelloXlibWindowHandle",
        "VelloSharp.WgpuAdapterInfo",
        "VelloSharp.WgpuBindGroupDescriptor",
        "VelloSharp.WgpuBindGroupEntry",
        "VelloSharp.WgpuBindGroupLayoutDescriptor",
        "VelloSharp.WgpuBindGroupLayoutEntry",
        "VelloSharp.WgpuBlendComponent",
        "VelloSharp.WgpuBlendState",
        "VelloSharp.WgpuBufferBinding",
        "VelloSharp.WgpuBufferBindingLayout",
        "VelloSharp.WgpuBufferDescriptor",
        "VelloSharp.WgpuColor",
        "VelloSharp.WgpuColorTargetState",
        "VelloSharp.WgpuCommandBufferDescriptor",
        "VelloSharp.WgpuCommandEncoderDescriptor",
        "VelloSharp.WgpuDepthStencilState",
        "VelloSharp.WgpuDeviceDescriptor",
        "VelloSharp.WgpuExtent3D",
        "VelloSharp.WgpuFragmentState",
        "VelloSharp.WgpuImageCopyTexture",
        "VelloSharp.WgpuInstanceOptions",
        "VelloSharp.WgpuMultisampleState",
        "VelloSharp.WgpuOrigin3D",
        "VelloSharp.WgpuPipelineCacheDescriptor",
        "VelloSharp.WgpuPipelineLayoutDescriptor",
        "VelloSharp.WgpuPrimitiveState",
        "VelloSharp.WgpuRenderPassColorAttachment",
        "VelloSharp.WgpuRenderPassDepthStencilAttachment",
        "VelloSharp.WgpuRenderPassDescriptor",
        "VelloSharp.WgpuRenderPipelineDescriptor",
        "VelloSharp.WgpuRequestAdapterOptions",
        "VelloSharp.WgpuSamplerBindingLayout",
        "VelloSharp.WgpuSamplerDescriptor",
        "VelloSharp.WgpuShaderModuleDescriptor",
        "VelloSharp.WgpuStencilFaceState",
        "VelloSharp.WgpuStorageTextureBindingLayout",
        "VelloSharp.WgpuSurfaceConfiguration",
        "VelloSharp.WgpuTextureBindingLayout",
        "VelloSharp.WgpuTextureDataLayout",
        "VelloSharp.WgpuTextureDescriptor",
        "VelloSharp.WgpuTextureViewDescriptor",
        "VelloSharp.WgpuVertexAttribute",
        "VelloSharp.WgpuVertexBufferLayout",
        "VelloSharp.WgpuVertexState",
        "VelloSharp.WinitEventArgs",
        "VelloSharp.WinitEventLoopContext",
        "VelloSharp.WinitRunConfiguration",
        "VelloSharp.WinitWindowOptions",
        "VelloSharp.IWinitEventHandler",
        "VelloSharp.AntialiasingMode",
        "VelloSharp.ExtendMode",
        "VelloSharp.FillRule",
        "VelloSharp.GlyphRunStyle",
        "VelloSharp.ImageAlphaMode",
        "VelloSharp.ImageQuality",
        "VelloSharp.KurboPathVerb",
        "VelloSharp.LayerCompose",
        "VelloSharp.LayerMix",
        "VelloSharp.LineCap",
        "VelloSharp.LineJoin",
        "VelloSharp.PathVerb",
        "VelloSharp.PenikoBrushKind",
        "VelloSharp.PenikoExtend",
        "VelloSharp.PenikoGradientKind",
        "VelloSharp.PenikoStatus",
        "VelloSharp.PresentMode",
        "VelloSharp.RenderFormat",
        "VelloSharp.SparseRenderMode",
        "VelloSharp.SparseSimdLevel",
        "VelloSharp.VelloWindowHandleKind",
        "VelloSharp.WgpuAddressMode",
        "VelloSharp.WgpuBackend",
        "VelloSharp.WgpuBackendType",
        "VelloSharp.WgpuBindGroupEntryType",
        "VelloSharp.WgpuBindingLayoutType",
        "VelloSharp.WgpuBlendFactor",
        "VelloSharp.WgpuBlendOperation",
        "VelloSharp.WgpuBufferBindingType",
        "VelloSharp.WgpuBufferUsage",
        "VelloSharp.WgpuColorWriteMask",
        "VelloSharp.WgpuCompareFunction",
        "VelloSharp.WgpuCompositeAlphaMode",
        "VelloSharp.WgpuCullMode",
        "VelloSharp.WgpuDeviceType",
        "VelloSharp.WgpuDx12Compiler",
        "VelloSharp.WgpuFeature",
        "VelloSharp.WgpuFilterMode",
        "VelloSharp.WgpuFrontFace",
        "VelloSharp.WgpuIndexFormat",
        "VelloSharp.WgpuLimitsPreset",
        "VelloSharp.WgpuLoadOp",
        "VelloSharp.WgpuPolygonMode",
        "VelloSharp.WgpuPowerPreference",
        "VelloSharp.WgpuPrimitiveTopology",
        "VelloSharp.WgpuSamplerBindingType",
        "VelloSharp.WgpuShaderModuleSourceKind",
        "VelloSharp.WgpuShaderStage",
        "VelloSharp.WgpuStencilOperation",
        "VelloSharp.WgpuStorageTextureAccess",
        "VelloSharp.WgpuStoreOp",
        "VelloSharp.WgpuTextureAspect",
        "VelloSharp.WgpuTextureDimension",
        "VelloSharp.WgpuTextureFormat",
        "VelloSharp.WgpuTextureSampleType",
        "VelloSharp.WgpuTextureUsage",
        "VelloSharp.WgpuTextureViewDimension",
        "VelloSharp.WgpuVertexFormat",
        "VelloSharp.WgpuVertexStepMode",
        "VelloSharp.WinitAccessKitEventKind",
        "VelloSharp.WinitControlFlow",
        "VelloSharp.WinitCursorIcon",
        "VelloSharp.WinitElementState",
        "VelloSharp.WinitEventKind",
        "VelloSharp.WinitKeyLocation",
        "VelloSharp.WinitModifiers",
        "VelloSharp.WinitMouseButton",
        "VelloSharp.WinitMouseScrollDeltaKind",
        "VelloSharp.WinitResizeDirection",
        "VelloSharp.WinitStartCause",
        "VelloSharp.WinitStatus",
        "VelloSharp.WinitTouchPhaseKind",
        "VelloSharp.WinitWindowButtons",
        "VelloSharp.WinitWindowLevel"
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
        "VelloSharp.Avalonia.Vello.Rendering.WgpuSurfaceRenderContext",
        "VelloSharp.Avalonia.Vello.Rendering.IVelloApiLease",
        "VelloSharp.Avalonia.Vello.Rendering.IVelloApiLeaseFeature",
        "VelloSharp.Avalonia.Vello.Rendering.IVelloPlatformGraphicsLease"
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
        "VelloSharp.ChartDiagnostics.DashboardTelemetrySink",
        "VelloSharp.ChartDiagnostics.FrameDiagnosticsCollector",
        "VelloSharp.ChartDiagnostics.ChartMetric",
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
        "VelloSharp.ChartEngine.AreaSeriesDefinition",
        "VelloSharp.ChartEngine.BarSeriesDefinition",
        "VelloSharp.ChartEngine.ChartAnimationProfile",
        "VelloSharp.ChartEngine.ChartComposition",
        "VelloSharp.ChartEngine.ChartCompositionBuilder",
        "VelloSharp.ChartEngine.ChartEngine",
        "VelloSharp.ChartEngine.ChartEngineOptions",
        "VelloSharp.ChartEngine.ChartFrameMetadata",
        "VelloSharp.ChartEngine.ChartPaneBuilder",
        "VelloSharp.ChartEngine.ChartPaneDefinition",
        "VelloSharp.ChartEngine.ChartSeriesDefinition",
        "VelloSharp.ChartEngine.ChartStreamingAnimationPreset",
        "VelloSharp.ChartEngine.CompositionAnnotationLayer",
        "VelloSharp.ChartEngine.HeatmapSeriesDefinition",
        "VelloSharp.ChartEngine.LineSeriesDefinition",
        "VelloSharp.ChartEngine.PolylineBandSeriesDefinition",
        "VelloSharp.ChartEngine.ScatterSeriesDefinition",
        "VelloSharp.ChartEngine.ChartAnimationTimeline",
        "VelloSharp.ChartEngine.ChartColor",
        "VelloSharp.ChartEngine.ChartCursorUpdate",
        "VelloSharp.ChartEngine.ChartFrameMetadata.AxisTickMetadata",
        "VelloSharp.ChartEngine.ChartFrameMetadata.ChartAnnotationOverlay",
        "VelloSharp.ChartEngine.ChartFrameMetadata.ChartCursorOverlay",
        "VelloSharp.ChartEngine.ChartFrameMetadata.ChartStreamingOverlay",
        "VelloSharp.ChartEngine.ChartFrameMetadata.PaneMetadata",
        "VelloSharp.ChartEngine.ChartFrameMetadata.SeriesMetadata",
        "VelloSharp.ChartEngine.ChartSeriesOverride",
        "VelloSharp.ChartEngine.ChartStreamingUpdate",
        "VelloSharp.ChartEngine.AnnotationZOrder",
        "VelloSharp.ChartEngine.ChartSeriesKind",
        "VelloSharp.ChartEngine.ChartStreamingEventKind"
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
        "VelloSharp.ChartEngine.Annotations.CalloutAnnotation",
        "VelloSharp.ChartEngine.Annotations.ChartAnnotation",
        "VelloSharp.ChartEngine.Annotations.GradientZoneAnnotation",
        "VelloSharp.ChartEngine.Annotations.HorizontalLineAnnotation",
        "VelloSharp.ChartEngine.Annotations.TimeRangeAnnotation",
        "VelloSharp.ChartEngine.Annotations.ValueZoneAnnotation",
        "VelloSharp.ChartEngine.Annotations.VerticalLineAnnotation",
        "VelloSharp.ChartEngine.Annotations.AnnotationCalloutPlacement",
        "VelloSharp.ChartEngine.Annotations.AnnotationKind",
        "VelloSharp.ChartEngine.Annotations.AnnotationSnapMode"
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
        "VelloSharp.ChartRuntime.RenderScheduler",
        "VelloSharp.ChartRuntime.FrameTick",
        "VelloSharp.ChartRuntime.IFrameTickSource",
        "VelloSharp.ChartRuntime.FrameTickCallback"
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
        "VelloSharp.Charting.Axis.AxisDefinition-1",
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
        "VelloSharp.Charting.Coordinates.ChartDataPoint-2",
        "VelloSharp.Charting.Coordinates.ChartPoint",
        "VelloSharp.Charting.Coordinates.CoordinateTransformer-2"
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
        "VelloSharp.Charting.Layout.ChartLayoutEngine",
        "VelloSharp.Charting.Layout.ChartLayoutPreset",
        "VelloSharp.Charting.Layout.ChartLayoutRequest",
        "VelloSharp.Charting.Layout.ChartLayoutResult",
        "VelloSharp.Charting.Layout.LayoutGallery",
        "VelloSharp.Charting.Layout.LayoutRect",
        "VelloSharp.Charting.Layout.AxisOrientation"
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
        "VelloSharp.Charting.Legend.LegendRenderer",
        "VelloSharp.Charting.Legend.LegendVisual",
        "VelloSharp.Charting.Legend.LegendItem",
        "VelloSharp.Charting.Legend.LegendItemVisual",
        "VelloSharp.Charting.Legend.LegendOrientation",
        "VelloSharp.Charting.Legend.LegendPosition"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Charting.Legend"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Charting.Primitives",
      "items": [
        "VelloSharp.Charting.Primitives.Range-1"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Charting.Primitives"
      }
    },
    {
      "type": "category",
      "label": "VelloSharp.Charting.Rendering",
      "items": [
        "VelloSharp.Charting.Rendering.AxisRenderResult",
        "VelloSharp.Charting.Rendering.AxisRenderer",
        "VelloSharp.Charting.Rendering.AxisVisual",
        "VelloSharp.Charting.Rendering.ChartOverlayRenderer",
        "VelloSharp.Charting.Rendering.AxisLabelVisual",
        "VelloSharp.Charting.Rendering.AxisLineVisual",
        "VelloSharp.Charting.Rendering.AxisTickVisual",
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
        "VelloSharp.Charting.Scales.LinearScale",
        "VelloSharp.Charting.Scales.LogarithmicScale",
        "VelloSharp.Charting.Scales.OrdinalScale-1",
        "VelloSharp.Charting.Scales.TimeScale",
        "VelloSharp.Charting.Scales.IScale",
        "VelloSharp.Charting.Scales.IScale-1",
        "VelloSharp.Charting.Scales.ScaleKind"
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
        "VelloSharp.Charting.Styling.ValueColorGradient",
        "VelloSharp.Charting.Styling.RgbaColor"
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
        "VelloSharp.Charting.Ticks.LinearTickGenerator",
        "VelloSharp.Charting.Ticks.OrdinalTickGenerator-1",
        "VelloSharp.Charting.Ticks.TickGenerationOptions-1",
        "VelloSharp.Charting.Ticks.TimeTickGenerator",
        "VelloSharp.Charting.Ticks.AxisTick-1",
        "VelloSharp.Charting.Ticks.AxisTickInfo",
        "VelloSharp.Charting.Ticks.IAxisTickGenerator-1"
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
        "VelloSharp.Composition.CompositionInterop",
        "VelloSharp.Composition.CompositionInterop.CompositionVirtualizer",
        "VelloSharp.Composition.CompositionMaterialRegistry",
        "VelloSharp.Composition.CompositionShaderRegistry",
        "VelloSharp.Composition.SceneCache",
        "VelloSharp.Composition.ScenePartitioner",
        "VelloSharp.Composition.TimelineSystem",
        "VelloSharp.Composition.ColumnSlice",
        "VelloSharp.Composition.ColumnViewportMetrics",
        "VelloSharp.Composition.CompositionColor",
        "VelloSharp.Composition.CompositionInterop.LinearLayoutChild",
        "VelloSharp.Composition.CompositionInterop.LinearLayoutResult",
        "VelloSharp.Composition.CompositionMaterialDescriptor",
        "VelloSharp.Composition.CompositionShaderDescriptor",
        "VelloSharp.Composition.DirtyRegion",
        "VelloSharp.Composition.DockLayoutChild",
        "VelloSharp.Composition.DockLayoutOptions",
        "VelloSharp.Composition.GridLayoutChild",
        "VelloSharp.Composition.GridLayoutOptions",
        "VelloSharp.Composition.GridTrack",
        "VelloSharp.Composition.LabelMetrics",
        "VelloSharp.Composition.LayoutConstraints",
        "VelloSharp.Composition.LayoutRect",
        "VelloSharp.Composition.LayoutSize",
        "VelloSharp.Composition.LayoutThickness",
        "VelloSharp.Composition.PlotArea",
        "VelloSharp.Composition.RenderLayer",
        "VelloSharp.Composition.RowPlanEntry",
        "VelloSharp.Composition.RowViewportMetrics",
        "VelloSharp.Composition.RowWindow",
        "VelloSharp.Composition.ScalarConstraint",
        "VelloSharp.Composition.StackLayoutChild",
        "VelloSharp.Composition.StackLayoutOptions",
        "VelloSharp.Composition.TimelineDirtyBinding",
        "VelloSharp.Composition.TimelineEasingTrackDescriptor",
        "VelloSharp.Composition.TimelineGroupConfig",
        "VelloSharp.Composition.TimelineSample",
        "VelloSharp.Composition.TimelineSpringTrackDescriptor",
        "VelloSharp.Composition.VirtualColumnStrip",
        "VelloSharp.Composition.VirtualRowMetric",
        "VelloSharp.Composition.VirtualizerTelemetry",
        "VelloSharp.Composition.WrapLayoutChild",
        "VelloSharp.Composition.WrapLayoutLine",
        "VelloSharp.Composition.WrapLayoutOptions",
        "VelloSharp.Composition.WrapLayoutSolveResult",
        "VelloSharp.Composition.CompositionShaderKind",
        "VelloSharp.Composition.DockSide",
        "VelloSharp.Composition.FrozenKind",
        "VelloSharp.Composition.GridTrackKind",
        "VelloSharp.Composition.LayoutAlignment",
        "VelloSharp.Composition.LayoutOrientation",
        "VelloSharp.Composition.RowAction",
        "VelloSharp.Composition.TimelineDirtyKind",
        "VelloSharp.Composition.TimelineEasing",
        "VelloSharp.Composition.TimelineRepeat",
        "VelloSharp.Composition.TimelineSampleFlags"
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
        "VelloSharp.Composition.Accessibility.AccessibilityActionEventArgs",
        "VelloSharp.Composition.Accessibility.AccessibilityAnnouncementEventArgs",
        "VelloSharp.Composition.Accessibility.AccessibilityChangedEventArgs",
        "VelloSharp.Composition.Accessibility.AccessibilityProperties",
        "VelloSharp.Composition.Accessibility.AccessibilityAction",
        "VelloSharp.Composition.Accessibility.AccessibilityLiveSetting",
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
        "VelloSharp.Composition.Telemetry.GaugeTelemetryConnector",
        "VelloSharp.Composition.Telemetry.ScadaTelemetryRouter",
        "VelloSharp.Composition.Telemetry.TelemetryHub",
        "VelloSharp.Composition.Telemetry.CommandRequest",
        "VelloSharp.Composition.Telemetry.CommandResult",
        "VelloSharp.Composition.Telemetry.TelemetrySample",
        "VelloSharp.Composition.Telemetry.ICommandHandler",
        "VelloSharp.Composition.Telemetry.IGaugeTelemetryConsumer",
        "VelloSharp.Composition.Telemetry.ITelemetryObserver",
        "VelloSharp.Composition.Telemetry.CommandStatus",
        "VelloSharp.Composition.Telemetry.TelemetryQuality"
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
        "VelloSharp.Integration.Avalonia.VelloSurfaceView",
        "VelloSharp.Integration.Avalonia.VelloView",
        "VelloSharp.Integration.Avalonia.VelloRenderFrameContext"
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
        "VelloSharp.Maui.Controls.VelloView",
        "VelloSharp.Maui.Controls.IVelloView",
        "VelloSharp.Maui.Controls.IVelloViewHandler"
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
        "VelloSharp.Maui.Rendering.VelloGraphicsDeviceOptions",
        "VelloSharp.Maui.Rendering.RenderLoopDriver",
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
        "VelloSharp.Rendering.VelloRenderPath",
        "VelloSharp.Rendering.RenderTargetDescriptor"
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
        "VelloSharp.Text.ParleyFontService",
        "VelloSharp.Text.VelloTextShaperCore",
        "VelloSharp.Text.ParleyFontInfo",
        "VelloSharp.Text.ParleyFontQuery",
        "VelloSharp.Text.ParleyVariationAxis",
        "VelloSharp.Text.VelloGlyph",
        "VelloSharp.Text.VelloOpenTypeFeature",
        "VelloSharp.Text.VelloTextShaperOptions",
        "VelloSharp.Text.VelloVariationAxisValue",
        "VelloSharp.Text.VelloFontStyle"
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
        "VelloSharp.TreeDataGrid.TreeDataModel",
        "VelloSharp.TreeDataGrid.TreeRenderLoop",
        "VelloSharp.TreeDataGrid.TreeRowAnimationProfile",
        "VelloSharp.TreeDataGrid.TreeVirtualizationScheduler",
        "VelloSharp.TreeDataGrid.TreeAnimationTimeline",
        "VelloSharp.TreeDataGrid.TreeBufferAdoptionDiagnostics",
        "VelloSharp.TreeDataGrid.TreeChromeVisual",
        "VelloSharp.TreeDataGrid.TreeColor",
        "VelloSharp.TreeDataGrid.TreeColumnMetric",
        "VelloSharp.TreeDataGrid.TreeColumnSlice",
        "VelloSharp.TreeDataGrid.TreeColumnSpan",
        "VelloSharp.TreeDataGrid.TreeFrameStats",
        "VelloSharp.TreeDataGrid.TreeGpuTimestampSummary",
        "VelloSharp.TreeDataGrid.TreeGroupHeaderVisual",
        "VelloSharp.TreeDataGrid.TreeModelDiff",
        "VelloSharp.TreeDataGrid.TreeNodeDescriptor",
        "VelloSharp.TreeDataGrid.TreeNodeMetadata",
        "VelloSharp.TreeDataGrid.TreeRowAnimationSnapshot",
        "VelloSharp.TreeDataGrid.TreeRowMetric",
        "VelloSharp.TreeDataGrid.TreeRowPlanEntry",
        "VelloSharp.TreeDataGrid.TreeRowVisual",
        "VelloSharp.TreeDataGrid.TreeRowWindow",
        "VelloSharp.TreeDataGrid.TreeSelectionDiff",
        "VelloSharp.TreeDataGrid.TreeSpringAnimationTrack",
        "VelloSharp.TreeDataGrid.TreeSummaryVisual",
        "VelloSharp.TreeDataGrid.TreeViewportMetrics",
        "VelloSharp.TreeDataGrid.TreeVirtualizationPlan",
        "VelloSharp.TreeDataGrid.TreeVirtualizationTelemetry",
        "VelloSharp.TreeDataGrid.TreeFrozenKind",
        "VelloSharp.TreeDataGrid.TreeModelDiffKind",
        "VelloSharp.TreeDataGrid.TreeRowAction",
        "VelloSharp.TreeDataGrid.TreeRowKind",
        "VelloSharp.TreeDataGrid.TreeSelectionMode"
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
        "VelloSharp.TreeDataGrid.Composition.TreeColumnLayoutAnimator",
        "VelloSharp.TreeDataGrid.Composition.TreeColumnStripCache",
        "VelloSharp.TreeDataGrid.Composition.TreeNodeLayoutEngine",
        "VelloSharp.TreeDataGrid.Composition.TreeColumnDefinition",
        "VelloSharp.TreeDataGrid.Composition.TreeColumnPaneDiff",
        "VelloSharp.TreeDataGrid.Composition.TreeColumnPaneSnapshot",
        "VelloSharp.TreeDataGrid.Composition.TreeColumnSlot",
        "VelloSharp.TreeDataGrid.Composition.TreeColumnStripSnapshot",
        "VelloSharp.TreeDataGrid.Composition.TreeColumnSizingMode"
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
        "VelloSharp.TreeDataGrid.Rendering.TreeMaterialRegistry",
        "VelloSharp.TreeDataGrid.Rendering.TreeRenderHookRegistry",
        "VelloSharp.TreeDataGrid.Rendering.TreeSceneGraph",
        "VelloSharp.TreeDataGrid.Rendering.TreeShaderRegistry",
        "VelloSharp.TreeDataGrid.Rendering.TreeTemplatePaneBatcher",
        "VelloSharp.TreeDataGrid.Rendering.TreeMaterialDescriptor",
        "VelloSharp.TreeDataGrid.Rendering.TreePaneSceneBatch",
        "VelloSharp.TreeDataGrid.Rendering.TreePaneSceneBatchSet",
        "VelloSharp.TreeDataGrid.Rendering.TreeRenderHookDescriptor",
        "VelloSharp.TreeDataGrid.Rendering.TreeShaderDescriptor",
        "VelloSharp.TreeDataGrid.Rendering.TreeRenderHookKind",
        "VelloSharp.TreeDataGrid.Rendering.TreeShaderKind"
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
        "VelloSharp.TreeDataGrid.Templates.TreeCellTemplateBuilder-2",
        "VelloSharp.TreeDataGrid.Templates.TreeCompiledTemplate",
        "VelloSharp.TreeDataGrid.Templates.TreePaneTemplateBuilder-2",
        "VelloSharp.TreeDataGrid.Templates.TreeRowTemplateBuilder-2",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateBuilder",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateCache",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateCompiler",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateContentNodeBuilder-2",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateDefinition-2",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateLeafBuilder-2",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateManagedBackend",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateNativeBackend",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateNodeBuilderBase-2",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateRuntime",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateValue",
        "VelloSharp.TreeDataGrid.Templates.TreeColumnContext",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateBindings",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateCacheKey",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateCompileOptions",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateInstruction",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateRuntimeContext",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateRuntimeHandle",
        "VelloSharp.TreeDataGrid.Templates.ITreeTemplateBackend",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateNodeKind",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateOpCode",
        "VelloSharp.TreeDataGrid.Templates.TreeTemplateStackOrientation",
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
        "VelloSharp.WinForms.VelloLinearGradientBrush",
        "VelloSharp.WinForms.VelloPathGradientBrush",
        "VelloSharp.WinForms.VelloPen",
        "VelloSharp.WinForms.VelloRegion",
        "VelloSharp.WinForms.VelloSolidBrush",
        "VelloSharp.WinForms.VelloStringFormat",
        "VelloSharp.WinForms.VelloTextureBrush",
        "VelloSharp.WinForms.VelloGraphicsState"
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
        "VelloSharp.WinForms.Integration.VelloRenderControl",
        "VelloSharp.WinForms.Integration.VelloRenderBackend",
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
        "VelloSharp.Windows.VelloGraphicsDevice",
        "VelloSharp.Windows.VelloGraphicsDeviceOptions",
        "VelloSharp.Windows.VelloGraphicsSession",
        "VelloSharp.Windows.VelloSurfaceRenderEventArgs",
        "VelloSharp.Windows.WindowsGpuContext",
        "VelloSharp.Windows.WindowsGpuContextLease",
        "VelloSharp.Windows.WindowsGpuDiagnostics",
        "VelloSharp.Windows.WindowsSurfaceFactory",
        "VelloSharp.Windows.WindowsSwapChainSurface",
        "VelloSharp.Windows.WindowsSurfaceDescriptor",
        "VelloSharp.Windows.WindowsSurfaceSize",
        "VelloSharp.Windows.IWindowsSurfaceSource",
        "VelloSharp.Windows.PresentMode",
        "VelloSharp.Windows.RenderLoopDriver",
        "VelloSharp.Windows.WindowsColorSpace",
        "VelloSharp.Windows.WindowsSurfaceKind"
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
        "VelloSharp.Windows.Shared.Diagnostics.VelloDiagnosticsChangedEventArgs",
        "VelloSharp.Windows.Shared.Diagnostics.IVelloDiagnosticsProvider"
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
        "VelloSharp.Windows.Shared.Dispatching.VelloCompositionTarget",
        "VelloSharp.Windows.Shared.Dispatching.VelloWindowsDispatcher",
        "VelloSharp.Windows.Shared.Dispatching.IVelloCompositionTarget",
        "VelloSharp.Windows.Shared.Dispatching.IVelloCompositionTargetProvider",
        "VelloSharp.Windows.Shared.Dispatching.IVelloWindowsDispatcher",
        "VelloSharp.Windows.Shared.Dispatching.IVelloWindowsDispatcherProvider",
        "VelloSharp.Windows.Shared.Dispatching.IVelloWindowsDispatcherTimer"
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
        "VelloSharp.Windows.Shared.Presenters.VelloSwapChainPresenter",
        "VelloSharp.Windows.Shared.Presenters.VelloSwapChainRenderEventArgs",
        "VelloSharp.Windows.Shared.Presenters.IVelloSwapChainPresenterHost"
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
        "VelloSharp.Wpf.Integration.VelloSwapChainRenderEventArgs",
        "VelloSharp.Wpf.Integration.VelloView",
        "VelloSharp.Wpf.Integration.VelloViewDiagnostics",
        "VelloSharp.Wpf.Integration.WpfCompositionInputSource",
        "VelloSharp.Wpf.Integration.VelloRenderBackend",
        "VelloSharp.Wpf.Integration.VelloRenderMode"
      ],
      "link": {
        "type": "doc",
        "id": "VelloSharp.Wpf.Integration"
      }
    }
  ]
};

export default sidebars;
