namespace VelloSharp.Composition;

public readonly record struct PlotArea(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}
