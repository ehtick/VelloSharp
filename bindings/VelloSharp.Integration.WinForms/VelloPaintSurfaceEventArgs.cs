using System;
using System.Runtime.CompilerServices;
using VelloSharp.WinForms;

namespace VelloSharp.WinForms.Integration;

public static class VelloPaintSurfaceEventArgsExtensions
{
    private sealed class GraphicsHolder
    {
        public GraphicsHolder(VelloPaintSurfaceEventArgs args)
        {
            Graphics = new VelloGraphics(args.Session);
        }

        public VelloGraphics Graphics { get; }
    }

    private static readonly ConditionalWeakTable<VelloPaintSurfaceEventArgs, GraphicsHolder> s_graphicsCache = new();

    public static VelloGraphics GetGraphics(this VelloPaintSurfaceEventArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        return s_graphicsCache.GetValue(args, static a => new GraphicsHolder(a)).Graphics;
    }
}
