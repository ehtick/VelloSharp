using System;
using Avalonia.Input;
using Avalonia.Platform;

namespace Avalonia.Winit;

internal sealed class WinitCursorFactory : ICursorFactory
{
    private sealed class CursorStub : ICursorImpl
    {
        public void Dispose()
        {
        }
    }

    public ICursorImpl GetCursor(StandardCursorType cursorType) => new CursorStub();

    public ICursorImpl CreateCursor(IBitmapImpl cursor, PixelPoint hotSpot)
    {
        throw new NotSupportedException("Custom cursors are not supported by the Winit backend yet.");
    }
}
