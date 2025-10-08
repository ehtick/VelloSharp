using System;
using VelloSharp.Composition;
using Xunit;

namespace VelloSharp.Charting.Tests.Composition;

public sealed class TimelineSystemInteropTests
{
    [Fact]
    public void TimelineTicksWithoutExceptions()
    {
        using var cache = new SceneCache();
        uint nodeId = cache.CreateNode();

        using var system = new TimelineSystem();
        uint groupId = system.CreateGroup(new TimelineGroupConfig());
        system.PlayGroup(groupId);

        var easingDescriptor = new TimelineEasingTrackDescriptor(
            nodeId,
            channelId: 0,
            startValue: 0f,
            endValue: 5f,
            duration: 0.4f,
            easing: TimelineEasing.EaseInOutQuad,
            repeat: TimelineRepeat.Once,
            dirtyBinding: TimelineDirtyBinding.None);

        var springDescriptor = new TimelineSpringTrackDescriptor(
            nodeId,
            channelId: 1,
            stiffness: 90f,
            damping: 14f,
            mass: 1f,
            startValue: 0f,
            initialVelocity: 0f,
            targetValue: 1f,
            restVelocity: 0.0005f,
            restOffset: 0.0005f,
            dirtyBinding: TimelineDirtyBinding.None);

        uint easingTrack = system.AddEasingTrack(groupId, easingDescriptor);
        uint springTrack = system.AddSpringTrack(groupId, springDescriptor);
        Assert.NotEqual(uint.MaxValue, easingTrack);
        Assert.NotEqual(uint.MaxValue, springTrack);

        Span<TimelineSample> samples = stackalloc TimelineSample[8];

        for (int i = 0; i < 4; i++)
        {
            _ = system.Tick(TimeSpan.FromMilliseconds(16.0), cache, samples);
        }

        system.SetSpringTarget(springTrack, 0.25f);
        _ = system.Tick(TimeSpan.FromMilliseconds(16.0), cache, samples);

        system.ResetTrack(easingTrack);
        system.RemoveTrack(easingTrack);
        system.RemoveTrack(springTrack);
    }
}
