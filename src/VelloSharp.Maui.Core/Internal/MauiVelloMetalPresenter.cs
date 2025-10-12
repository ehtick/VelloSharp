#if IOS || MACCATALYST
using System;
using CoreAnimation;
using CoreGraphics;
using Foundation;
using Metal;
using MetalKit;
using UIKit;
using VelloSharp;
using VelloSharp.Maui.Controls;
using VelloSharp.Maui.Rendering;
using VelloSharp.Maui.Input;
using System.Linq;

namespace VelloSharp.Maui.Internal;

internal sealed class MauiVelloMetalPresenter : MauiVelloWgpuPresenterBase
{
    private readonly bool _isMacCatalyst;
    private MauiMetalView? _metalView;
    private bool _isContinuous;

    public MauiVelloMetalPresenter(IVelloView view, bool isMacCatalyst)
        : base(view)
    {
        _isMacCatalyst = isMacCatalyst;
    }

    public override void Attach(object? platformView)
    {
        if (platformView is not MauiMetalView metalView)
        {
            throw new InvalidOperationException("Expected MTKView-backed MauiMetalView.");
        }

        _metalView = metalView;
        metalView.Presenter = this;
        OnDrawableSizeChanged(metalView.DrawableSize);
        ApplyRenderMode();
    }

    public override void Detach()
    {
        if (_metalView is null)
        {
            return;
        }

        _metalView.Presenter = null;
        _metalView = null;
        ResetSurface();
    }

    public override void OnRenderModeChanged()
        => ApplyRenderMode();

    public override void RequestRender()
    {
        if (_metalView is null)
        {
            return;
        }

        _metalView.SetNeedsDisplay();
    }

    protected override SurfaceHandle CreateSurfaceHandle()
    {
        if (_metalView?.Layer is not CAMetalLayer layer)
        {
            throw new InvalidOperationException("Metal layer is not available.");
        }

        return SurfaceHandle.FromCoreAnimationLayer(layer.Handle);
    }

    internal void OnDrawableSizeChanged(CGSize size)
    {
        if (_metalView is null)
        {
            return;
        }

        if (View.RenderMode == VelloRenderMode.OnDemand)
        {
            _metalView.SetNeedsDisplay();
        }
    }

    internal void RenderCurrentDrawable(MTKView view)
    {
        var drawableSize = view.DrawableSize;
        var width = (uint)Math.Max(1, (int)Math.Round(drawableSize.Width));
        var height = (uint)Math.Max(1, (int)Math.Round(drawableSize.Height));
        var isAnimationFrame = _isContinuous;

        RenderFrame(width, height, isAnimationFrame, platformContext: view, platformSurface: view.CurrentDrawable);
    }

    private void ApplyRenderMode()
    {
        if (_metalView is null)
        {
            return;
        }

        _isContinuous = View.RenderMode == VelloRenderMode.Continuous;
        _metalView.Paused = !_isContinuous;
        if (!_isContinuous)
        {
            _metalView.SetNeedsDisplay();
        }
    }
}

internal sealed class MauiMetalView : MTKView
{
    private readonly IMTLDevice _device;

    public MauiMetalView(IMTLDevice device, bool isMacCatalyst)
        : base(CGRect.Empty, device)
    {
        _device = device;
        Device = device;
        ColorPixelFormat = MTLPixelFormat.BGRA8Unorm;
        FramebufferOnly = false;
        Paused = true;
        EnableSetNeedsDisplay = true;
        PreferredFramesPerSecond = 60;
        AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
        Delegate = new MetalViewDelegate(this);
        MultipleTouchEnabled = true;
        UserInteractionEnabled = true;
    }

    public MauiVelloMetalPresenter? Presenter { get; set; }
    internal MauiCompositionInputSource? InputSource { get; set; }

    public override bool CanBecomeFirstResponder => true;

    public override bool BecomeFirstResponder()
    {
        var result = base.BecomeFirstResponder();
        if (result)
        {
            InputSource?.ForwardFocusChanged(true);
        }
        return result;
    }

    public override bool ResignFirstResponder()
    {
        var result = base.ResignFirstResponder();
        if (result)
        {
            InputSource?.ForwardFocusChanged(false);
        }
        return result;
    }

    public override void TouchesBegan(NSSet touches, UIEvent? evt)
    {
        base.TouchesBegan(touches, evt);
        InputSource?.HandleTouches(ToTouchArray(touches), evt, MauiTouchPhase.Began);
    }

    public override void TouchesMoved(NSSet touches, UIEvent? evt)
    {
        base.TouchesMoved(touches, evt);
        InputSource?.HandleTouches(ToTouchArray(touches), evt, MauiTouchPhase.Moved);
    }

    public override void TouchesEnded(NSSet touches, UIEvent? evt)
    {
        base.TouchesEnded(touches, evt);
        InputSource?.HandleTouches(ToTouchArray(touches), evt, MauiTouchPhase.Ended);
    }

    public override void TouchesCancelled(NSSet touches, UIEvent? evt)
    {
        base.TouchesCancelled(touches, evt);
        InputSource?.HandleTouches(ToTouchArray(touches), evt, MauiTouchPhase.Cancelled);
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        Presenter?.OnDrawableSizeChanged(DrawableSize);
    }

    private sealed class MetalViewDelegate : MTKViewDelegate
    {
        private readonly MauiMetalView _owner;

        public MetalViewDelegate(MauiMetalView owner)
        {
            _owner = owner;
        }

        public override void DrawableSizeWillChange(MTKView view, CGSize size)
            => _owner.Presenter?.OnDrawableSizeChanged(size);

        public override void Draw(MTKView view)
            => _owner.Presenter?.RenderCurrentDrawable(view);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }

    private static UITouch[] ToTouchArray(NSSet touches)
        => touches is { Count: > 0 } ? touches.ToArray<UITouch>() ?? Array.Empty<UITouch>() : Array.Empty<UITouch>();
}
#endif
