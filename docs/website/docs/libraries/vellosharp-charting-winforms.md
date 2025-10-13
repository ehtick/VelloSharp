# VelloSharp.Charting.WinForms

`VelloSharp.Charting.WinForms` adapts the charting controls to the Windows Forms framework, balancing legacy investments with modern rendering performance.

## Getting Started

1. Install via `dotnet add package VelloSharp.Charting.WinForms`.
2. Import `using VelloSharp.Charting.WinForms;` in your WinForms project.
3. Drag the provided controls onto your forms (or instantiate them in code) and bind them to data produced by `VelloSharp.ChartData`.
4. Tie the controls into your rendering loop using helpers from `VelloSharp.Integration.WinForms` to keep frames synchronized with the UI thread.

## Usage Example

```csharp
using System;
using System.Windows.Forms;
using VelloSharp.Charting.WinForms;

[STAThread]
public static void Main()
{
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);

    var chart = new ChartView { Dock = DockStyle.Fill };
    Application.Run(new Form { Text = "WinForms Chart", Controls = { chart } });
}
```

## Next Steps

- Explore the API reference for available controls, events, and customization hooks.
- Check the `samples/WinFormsMotionMarkShim` or other WinForms demos for patterns that balance performance and responsiveness.

