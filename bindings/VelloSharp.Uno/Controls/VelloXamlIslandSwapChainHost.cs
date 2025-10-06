#if HAS_UNO

using System;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VelloSharp.Windows;

namespace VelloSharp.Uno.Controls;

/// <summary>
/// Provides a XAML Islands bridge that hosts the WPF <c>VelloNativeSwapChainView</c> inside Uno's Win32 head.
/// </summary>
public sealed class VelloXamlIslandSwapChainHost : ContentControl
{
    public static readonly DependencyProperty DeviceOptionsProperty = DependencyProperty.Register(
        nameof(DeviceOptions),
        typeof(VelloGraphicsDeviceOptions),
        typeof(VelloXamlIslandSwapChainHost),
        new PropertyMetadata(VelloGraphicsDeviceOptions.Default, OnDeviceOptionsChanged));

    public static readonly DependencyProperty PreferredBackendProperty = DependencyProperty.Register(
        nameof(PreferredBackend),
        typeof(VelloRenderBackend),
        typeof(VelloXamlIslandSwapChainHost),
        new PropertyMetadata(VelloRenderBackend.Gpu, OnPreferredBackendChanged));

    public static readonly DependencyProperty RenderModeProperty = DependencyProperty.Register(
        nameof(RenderMode),
        typeof(VelloRenderMode),
        typeof(VelloXamlIslandSwapChainHost),
        new PropertyMetadata(VelloRenderMode.OnDemand, OnRenderModeChanged));

    public static readonly DependencyProperty RenderLoopDriverProperty = DependencyProperty.Register(
        nameof(RenderLoopDriver),
        typeof(RenderLoopDriver),
        typeof(VelloXamlIslandSwapChainHost),
        new PropertyMetadata(RenderLoopDriver.CompositionTarget, OnRenderLoopDriverChanged));

    private readonly object? _nativeView;

    public VelloXamlIslandSwapChainHost()
    {
        _nativeView = CreateNativeView();

        if (_nativeView is UIElement element)
        {
            Content = element;
        }
        else if (_nativeView is not null)
        {
            Content = _nativeView;
        }
        else
        {
            Content = CreateFallback();
        }

        ApplyAllProperties();
    }

    public object? NativeView => _nativeView;

    public bool IsNativeViewAvailable => _nativeView is not null;

    public VelloGraphicsDeviceOptions DeviceOptions
    {
        get => GetValue(DeviceOptionsProperty) as VelloGraphicsDeviceOptions ?? VelloGraphicsDeviceOptions.Default;
        set => SetValue(DeviceOptionsProperty, value ?? VelloGraphicsDeviceOptions.Default);
    }

    public VelloRenderBackend PreferredBackend
    {
        get => (VelloRenderBackend)(GetValue(PreferredBackendProperty) ?? VelloRenderBackend.Gpu);
        set => SetValue(PreferredBackendProperty, value);
    }

    public VelloRenderMode RenderMode
    {
        get => (VelloRenderMode)(GetValue(RenderModeProperty) ?? VelloRenderMode.OnDemand);
        set => SetValue(RenderModeProperty, value);
    }

    public RenderLoopDriver RenderLoopDriver
    {
        get => (RenderLoopDriver)(GetValue(RenderLoopDriverProperty) ?? RenderLoopDriver.CompositionTarget);
        set => SetValue(RenderLoopDriverProperty, value);
    }

    public void RequestRender()
    {
        InvokeNativeMethod(nameof(RequestRender));
    }

    private static void OnDeviceOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloXamlIslandSwapChainHost host)
        {
            host.ApplyProperty(nameof(DeviceOptions), e.NewValue ?? VelloGraphicsDeviceOptions.Default);
        }
    }

    private static void OnPreferredBackendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloXamlIslandSwapChainHost host)
        {
            host.ApplyProperty(nameof(PreferredBackend), e.NewValue ?? VelloRenderBackend.Gpu);
        }
    }

    private static void OnRenderModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloXamlIslandSwapChainHost host)
        {
            host.ApplyProperty(nameof(RenderMode), e.NewValue ?? VelloRenderMode.OnDemand);
        }
    }

    private static void OnRenderLoopDriverChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VelloXamlIslandSwapChainHost host)
        {
            host.ApplyProperty(nameof(RenderLoopDriver), e.NewValue ?? RenderLoopDriver.CompositionTarget);
        }
    }

    private static object? CreateNativeView()
    {
        var type = Type.GetType(
            "VelloSharp.Wpf.Integration.VelloNativeSwapChainView, VelloSharp.Integration.Wpf",
            throwOnError: false);
        if (type is null)
        {
            return null;
        }

        try
        {
            return Activator.CreateInstance(type);
        }
        catch
        {
            return null;
        }
    }

    private void ApplyAllProperties()
    {
        ApplyProperty(nameof(DeviceOptions), DeviceOptions);
        ApplyProperty(nameof(PreferredBackend), PreferredBackend);
        ApplyProperty(nameof(RenderMode), RenderMode);
        ApplyProperty(nameof(RenderLoopDriver), RenderLoopDriver);
    }

    private void ApplyProperty(string propertyName, object? value)
    {
        if (_nativeView is null)
        {
            return;
        }

        var property = _nativeView.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property?.CanWrite != true)
        {
            return;
        }

        var converted = ConvertValue(property.PropertyType, value);
        if (converted is null && property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null)
        {
            return;
        }

        property.SetValue(_nativeView, converted);
    }

    private void InvokeNativeMethod(string methodName)
    {
        if (_nativeView is null)
        {
            return;
        }

        var method = _nativeView.GetType().GetMethod(methodName, Type.EmptyTypes);
        method?.Invoke(_nativeView, Array.Empty<object>());
    }

    private static UIElement CreateFallback()
    {
        return new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(global::Windows.UI.Color.FromArgb(12, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(global::Windows.UI.Color.FromArgb(32, 0, 0, 0)),
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock
            {
                Text = "VelloSwapChain is not available in this head. Add a reference to VelloSharp.Integration.Wpf when using the Win32/XAML Islands target.",
                TextWrapping = TextWrapping.WrapWholeWords,
            },
        };
    }

    private static object? ConvertValue(Type propertyType, object? value)
    {
        if (value is null)
        {
            return propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) is null
                ? Activator.CreateInstance(propertyType)
                : null;
        }

        if (propertyType.IsInstanceOfType(value))
        {
            return value;
        }

        if (propertyType.IsEnum)
        {
            if (value is Enum enumValue)
            {
                var name = enumValue.ToString();
                if (!string.IsNullOrEmpty(name))
                {
                    try
                    {
                        return Enum.Parse(propertyType, name);
                    }
                    catch
                    {
                        // Fall through to return default enum value.
                    }
                }
            }

            var values = propertyType.GetEnumValues();
            return values.Length > 0 ? values.GetValue(0) : Activator.CreateInstance(propertyType);
        }

        return value;
    }
}

#endif
