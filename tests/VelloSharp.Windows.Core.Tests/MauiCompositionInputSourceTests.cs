#nullable enable

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VelloSharp.Composition.Input;
using VelloSharp.Maui.Controls;
using VelloSharp.Maui.Diagnostics;
using VelloSharp.Maui.Events;
using VelloSharp.Maui.Input;
using VelloSharp.WinForms.Integration;
using Xunit;

namespace VelloSharp.Windows.Core.Tests;

internal static class DeviceTestCategory
{
    public const string Input = "Input";
}

public class MauiCompositionInputSourceTests
{
    [Fact]
    [Trait("Category", DeviceTestCategory.Input)]
    public void GotAndLostFocusForwardedToSink()
    {
        var view = new TestVelloView();
        var element = new TestFocusControl();
        using var source = new MauiCompositionInputSource(view, element);
        var sink = new TestCompositionInputSink();

        source.Connect(sink);

        element.RaiseGotFocus();
        Assert.True(sink.IsFocused);

        element.RaiseLostFocus();
        Assert.False(sink.IsFocused);
    }

    private sealed class TestFocusControl : Control
    {
        public void RaiseGotFocus() => OnGotFocus(new RoutedEventArgs());

        public void RaiseLostFocus() => OnLostFocus(new RoutedEventArgs());

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            GotFocus?.Invoke(this, e);
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);
            LostFocus?.Invoke(this, e);
        }

        protected override bool Focus(FocusState value)
        {
            FocusRequested = true;
            return base.Focus(value);
        }
    }

    private sealed class TestCompositionInputSink : ICompositionInputSink
    {
        public bool IsFocused { get; private set; }

        public void ProcessPointerEvent(CompositionPointerEventArgs args)
        {
        }

        public void ProcessKeyEvent(CompositionKeyEventArgs args)
        {
        }

        public void ProcessTextInput(CompositionTextInputEventArgs args)
        {
        }

        public void ProcessFocusChanged(bool isFocused)
        {
            IsFocused = isFocused;
        }
    }

    private sealed class TestVelloView : IVelloView
    {
        private readonly VelloViewDiagnostics _diagnostics = new();

        public VelloGraphicsDeviceOptions DeviceOptions => VelloGraphicsDeviceOptions.Default;

        public VelloRenderBackend PreferredBackend => VelloRenderBackend.Gpu;

        public VelloRenderMode RenderMode => VelloRenderMode.OnDemand;

        public RenderLoopDriver RenderLoopDriver => RenderLoopDriver.CompositionTarget;

        public bool IsDiagnosticsEnabled => false;

        public bool UseTextureView => false;

        public bool SuppressGraphicsViewCompositor => false;

        public bool IsInDesignMode => false;

        public VelloViewDiagnostics Diagnostics => _diagnostics;

        public void InvalidateSurface()
        {
        }

        public void OnDiagnosticsUpdated(VelloDiagnosticsChangedEventArgs args)
        {
        }

        public void OnGpuUnavailable(string? message)
        {
        }

        public void OnPaintSurface(VelloPaintSurfaceEventArgs args)
        {
        }

        public void OnRenderSurface(VelloSurfaceRenderEventArgs args)
        {
        }
    }
}
