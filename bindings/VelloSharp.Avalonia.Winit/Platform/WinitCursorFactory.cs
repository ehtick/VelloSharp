using System;
using Avalonia.Input;
using Avalonia.Platform;
using VelloSharp;

namespace Avalonia.Winit;

internal sealed class WinitCursorFactory : ICursorFactory
{
    internal sealed class WinitCursorImpl : ICursorImpl
    {
        public WinitCursorImpl(WinitCursorIcon icon, bool isHidden)
        {
            Icon = icon;
            IsHidden = isHidden;
        }

        public WinitCursorIcon Icon { get; }

        public bool IsHidden { get; }

        public void Dispose()
        {
        }
    }

    public ICursorImpl GetCursor(StandardCursorType cursorType)
    {
        var (icon, hidden) = MapCursor(cursorType);
        return new WinitCursorImpl(icon, hidden);
    }

    public ICursorImpl CreateCursor(IBitmapImpl cursor, PixelPoint hotSpot)
    {
        throw new NotSupportedException("Custom cursors are not supported by the Winit backend yet.");
    }

    private static (WinitCursorIcon Icon, bool Hidden) MapCursor(StandardCursorType cursorType)
    {
        return cursorType switch
        {
            StandardCursorType.None => (WinitCursorIcon.Default, true),
            StandardCursorType.Arrow => (WinitCursorIcon.Default, false),
            StandardCursorType.Ibeam => (WinitCursorIcon.Text, false),
            StandardCursorType.Wait => (WinitCursorIcon.Wait, false),
            StandardCursorType.Cross => (WinitCursorIcon.Crosshair, false),
            StandardCursorType.UpArrow => (WinitCursorIcon.Default, false),
            StandardCursorType.SizeWestEast => (WinitCursorIcon.EwResize, false),
            StandardCursorType.SizeNorthSouth => (WinitCursorIcon.NsResize, false),
            StandardCursorType.SizeAll => (WinitCursorIcon.AllScroll, false),
            StandardCursorType.No => (WinitCursorIcon.NotAllowed, false),
            StandardCursorType.Hand => (WinitCursorIcon.Pointer, false),
            StandardCursorType.AppStarting => (WinitCursorIcon.Progress, false),
            StandardCursorType.Help => (WinitCursorIcon.Help, false),
            StandardCursorType.TopSide => (WinitCursorIcon.NResize, false),
            StandardCursorType.BottomSide => (WinitCursorIcon.SResize, false),
            StandardCursorType.LeftSide => (WinitCursorIcon.WResize, false),
            StandardCursorType.RightSide => (WinitCursorIcon.EResize, false),
            StandardCursorType.TopLeftCorner => (WinitCursorIcon.NwResize, false),
            StandardCursorType.TopRightCorner => (WinitCursorIcon.NeResize, false),
            StandardCursorType.BottomLeftCorner => (WinitCursorIcon.SwResize, false),
            StandardCursorType.BottomRightCorner => (WinitCursorIcon.SeResize, false),
            StandardCursorType.DragMove => (WinitCursorIcon.Move, false),
            StandardCursorType.DragCopy => (WinitCursorIcon.Copy, false),
            StandardCursorType.DragLink => (WinitCursorIcon.Alias, false),
            _ => (WinitCursorIcon.Default, false),
        };
    }
}
