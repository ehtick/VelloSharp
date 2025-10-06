using System;
using System.Drawing;
using System.Windows.Forms;
using VelloSharp.WinForms;
using VelloSharp.WinForms.Integration;
using Xunit;

namespace VelloSharp.WinForms.Tests;

public class VelloRenderControlTests
{
    [Fact]
    public void PreferredBackendDefaultsToGpu()
    {
        using var control = new VelloRenderControl();
        Assert.Equal(VelloRenderBackend.Gpu, control.PreferredBackend);
    }

    [Fact]
    public void PreferredBackendSwitchesToCpu()
    {
        using var control = new VelloRenderControl();
        control.PreferredBackend = VelloRenderBackend.Cpu;
        Assert.Equal(VelloRenderBackend.Cpu, control.PreferredBackend);
        control.PreferredBackend = VelloRenderBackend.Gpu;
        Assert.Equal(VelloRenderBackend.Gpu, control.PreferredBackend);
    }

    [Fact]
    public void PreferredBackendRejectsInvalidValue()
    {
        using var control = new VelloRenderControl();
        Assert.Throws<ArgumentOutOfRangeException>(() => control.PreferredBackend = (VelloRenderBackend)(-1));
    }

    [Fact]
    public void DeviceOptionsSetterValidatesInput()
    {
        using var control = new VelloRenderControl();

        Assert.Throws<ArgumentNullException>(() => control.DeviceOptions = null!);

        var newOptions = VelloGraphicsDeviceOptions.Default with { PreferDiscreteAdapter = true };
        control.DeviceOptions = newOptions;
        Assert.Equal(newOptions, control.DeviceOptions);
    }

    [StaFact]
    public void PaintSurfaceRaisedInOnDemandMode()
    {
        using var form = new Form { Size = new Size(200, 200) };
        using var control = new VelloRenderControl { Size = new Size(64, 64) };
        form.Controls.Add(control);

        form.CreateControl();
        control.CreateControl();

        var callCount = 0;
        var graphicsCached = false;
        TimeSpan timestamp = TimeSpan.Zero;
        TimeSpan delta = TimeSpan.Zero;
        long frameId = 0;
        var animationFlag = true;

        control.PaintSurface += (_, args) =>
        {
            callCount++;
            var g1 = args.GetGraphics();
            var g2 = args.GetGraphics();
            graphicsCached = ReferenceEquals(g1, g2);
            g1.Clear(Color.CornflowerBlue);
            timestamp = args.Timestamp;
            delta = args.Delta;
            frameId = args.FrameId;
            animationFlag = args.IsAnimationFrame;
        };

        try
        {
            form.Show();
            control.Invalidate();
            control.Refresh();
            Application.DoEvents();
        }
        catch (DllNotFoundException)
        {
            return;
        }
        finally
        {
            form.Hide();
        }

        Assert.True(callCount > 0);
        Assert.True(graphicsCached);
        Assert.True(frameId >= 1);
        Assert.True(timestamp >= TimeSpan.Zero);
        Assert.True(delta >= TimeSpan.Zero);
        Assert.False(animationFlag);
    }

    [StaFact]
    public void PaintSurfaceIndicatesContinuousMode()
    {
        using var form = new Form { Size = new Size(200, 200) };
        using var control = new VelloRenderControl { Size = new Size(64, 64) };
        form.Controls.Add(control);

        form.CreateControl();
        control.CreateControl();

        var animationFlag = false;
        control.RenderMode = VelloRenderMode.Continuous;

        control.PaintSurface += (_, args) =>
        {
            animationFlag = args.IsAnimationFrame;
            args.GetGraphics().Clear(Color.DarkRed);
            control.RenderMode = VelloRenderMode.OnDemand;
        };

        try
        {
            form.Show();
            control.Invalidate();
            control.Refresh();
            Application.DoEvents();
        }
        catch (DllNotFoundException)
        {
            return;
        }
        finally
        {
            control.RenderMode = VelloRenderMode.OnDemand;
            form.Hide();
        }

        Assert.True(animationFlag);
    }
}
