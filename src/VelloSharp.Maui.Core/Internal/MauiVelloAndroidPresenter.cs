#if ANDROID
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Opengl;
using Android.OS;
using Android.Runtime;
using Android.Views;
using VelloSharp;
using VelloSharp.Maui.Controls;
using VelloSharp.Maui.Rendering;
using AView = Android.Views.View;

namespace VelloSharp.Maui.Internal;

internal sealed class MauiVelloAndroidPresenter : MauiVelloWgpuPresenterBase
{
    private AView? _platformView;
    private MauiVelloNativeSurfaceView? _surfaceView;
    private MauiVelloTextureView? _textureView;
    private Surface? _textureSurface;
    private IntPtr _nativeWindow;
    private bool _hasSurface;
    private bool _isContinuous;
    private bool _isTextureView;
    private readonly FrameCallback _frameCallback;
    private IReadOnlyDictionary<string, string>? _extendedDiagnostics;

    public MauiVelloAndroidPresenter(IVelloView view)
        : base(view)
    {
        _frameCallback = new FrameCallback(this);
    }

    public override void Attach(object? platformView)
    {
        if (platformView is null)
        {
            throw new ArgumentNullException(nameof(platformView));
        }

        _platformView = platformView as AView ?? throw new InvalidOperationException("Expected Android view for the Vello presenter.");
        _surfaceView = platformView as MauiVelloNativeSurfaceView;
        _textureView = platformView as MauiVelloTextureView;
        _isTextureView = _textureView is not null;

        if (_surfaceView is not null)
        {
            _surfaceView.SetPresenter(this);
        }
        else if (_textureView is not null)
        {
            _textureView.SetPresenter(this);
        }
        else
        {
            throw new InvalidOperationException("Expected SurfaceView or TextureView platform view.");
        }

        EnsureExtendedDiagnostics();
        ApplyRenderMode();
    }

    public override void Detach()
    {
        StopContinuousRendering();
        ReleaseNativeWindow();

        if (_surfaceView is not null)
        {
            _surfaceView.SetPresenter(null);
            _surfaceView = null;
        }

        if (_textureView is not null)
        {
            _textureView.SetPresenter(null);
            _textureView = null;
        }

        _platformView = null;
        _textureSurface?.Release();
        _textureSurface?.Dispose();
        _textureSurface = null;
        ResetSurface();
    }

    public override void OnRenderModeChanged()
        => ApplyRenderMode();

    public override void RequestRender()
    {
        if (_platformView is null || !_hasSurface || _isContinuous)
        {
            return;
        }

        _platformView.Post(RenderOnce);
    }

    internal void OnSurfaceCreated(ISurfaceHolder holder)
    {
        if (_isTextureView)
        {
            return;
        }

        AcquireNativeWindow(holder.Surface?.Handle ?? IntPtr.Zero);
    }

    internal void OnSurfaceDestroyed()
    {
        if (_isTextureView)
        {
            return;
        }

        HandleSurfaceLost();
    }

    internal void OnSurfaceChanged()
    {
        if (!_isContinuous)
        {
            RequestRender();
        }
    }

    internal void OnSurfaceTextureAvailable(Surface surface, int width, int height)
    {
        if (!_isTextureView)
        {
            return;
        }

        _textureSurface = surface;
        AcquireNativeWindow(surface?.Handle ?? IntPtr.Zero);
    }

    internal void OnSurfaceTextureSizeChanged()
    {
        if (!_isContinuous)
        {
            RequestRender();
        }
    }

    internal void OnSurfaceTextureDestroyed()
    {
        if (!_isTextureView)
        {
            return;
        }

        HandleSurfaceLost();
        _textureSurface?.Release();
        _textureSurface?.Dispose();
        _textureSurface = null;
    }

    protected override SurfaceHandle CreateSurfaceHandle()
    {
        if (_nativeWindow == IntPtr.Zero)
        {
            throw new InvalidOperationException("Native window pointer is not available.");
        }

        return SurfaceHandle.FromAndroidNativeWindow(_nativeWindow);
    }

    private void RenderOnce()
    {
        if (_platformView is null || !_hasSurface)
        {
            return;
        }

        var width = (uint)Math.Max(1, _platformView.Width);
        var height = (uint)Math.Max(1, _platformView.Height);
        RenderFrame(width, height, isAnimationFrame: false, platformContext: _platformView, platformSurface: _nativeWindow);
    }

    private void ApplyRenderMode()
    {
        _isContinuous = View.RenderMode == VelloRenderMode.Continuous;
        if (_isContinuous)
        {
            StartContinuousRendering();
        }
        else
        {
            StopContinuousRendering();
        }
    }

    private void StartContinuousRendering()
    {
        if (!_isContinuous || _platformView is null)
        {
            return;
        }

        Choreographer.Instance.PostFrameCallback(_frameCallback);
    }

    private void StopContinuousRendering()
        => Choreographer.Instance.RemoveFrameCallback(_frameCallback);

    protected override void ReleaseNativeResources()
    {
        ReleaseNativeWindow();
    }

    private void AcquireNativeWindow(IntPtr surfaceHandle)
    {
        ReleaseNativeWindow();

        if (surfaceHandle == IntPtr.Zero)
        {
            ReportGpuUnavailable("Surface handle was null.");
            return;
        }

        var window = AndroidNativeWindow.ANativeWindow_fromSurface(JNIEnv.Handle, surfaceHandle);
        if (window == IntPtr.Zero)
        {
            ReportGpuUnavailable("ANativeWindow_fromSurface returned null.");
            return;
        }

        _nativeWindow = window;
        _hasSurface = true;

        if (!_isContinuous)
        {
            RequestRender();
        }
    }

    private void HandleSurfaceLost()
    {
        StopContinuousRendering();
        _hasSurface = false;
        ReleaseNativeWindow();
        ResetSurface();
    }

    private void ReleaseNativeWindow()
    {
        if (_nativeWindow != IntPtr.Zero)
        {
            AndroidNativeWindow.ANativeWindow_release(_nativeWindow);
            _nativeWindow = IntPtr.Zero;
        }

        if (_textureSurface is not null)
        {
            _textureSurface.Release();
            _textureSurface.Dispose();
            _textureSurface = null;
        }
    }

    public override void OnSurfaceConfigurationChanged()
    {
        var shouldUseTexture = View.UseTextureView;
        if (shouldUseTexture != _isTextureView)
        {
            ReportGpuUnavailable("Changing UseTextureView at runtime requires recreating the MAUI handler.");
        }
    }

    protected override IReadOnlyDictionary<string, string>? GetExtendedDiagnostics()
        => _extendedDiagnostics;

    private void EnsureExtendedDiagnostics()
    {
        if (_extendedDiagnostics is not null)
        {
            return;
        }

        _extendedDiagnostics = AndroidDiagnosticsCollector.Collect();
    }

    private sealed class FrameCallback : Java.Lang.Object, Choreographer.IFrameCallback
    {
        private readonly MauiVelloAndroidPresenter _presenter;

        public FrameCallback(MauiVelloAndroidPresenter presenter)
        {
            _presenter = presenter;
        }

        public void DoFrame(long frameTimeNanos)
        {
            if (_presenter._isContinuous)
            {
                _presenter.RenderOnce();
                Choreographer.Instance.PostFrameCallback(this);
            }
        }
    }

    private static class AndroidDiagnosticsCollector
    {
        public static IReadOnlyDictionary<string, string> Collect()
        {
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            CollectDeviceInfo(properties);
            CollectGlInfo(properties);
            CollectVulkanInfo(properties);
            return properties;
        }

        private static void CollectDeviceInfo(IDictionary<string, string> properties)
        {
            try
            {
                properties["Device.Manufacturer"] = Build.Manufacturer ?? string.Empty;
                properties["Device.Model"] = Build.Model ?? string.Empty;
                properties["Device.Product"] = Build.Product ?? string.Empty;
                properties["Device.Board"] = Build.Board ?? string.Empty;
            }
            catch
            {
                // ignored
            }
        }

        private static void CollectGlInfo(IDictionary<string, string> properties)
        {
            EGLDisplay display = EGL14.EglNoDisplay;
            EGLSurface surface = EGL14.EglNoSurface;
            EGLContext context = EGL14.EglNoContext;

            try
            {
                display = EGL14.EglGetDisplay(EGL14.EglDefaultDisplay);
                if (display == EGL14.EglNoDisplay)
                {
                    return;
                }

                var version = new int[2];
                if (!EGL14.EglInitialize(display, version, 0, version, 1))
                {
                    return;
                }

                const int EGL_RENDERABLE_TYPE = 0x3040;
                const int EGL_OPENGL_ES2_BIT = 0x0004;
                const int EGL_SURFACE_TYPE = 0x3033;
                const int EGL_PBUFFER_BIT = 0x0001;
                const int EGL_NONE = 0x3038;
                const int EGL_WIDTH = 0x3057;
                const int EGL_HEIGHT = 0x3056;

                var configAttribs = new[]
                {
                    EGL_RENDERABLE_TYPE, EGL_OPENGL_ES2_BIT,
                    EGL_SURFACE_TYPE, EGL_PBUFFER_BIT,
                    EGL_NONE
                };

                var configs = new EGLConfig[1];
                var numConfigs = new int[1];
                if (!EGL14.EglChooseConfig(display, configAttribs, 0, configs, 0, configs.Length, numConfigs, 0) || numConfigs[0] <= 0)
                {
                    return;
                }

                var surfaceAttribs = new[]
                {
                    EGL_WIDTH, 1,
                    EGL_HEIGHT, 1,
                    EGL_NONE
                };

                surface = EGL14.EglCreatePbufferSurface(display, configs[0], surfaceAttribs, 0);
                if (surface == EGL14.EglNoSurface)
                {
                    return;
                }

                var contextAttribs = new[]
                {
                    EGL14.EglContextClientVersion, 2,
                    EGL14.EglNone
                };

                context = EGL14.EglCreateContext(display, configs[0], EGL14.EglNoContext, contextAttribs, 0);
                if (context == EGL14.EglNoContext)
                {
                    return;
                }

                if (!EGL14.EglMakeCurrent(display, surface, surface, context))
                {
                    return;
                }

                var vendor = GLES20.GlGetString(GLES20.GlVendor);
                if (!string.IsNullOrWhiteSpace(vendor))
                {
                    properties["OpenGL.Vendor"] = vendor;
                }

                var renderer = GLES20.GlGetString(GLES20.GlRenderer);
                if (!string.IsNullOrWhiteSpace(renderer))
                {
                    properties["OpenGL.Renderer"] = renderer;
                }

                var versionString = GLES20.GlGetString(GLES20.GlVersion);
                if (!string.IsNullOrWhiteSpace(versionString))
                {
                    properties["OpenGL.Version"] = versionString;
                }
            }
            catch
            {
                // ignored
            }
            finally
            {
                if (display != EGL14.EglNoDisplay)
                {
                    EGL14.EglMakeCurrent(display, EGL14.EglNoSurface, EGL14.EglNoSurface, EGL14.EglNoContext);
                }

                if (surface != EGL14.EglNoSurface)
                {
                    EGL14.EglDestroySurface(display, surface);
                }

                if (context != EGL14.EglNoContext)
                {
                    EGL14.EglDestroyContext(display, context);
                }

                if (display != EGL14.EglNoDisplay)
                {
                    EGL14.EglTerminate(display);
                }
            }
        }

        private static void CollectVulkanInfo(IDictionary<string, string> properties)
        {
            try
            {
                var vulkanType = Type.GetType("Android.Hardware.Graphics.Vulkan.Vulkan, Mono.Android", throwOnError: false);
                bool isAvailable = false;
                if (vulkanType is not null)
                {
                    var isAvailableObj = vulkanType.GetProperty("IsAvailable", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                    isAvailable = isAvailableObj is bool value && value;
                    properties["Vulkan.IsAvailable"] = isAvailable ? "True" : "False";

                    if (isAvailable)
                    {
                        var versionObj = vulkanType.GetProperty("InstanceVersion", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                        if (versionObj is not null)
                        {
                            properties["Vulkan.InstanceVersion"] = versionObj.ToString() ?? string.Empty;
                        }
                    }
                }
                else
                {
                    properties["Vulkan.IsAvailable"] = "Unknown";
                }

                var pm = Android.App.Application.Context?.PackageManager;
                if (pm is not null && OperatingSystem.IsAndroidVersionAtLeast(24))
                {
                    properties["Feature.Vulkan.Level"] = pm.HasSystemFeature(PackageManager.FeatureVulkanHardwareLevel) ? "Present" : "Missing";
                    properties["Feature.Vulkan.Version"] = pm.HasSystemFeature(PackageManager.FeatureVulkanHardwareVersion) ? "Present" : "Missing";
                }
            }
            catch
            {
                // ignored
            }
        }
    }
}

internal sealed class MauiVelloNativeSurfaceView : SurfaceView, ISurfaceHolderCallback
{
    private MauiVelloAndroidPresenter? _presenter;

    public MauiVelloNativeSurfaceView(Context context)
        : base(context)
    {
        Holder.AddCallback(this);
        SetWillNotDraw(true);
        Focusable = true;
        FocusableInTouchMode = true;
        Clickable = true;
    }

    public void SetPresenter(MauiVelloAndroidPresenter? presenter)
    {
        _presenter = presenter;
    }

    public void SurfaceCreated(ISurfaceHolder holder)
        => _presenter?.OnSurfaceCreated(holder);

    public void SurfaceDestroyed(ISurfaceHolder holder)
        => _presenter?.OnSurfaceDestroyed();

    public void SurfaceChanged(ISurfaceHolder holder, [GeneratedEnum] Format format, int width, int height)
        => _presenter?.OnSurfaceChanged();
}

internal sealed class MauiVelloTextureView : TextureView, TextureView.ISurfaceTextureListener
{
    private MauiVelloAndroidPresenter? _presenter;
    private Surface? _surface;

    public MauiVelloTextureView(Context context)
        : base(context)
    {
        SurfaceTextureListener = this;
        Clickable = true;
        Focusable = true;
        FocusableInTouchMode = true;
    }

    public void SetPresenter(MauiVelloAndroidPresenter? presenter)
    {
        _presenter = presenter;
    }

    public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
    {
        _surface = new Surface(surface);
        _presenter?.OnSurfaceTextureAvailable(_surface, width, height);
    }

    public bool OnSurfaceTextureDestroyed(SurfaceTexture surface)
    {
        _presenter?.OnSurfaceTextureDestroyed();
        _surface?.Release();
        _surface?.Dispose();
        _surface = null;
        return true;
    }

    public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height)
        => _presenter?.OnSurfaceTextureSizeChanged();

    public void OnSurfaceTextureUpdated(SurfaceTexture surface)
    {
    }
}

internal static class AndroidNativeWindow
{
    [DllImport("android")]
    internal static extern IntPtr ANativeWindow_fromSurface(IntPtr jniEnv, IntPtr handle);

    [DllImport("android")]
    internal static extern void ANativeWindow_release(IntPtr window);
}
#endif
