namespace VelloSharp.Composition.Input;

public interface ICompositionInputSink
{
    void ProcessPointerEvent(CompositionPointerEventArgs args);

    void ProcessKeyEvent(CompositionKeyEventArgs args);

    void ProcessTextInput(CompositionTextInputEventArgs args);

    void ProcessFocusChanged(bool isFocused);
}
