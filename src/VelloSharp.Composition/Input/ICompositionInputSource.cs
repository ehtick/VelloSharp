using System;

namespace VelloSharp.Composition.Input;

public interface ICompositionInputSource : IDisposable
{
    void Connect(ICompositionInputSink sink);

    void Disconnect(ICompositionInputSink sink);

    void RequestPointerCapture(ICompositionInputSink sink, ulong pointerId);

    void ReleasePointerCapture(ICompositionInputSink sink, ulong pointerId);

    void RequestFocus(ICompositionInputSink sink);
}
