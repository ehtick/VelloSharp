# VelloSharp.Integration.WinForms

`VelloSharp.Integration.WinForms` enables Windows Forms applications to host Vello-powered rendering using familiar control patterns.

## Getting Started

1. Install with `dotnet add package VelloSharp.Integration.WinForms`.
2. Import `using VelloSharp.Integration.WinForms;` in your WinForms project.
3. Place the provided control wrappers on your form and connect them to scenes produced through `VelloSharp` or the charting components.
4. Handle resize and invalidate events using the helper methods supplied in the package to keep frame presentation smooth.

## Usage Example

```csharp
using System.Windows.Forms;
using VelloSharp.WinForms.Integration;

var control = new VelloRenderControl { Dock = DockStyle.Fill };
control.PaintSurface += (_, e) =>
{
    // Build your scene with e.Session.Scene and render via e.Session.Renderer.
};

var form = new Form { Text = "VelloSharp WinForms" };
form.Controls.Add(control);
Application.Run(form);
```

## Next Steps

- Inspect the API reference for the control surface APIs and lifecycle callbacks.
- Review the WinForms samples to observe multithreading considerations and device sharing strategies.

