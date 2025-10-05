using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace VelloSharp.WinForms;

public sealed class VelloStringFormat
{
    public static VelloStringFormat GenericDefault { get; } = new();

    public static VelloStringFormat GenericTypographic { get; } = new()
    {
        FormatFlags = StringFormatFlags.FitBlackBox | StringFormatFlags.NoWrap,
        LineAlignment = StringAlignment.Near,
        Alignment = StringAlignment.Near,
    };

    public StringAlignment Alignment { get; set; } = StringAlignment.Near;

    public StringAlignment LineAlignment { get; set; } = StringAlignment.Near;

    public StringFormatFlags FormatFlags { get; set; } = StringFormatFlags.NoClip;

    public StringTrimming Trimming { get; set; } = StringTrimming.None;

    public float LineSpacing { get; set; } = 1f;

    public float ParagraphSpacing { get; set; }

    public HotkeyPrefix HotkeyPrefix { get; set; } = HotkeyPrefix.None;

    public VelloStringFormat Clone()
    {
        return new VelloStringFormat
        {
            Alignment = Alignment,
            LineAlignment = LineAlignment,
            FormatFlags = FormatFlags,
            Trimming = Trimming,
            LineSpacing = LineSpacing,
            ParagraphSpacing = ParagraphSpacing,
            HotkeyPrefix = HotkeyPrefix,
        };
    }
}
