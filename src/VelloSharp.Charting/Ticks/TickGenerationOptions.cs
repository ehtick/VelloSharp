using System;

namespace VelloSharp.Charting.Ticks;

/// <summary>
/// Configures tick generation behavior.
/// </summary>
/// <typeparam name="T">Domain value type.</typeparam>
public sealed class TickGenerationOptions<T>
{
    private int _targetTickCount = 6;

    /// <summary>
    /// Gets or sets desired tick count. Defaults to 6.
    /// </summary>
    public int TargetTickCount
    {
        get => _targetTickCount;
        init
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Target tick count must be positive.");
            }

            _targetTickCount = value;
        }
    }

    /// <summary>
    /// Optional delegate for label formatting.
    /// </summary>
    public Func<T, string>? LabelFormatter { get; init; }
}
