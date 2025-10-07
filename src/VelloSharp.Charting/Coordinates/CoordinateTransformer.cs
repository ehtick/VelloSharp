using System;
using VelloSharp.Charting.Scales;
using VelloSharp.Charting.Units;

namespace VelloSharp.Charting.Coordinates;

/// <summary>
/// Converts between data domain coordinates and physical render units.
/// </summary>
public readonly struct CoordinateTransformer<TX, TY>
{
    private readonly IScale<TX> _xScale;
    private readonly IScale<TY> _yScale;
    private readonly UnitRange _xRange;
    private readonly UnitRange _yRange;
    private readonly bool _invertY;

    public static CoordinateTransformer<TX, TY> CreateForSize(IScale<TX> xScale, IScale<TY> yScale, double width, double height, bool invertY = true)
    {
        return new CoordinateTransformer<TX, TY>(
            xScale,
            yScale,
            new UnitRange(0d, width),
            new UnitRange(0d, height),
            invertY);
    }

    public CoordinateTransformer(IScale<TX> xScale, IScale<TY> yScale, UnitRange xRange, UnitRange yRange, bool invertY = true)
    {
        ArgumentNullException.ThrowIfNull(xScale);
        ArgumentNullException.ThrowIfNull(yScale);

        _xScale = xScale;
        _yScale = yScale;
        _xRange = xRange;
        _yRange = yRange;
        _invertY = invertY;
    }

    /// <summary>
    /// Projects a data point into physical coordinates.
    /// </summary>
    public ChartPoint Project(TX x, TY y)
    {
        var ux = _xScale.Project(x);
        var uy = _yScale.Project(y);

        if (_invertY)
        {
            uy = 1d - uy;
        }

        return new ChartPoint(
            _xRange.FromUnit(ux),
            _yRange.FromUnit(uy));
    }

    /// <summary>
    /// Projects the X component only.
    /// </summary>
    public double ProjectX(TX x)
    {
        var ux = _xScale.Project(x);
        return _xRange.FromUnit(ux);
    }

    /// <summary>
    /// Projects the Y component only.
    /// </summary>
    public double ProjectY(TY y)
    {
        var uy = _yScale.Project(y);
        if (_invertY)
        {
            uy = 1d - uy;
        }

        return _yRange.FromUnit(uy);
    }

    /// <summary>
    /// Unprojects a physical point back into data space.
    /// </summary>
    public ChartDataPoint<TX, TY> Unproject(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Point coordinates must be finite.");
        }

        var ux = _xRange.ToUnit(x);
        var uy = _yRange.ToUnit(y);

        if (_invertY)
        {
            uy = 1d - uy;
        }

        var dataX = _xScale.Unproject(ux);
        var dataY = _yScale.Unproject(uy);
        return new ChartDataPoint<TX, TY>(dataX, dataY);
    }
}
