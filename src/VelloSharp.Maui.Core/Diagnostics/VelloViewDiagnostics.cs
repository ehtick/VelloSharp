using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VelloSharp.Maui.Diagnostics;

/// <summary>
/// Tracks frame timing and GPU diagnostics in a platform-agnostic way so views can surface perf insights.
/// </summary>
public sealed class VelloViewDiagnostics : INotifyPropertyChanged
{
    private const double FrameRateSmoothing = 0.2;

    private double _framesPerSecond;
    private long _swapChainResets;
    private long _keyedMutexContention;
    private string? _lastError;
    private IReadOnlyDictionary<string, string>? _extendedProperties;

    public event PropertyChangedEventHandler? PropertyChanged;

    public double FramesPerSecond
    {
        get => _framesPerSecond;
        private set => SetFrameRate(value);
    }

    public long SwapChainResets
    {
        get => _swapChainResets;
        private set => SetField(ref _swapChainResets, value, nameof(SwapChainResets));
    }

    public long KeyedMutexContention
    {
        get => _keyedMutexContention;
        private set => SetField(ref _keyedMutexContention, value, nameof(KeyedMutexContention));
    }

    public string? LastError
    {
        get => _lastError;
        private set
        {
            if (SetField(ref _lastError, value, nameof(LastError)))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(_lastError);

    public IReadOnlyDictionary<string, string>? ExtendedProperties => _extendedProperties;

    internal void UpdateFrame(TimeSpan delta, VelloDiagnosticsSnapshot snapshot)
    {
        if (delta > TimeSpan.Zero)
        {
            var fps = 1.0 / delta.TotalSeconds;
            var smoothed = _framesPerSecond <= 0
                ? fps
                : (_framesPerSecond * (1 - FrameRateSmoothing)) + (fps * FrameRateSmoothing);
            FramesPerSecond = smoothed;
        }

        UpdateFromSnapshot(snapshot);
    }

    internal void UpdateFromSnapshot(VelloDiagnosticsSnapshot snapshot)
    {
        SwapChainResets = snapshot.SwapChainResets;
        KeyedMutexContention = snapshot.KeyedMutexContention;

        var error = snapshot.LastError;
        if (!string.IsNullOrWhiteSpace(error))
        {
            LastError = error;
        }

        SetField(ref _extendedProperties, snapshot.ExtendedProperties, nameof(ExtendedProperties));
    }

    internal void ResetFrameTiming()
    {
        FramesPerSecond = 0;
    }

    internal void ClearError()
    {
        if (_lastError is not null)
        {
            LastError = null;
        }
    }

    internal void ReportError(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            LastError = message;
        }
    }

    private void SetFrameRate(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            value = 0;
        }

        value = Math.Round(value, 2);
        SetField(ref _framesPerSecond, value, nameof(FramesPerSecond));
    }

    private bool SetField<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
