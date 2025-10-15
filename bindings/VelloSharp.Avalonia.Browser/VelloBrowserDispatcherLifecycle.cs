using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;

namespace VelloSharp.Avalonia.Browser;

/// <summary>
/// Coordinates browser-specific dispatcher lifecycle events and exposes visibility changes.
/// </summary>
internal static class VelloBrowserDispatcherLifecycle
{
    private static readonly object s_syncRoot = new();
    private static bool s_initialized;
    private static IActivatableLifetime? s_lifetime;
    private static int s_visibilityState = 1;

    public static event Action<bool>? VisibilityChanged;

    public static bool IsVisible => Volatile.Read(ref s_visibilityState) != 0;

    public static void EnsureInitialized()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(EnsureInitialized, DispatcherPriority.Send);
            return;
        }

        lock (s_syncRoot)
        {
            if (s_initialized)
            {
                return;
            }

            s_initialized = true;
        }

        Dispatcher.UIThread.ShutdownStarted += OnShutdownStarted;
        AttachLifetimeListener();
    }

    private static void AttachLifetimeListener()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(AttachLifetimeListener, DispatcherPriority.Background);
            return;
        }

        var lifetime = Application.Current?.TryGetFeature<IActivatableLifetime>()
                      ?? AvaloniaLocator.Current.GetService<IActivatableLifetime>();

        if (lifetime is null)
        {
            Dispatcher.UIThread.Post(AttachLifetimeListener, DispatcherPriority.Background);
            return;
        }

        lock (s_syncRoot)
        {
            if (!s_initialized || ReferenceEquals(s_lifetime, lifetime))
            {
                return;
            }

            if (s_lifetime is not null)
            {
                s_lifetime.Activated -= OnActivated;
                s_lifetime.Deactivated -= OnDeactivated;
            }

            s_lifetime = lifetime;
            s_lifetime.Activated += OnActivated;
            s_lifetime.Deactivated += OnDeactivated;
        }
    }

    private static void OnActivated(object? sender, ActivatedEventArgs e)
    {
        if (e.Kind == ActivationKind.Background)
        {
            UpdateVisibility(true);
        }
    }

    private static void OnDeactivated(object? sender, ActivatedEventArgs e)
    {
        if (e.Kind == ActivationKind.Background)
        {
            UpdateVisibility(false);
        }
    }

    private static void OnShutdownStarted(object? sender, EventArgs e)
    {
        UpdateVisibility(false);

        IActivatableLifetime? lifetime;
        lock (s_syncRoot)
        {
            lifetime = s_lifetime;
            s_lifetime = null;
            s_initialized = false;
        }

        if (lifetime is not null)
        {
            lifetime.Activated -= OnActivated;
            lifetime.Deactivated -= OnDeactivated;
        }

        Dispatcher.UIThread.ShutdownStarted -= OnShutdownStarted;
    }

    private static void UpdateVisibility(bool isVisible)
    {
        var newValue = isVisible ? 1 : 0;
        var previous = Interlocked.Exchange(ref s_visibilityState, newValue);
        if (previous == newValue)
        {
            return;
        }

        VisibilityChanged?.Invoke(isVisible);
    }
}
