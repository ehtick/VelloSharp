using System;
using VelloSharp.ChartEngine;
using VelloSharp.Composition;
using Xunit;

namespace VelloSharp.Charting.Tests.Composition;

public sealed class ChartCompositionBuilderTests
{
    [Fact]
    public void CompositionUsesDefaultAnimationProfileWhenNotConfigured()
    {
        var composition = ChartComposition.Create(builder =>
        {
            builder.Pane("primary").WithSeries(1).Done();
        });

        Assert.False(composition.HasCustomAnimations);
        Assert.Equal(ChartAnimationProfile.Default, composition.Animations);
    }

    [Fact]
    public void CompositionRecordsCustomAnimationProfile()
    {
        var customProfile = ChartAnimationProfile.Default with
        {
            ReducedMotionEnabled = true,
            SeriesStroke = new ChartAnimationTimeline(TimeSpan.FromMilliseconds(120), TimelineEasing.EaseOutQuad),
        };

        var composition = ChartComposition.Create(builder =>
        {
            builder.Pane("primary").WithSeries(1).Done();
            builder.UseAnimations(customProfile);
        });

        Assert.True(composition.HasCustomAnimations);
        Assert.Equal(customProfile, composition.Animations);
    }
}
