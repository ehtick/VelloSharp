using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia;
using Avalonia.Rendering;

namespace VelloSharp.Avalonia.Browser;

internal static class VelloBrowserRenderLoopManager
{
    private const string RenderLoopInterfaceTypeName = "Avalonia.Rendering.IRenderLoop, Avalonia.Base";
    private const string RenderLoopTypeName = "Avalonia.Rendering.RenderLoop, Avalonia.Base";

    private static readonly object s_syncRoot = new();
    private static Type? s_renderLoopInterfaceType;
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
    private static Type? s_renderLoopType;
    private static object? s_renderLoopInstance;
    private static IReadOnlyDictionary<Type, object>? s_cachedFeatures;

    public static void EnsureRegistered(
        AvaloniaLocator locator,
        IRenderTimer timer,
        ref bool registrationFlag)
    {
        lock (s_syncRoot)
        {
            if (registrationFlag)
            {
                return;
            }

            s_renderLoopInterfaceType ??= Type.GetType(RenderLoopInterfaceTypeName);
            s_renderLoopType ??= Type.GetType(RenderLoopTypeName);

            if (s_renderLoopInterfaceType is null || s_renderLoopType is null)
            {
                registrationFlag = true;
                return;
            }

            var existing = AvaloniaLocator.Current.GetService(s_renderLoopInterfaceType);
            if (existing is null)
            {
                var ctor = s_renderLoopType.GetConstructor(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    binder: null,
                    new[] { typeof(IRenderTimer) },
                    modifiers: null);

                if (ctor is null)
                {
                    registrationFlag = true;
                    return;
                }

                existing = ctor.Invoke(new object[] { timer });
                Register(locator, s_renderLoopInterfaceType, s_renderLoopType, existing);
            }

            s_renderLoopInstance = existing;
            registrationFlag = true;
        }
    }

    public static IReadOnlyDictionary<Type, object> GetRenderLoopFeatures(IReadOnlyDictionary<Type, object> fallbackFeatures)
    {
        lock (s_syncRoot)
        {
            if (!TryResolveRenderLoop(out var renderLoop, out var renderLoopInterfaceType))
            {
                return fallbackFeatures;
            }

            if (s_cachedFeatures is Dictionary<Type, object> cached)
            {
                cached[renderLoopInterfaceType] = renderLoop;
                return cached;
            }

            var features = new Dictionary<Type, object>
            {
                [renderLoopInterfaceType] = renderLoop
            };

            s_cachedFeatures = features;
            return features;
        }
    }

    private static bool TryResolveRenderLoop(out object renderLoop, out Type renderLoopInterfaceType)
    {
        renderLoop = null!;
        renderLoopInterfaceType = null!;

        var interfaceType = s_renderLoopInterfaceType ?? Type.GetType(RenderLoopInterfaceTypeName);
        if (interfaceType is null)
        {
            return false;
        }

        var instance = s_renderLoopInstance ?? AvaloniaLocator.Current.GetService(interfaceType);
        if (instance is null)
        {
            return false;
        }

        s_renderLoopInterfaceType ??= interfaceType;
        s_renderLoopInstance ??= instance;

        renderLoopInterfaceType = interfaceType;
        renderLoop = instance;
        return true;
    }

    private static void Register(
        AvaloniaLocator locator,
        Type serviceType,
        Type implementationType,
        object instance)
    {
        var bindMethod = typeof(AvaloniaLocator)
            .GetMethod(nameof(AvaloniaLocator.Bind), BindingFlags.Public | BindingFlags.Instance);
        if (bindMethod is null)
        {
            return;
        }

        var registration = bindMethod.MakeGenericMethod(serviceType).Invoke(locator, null);
        if (registration is null)
        {
            return;
        }

        var toConstantMethod = registration.GetType()
            .GetMethod("ToConstant", BindingFlags.Public | BindingFlags.Instance);
        if (toConstantMethod is null)
        {
            return;
        }

        var genericToConstant = toConstantMethod.MakeGenericMethod(implementationType);
        genericToConstant.Invoke(registration, new[] { instance });
    }
}
