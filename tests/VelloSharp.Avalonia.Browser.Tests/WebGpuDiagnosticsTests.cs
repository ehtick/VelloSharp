using System;
using VelloSharp.Avalonia.Browser;
using Xunit;

namespace VelloSharp.Avalonia.Browser.Tests;

public sealed class WebGpuDiagnosticsTests
{
    [Fact]
    public void ReportAvailability_notifies_subscribers_and_updates_state()
    {
        VelloBrowserDiagnostics.ReportAvailability(false, null);

        bool? observedState = null;
        string? observedMessage = null;

        void Handler(object? sender, WebGpuAvailabilityChangedEventArgs args)
        {
            observedState = args.IsAvailable;
            observedMessage = args.Explanation;
        }

        VelloBrowserDiagnostics.WebGpuAvailabilityChanged += Handler;
        try
        {
            VelloBrowserDiagnostics.ReportAvailability(false, "initial failure");
            Assert.False(VelloBrowserDiagnostics.IsWebGpuAvailable);
            Assert.Equal("initial failure", VelloBrowserDiagnostics.LastFailureExplanation);
            Assert.False(observedState);
            Assert.Equal("initial failure", observedMessage);

            observedState = null;
            observedMessage = null;

            VelloBrowserDiagnostics.ReportAvailability(true, null);
            Assert.True(VelloBrowserDiagnostics.IsWebGpuAvailable);
            Assert.Null(VelloBrowserDiagnostics.LastFailureExplanation);
            Assert.True(observedState);
            Assert.Null(observedMessage);
        }
        finally
        {
            VelloBrowserDiagnostics.WebGpuAvailabilityChanged -= Handler;
        }
    }
}
