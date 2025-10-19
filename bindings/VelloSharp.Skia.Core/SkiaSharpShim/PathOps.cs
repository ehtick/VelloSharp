using System;
using System.Collections.Generic;
using System.Numerics;
using SkiaSharp;
using VelloSharp;

namespace SkiaSharpShim;

internal static class PathOps
{
    private const float ManagedCellTargetSize = 2f;
    private const int ManagedMaxCellsPerAxis = 256;
    private const float ManagedMinBoundsSize = 1f;

    public static bool TryCompute(SKPath first, SKPath second, SKPathOp operation, out SKPath? result)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        result = null;

        if (TryHandleTrivialCases(first, second, operation, out result))
        {
            return true;
        }

        if (!HasInverseFill(first.FillType) && !HasInverseFill(second.FillType))
        {
            if (TryNative(first, second, operation, out result))
            {
                return true;
            }
        }

        return TryManaged(first, second, operation, out result);
    }

    public static SKPath? Compute(SKPath first, SKPath second, SKPathOp operation)
        => TryCompute(first, second, operation, out var path) ? path : null;

    private static bool TryHandleTrivialCases(SKPath first, SKPath second, SKPathOp operation, out SKPath? result)
    {
        result = null;
        var firstEmpty = first.IsEmpty;
        var secondEmpty = second.IsEmpty;

        if (!firstEmpty && !secondEmpty)
        {
            return false;
        }

        SKPath? CreateClone(SKPath source)
        {
            var clone = new SKPath(source);
            clone.FillType = ResolveFillType(first.FillType, second.FillType);
            return clone;
        }

        switch (operation)
        {
            case SKPathOp.Difference:
                result = firstEmpty ? CreateEmptyResult(first.FillType, second.FillType) : CreateClone(first);
                return true;
            case SKPathOp.ReverseDifference:
                result = secondEmpty ? CreateEmptyResult(first.FillType, second.FillType) : CreateClone(second);
                return true;
            case SKPathOp.Intersect:
                result = CreateEmptyResult(first.FillType, second.FillType);
                return true;
            case SKPathOp.Union:
                if (firstEmpty && secondEmpty)
                {
                    result = CreateEmptyResult(first.FillType, second.FillType);
                }
                else if (firstEmpty)
                {
                    result = CreateClone(second);
                }
                else
                {
                    result = CreateClone(first);
                }
                return true;
            case SKPathOp.Xor:
                if (firstEmpty && secondEmpty)
                {
                    result = CreateEmptyResult(first.FillType, second.FillType);
                }
                else if (firstEmpty)
                {
                    result = CreateClone(second);
                }
                else
                {
                    result = CreateClone(first);
                }
                return true;
            default:
                return false;
        }
    }

    private static bool TryNative(SKPath first, SKPath second, SKPathOp operation, out SKPath? result)
    {
        result = null;

        if (!TryMapOperation(operation, out var nativeOp, out var swapInputs))
        {
            return false;
        }

        var left = swapInputs ? second : first;
        var right = swapInputs ? first : second;

        var fillRule = ResolveFillRule(left.FillType, right.FillType);

        var leftBuilder = left.ToPathBuilder();
        var rightBuilder = right.ToPathBuilder();

        using var nativeLeft = VelloSharp.NativePathElements.Rent(leftBuilder);
        using var nativeRight = VelloSharp.NativePathElements.Rent(rightBuilder);
        var spanLeft = nativeLeft.Span;
        var spanRight = nativeRight.Span;

        if (spanLeft.IsEmpty || spanRight.IsEmpty)
        {
            return false;
        }

        IntPtr handle = IntPtr.Zero;
        try
        {
            unsafe
            {
                fixed (VelloPathElement* ptrLeft = spanLeft)
                fixed (VelloPathElement* ptrRight = spanRight)
                {
                    var status = NativeMethods.vello_path_boolean_op(
                        ptrLeft,
                        (nuint)spanLeft.Length,
                        ptrRight,
                        (nuint)spanRight.Length,
                        fillRule,
                        nativeOp,
                        out handle);

                    if (status != VelloStatus.Success)
                    {
                        return false;
                    }
                }
            }

            var fillType = ResolveFillType(first.FillType, second.FillType);
            result = CreatePathFromCommandList(handle, fillType);
            return true;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                NativeMethods.vello_path_command_list_destroy(handle);
            }
        }
    }

    private static bool TryManaged(SKPath first, SKPath second, SKPathOp operation, out SKPath? result)
    {
        result = null;

        if (HasInverseFill(first.FillType) || HasInverseFill(second.FillType))
        {
            return false;
        }

        var bounds = first.TightBounds;
        bounds.Union(second.TightBounds);

        if (bounds.IsEmpty)
        {
            result = CreateEmptyResult(first.FillType, second.FillType);
            return true;
        }

        var width = MathF.Max(bounds.Width, ManagedMinBoundsSize);
        var height = MathF.Max(bounds.Height, ManagedMinBoundsSize);

        var gridWidth = Math.Clamp((int)MathF.Ceiling(width / ManagedCellTargetSize), 1, ManagedMaxCellsPerAxis);
        var gridHeight = Math.Clamp((int)MathF.Ceiling(height / ManagedCellTargetSize), 1, ManagedMaxCellsPerAxis);

        var cellWidth = width / gridWidth;
        var cellHeight = height / gridHeight;

        var rowRuns = new List<(int Start, int End)>(gridWidth);
        var activeRects = new List<ActiveRect>();
        var completedRects = new List<ActiveRect>();

        for (var y = 0; y < gridHeight; y++)
        {
            rowRuns.Clear();
            var sampleY = bounds.Top + (y + 0.5f) * cellHeight;
            var inRun = false;
            var runStart = 0;

            for (var x = 0; x < gridWidth; x++)
            {
                var sampleX = bounds.Left + (x + 0.5f) * cellWidth;
                var inside = EvaluateBoolean(first, second, operation, new Vector2(sampleX, sampleY));
                if (inside)
                {
                    if (!inRun)
                    {
                        inRun = true;
                        runStart = x;
                    }
                }
                else if (inRun)
                {
                    rowRuns.Add((runStart, x));
                    inRun = false;
                }
            }

            if (inRun)
            {
                rowRuns.Add((runStart, gridWidth));
            }

            var nextActive = new List<ActiveRect>(rowRuns.Count);
            foreach (var run in rowRuns)
            {
                var matched = false;
                for (var i = 0; i < activeRects.Count; i++)
                {
                    var rect = activeRects[i];
                    if (rect.StartX == run.Start && rect.EndX == run.End)
                    {
                        rect.EndY = y;
                        nextActive.Add(rect);
                        activeRects.RemoveAt(i);
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    nextActive.Add(new ActiveRect(run.Start, run.End, y, y));
                }
            }

            if (activeRects.Count > 0)
            {
                completedRects.AddRange(activeRects);
            }

            activeRects = nextActive;
        }

        if (activeRects.Count > 0)
        {
            completedRects.AddRange(activeRects);
        }

        var path = CreateEmptyResult(first.FillType, second.FillType);
        foreach (var rect in completedRects)
        {
            if (rect.EndY < rect.StartY || rect.EndX <= rect.StartX)
            {
                continue;
            }

            var left = bounds.Left + rect.StartX * cellWidth;
            var top = bounds.Top + rect.StartY * cellHeight;
            var right = bounds.Left + rect.EndX * cellWidth;
            var bottom = bounds.Top + (rect.EndY + 1) * cellHeight;

            if (right - left <= float.Epsilon || bottom - top <= float.Epsilon)
            {
                continue;
            }

            path.AddRect(new SKRect(left, top, right, bottom));
        }

        result = path;
        return true;
    }

    private static bool EvaluateBoolean(SKPath first, SKPath second, SKPathOp operation, Vector2 sample)
    {
        var insideFirst = first.Contains(sample.X, sample.Y);
        var insideSecond = second.Contains(sample.X, sample.Y);

        return operation switch
        {
            SKPathOp.Difference => insideFirst && !insideSecond,
            SKPathOp.Intersect => insideFirst && insideSecond,
            SKPathOp.Union => insideFirst || insideSecond,
            SKPathOp.Xor => insideFirst ^ insideSecond,
            SKPathOp.ReverseDifference => insideSecond && !insideFirst,
            _ => false,
        };
    }

    internal static SKPath CreatePathFromCommandList(IntPtr handle, SKPathFillType fillType)
    {
        var path = new SKPath
        {
            FillType = fillType,
        };

        if (handle == IntPtr.Zero)
        {
            return path;
        }

        var status = NativeMethods.vello_path_command_list_get_data(handle, out var commandList);
        if (status != VelloStatus.Success || commandList.CommandCount == 0 || commandList.Commands == IntPtr.Zero)
        {
            return path;
        }

        var count = checked((int)commandList.CommandCount);
        unsafe
        {
            var span = new ReadOnlySpan<VelloPathElement>((void*)commandList.Commands, count);
            AppendElements(path, span);
        }

        return path;
    }

    internal static void AppendElements(SKPath path, ReadOnlySpan<VelloPathElement> elements)
    {
        foreach (var element in elements)
        {
            switch (element.Verb)
            {
                case VelloPathVerb.MoveTo:
                    path.MoveTo((float)element.X0, (float)element.Y0);
                    break;
                case VelloPathVerb.LineTo:
                    path.LineTo((float)element.X0, (float)element.Y0);
                    break;
                case VelloPathVerb.QuadTo:
                    path.QuadTo(
                        new SKPoint((float)element.X0, (float)element.Y0),
                        new SKPoint((float)element.X1, (float)element.Y1));
                    break;
                case VelloPathVerb.CubicTo:
                    path.CubicTo(
                        new SKPoint((float)element.X0, (float)element.Y0),
                        new SKPoint((float)element.X1, (float)element.Y1),
                        new SKPoint((float)element.X2, (float)element.Y2));
                    break;
                case VelloPathVerb.Close:
                    path.Close();
                    break;
            }
        }
    }

    private static bool TryMapOperation(SKPathOp operation, out VelloPathBooleanOp nativeOp, out bool swapInputs)
    {
        swapInputs = false;
        nativeOp = operation switch
        {
            SKPathOp.Difference => VelloPathBooleanOp.Difference,
            SKPathOp.Intersect => VelloPathBooleanOp.Intersection,
            SKPathOp.Union => VelloPathBooleanOp.Union,
            SKPathOp.Xor => VelloPathBooleanOp.Xor,
            SKPathOp.ReverseDifference => VelloPathBooleanOp.Difference,
            _ => default,
        };

        if (operation == SKPathOp.ReverseDifference)
        {
            swapInputs = true;
            return true;
        }

        return operation is SKPathOp.Difference or SKPathOp.Intersect or SKPathOp.Union or SKPathOp.Xor;
    }

    private static SKPath CreateEmptyResult(SKPathFillType first, SKPathFillType second)
    {
        var path = new SKPath();
        path.FillType = ResolveFillType(first, second);
        return path;
    }

    private static VelloFillRule ResolveFillRule(SKPathFillType first, SKPathFillType second)
        => (IsEvenOdd(first) || IsEvenOdd(second)) ? VelloFillRule.EvenOdd : VelloFillRule.NonZero;

    private static SKPathFillType ResolveFillType(SKPathFillType first, SKPathFillType second)
        => (IsEvenOdd(first) || IsEvenOdd(second)) ? SKPathFillType.EvenOdd : SKPathFillType.Winding;

    private static bool IsEvenOdd(SKPathFillType value)
        => value is SKPathFillType.EvenOdd or SKPathFillType.InverseEvenOdd;

    internal static bool HasInverseFill(SKPathFillType value)
        => value is SKPathFillType.InverseEvenOdd or SKPathFillType.InverseWinding;

    private struct ActiveRect
    {
        public ActiveRect(int startX, int endX, int startY, int endY)
        {
            StartX = startX;
            EndX = endX;
            StartY = startY;
            EndY = endY;
        }

        public int StartX { get; }
        public int EndX { get; }
        public int StartY { get; }
        public int EndY { get; set; }
    }
}
