using System;

namespace VelloSharp.Composition.Input;

[Flags]
public enum InputModifiers
{
    None = 0,
    Shift = 1 << 0,
    Control = 1 << 1,
    Alt = 1 << 2,
    Meta = 1 << 3,
    PrimaryButton = 1 << 4,
    SecondaryButton = 1 << 5,
    MiddleButton = 1 << 6,
    XButton1 = 1 << 7,
    XButton2 = 1 << 8,
}
