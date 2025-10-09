using System;
using VelloSharp.Composition;
using VelloSharp.Composition.Controls;
using Xunit;

namespace VelloSharp.Charting.Tests.Composition;

public sealed class TextControlTests
{
    private static readonly LayoutConstraints DefaultConstraints =
        new(
            new ScalarConstraint(0, 200, 200),
            new ScalarConstraint(0, 200, 200));

    [Fact]
    public void TextBlock_MeasureReflectsTextMetrics()
    {
        var block = new TextBlock
        {
            Text = "VelloSharp",
            FontSize = 14f,
        };

        block.Measure(DefaultConstraints);

        Assert.True(block.DesiredSize.Width > 0);
        Assert.True(block.DesiredSize.Height > 0);
    }

    [Fact]
    public void AccessText_NormalizesAccessKeyForMeasurement()
    {
        var reference = new TextBlock
        {
            Text = "File",
            FontSize = 13f,
        };
        reference.Measure(DefaultConstraints);
        var referenceSize = reference.DesiredSize;

        var access = new AccessText
        {
            Text = "_File",
            FontSize = 13f,
        };
        access.Measure(DefaultConstraints);

        Assert.Equal(referenceSize.Width, access.DesiredSize.Width, 3);
        Assert.Equal(referenceSize.Height, access.DesiredSize.Height, 3);
    }

    [Fact]
    public void TextBox_AddsPaddingAroundText()
    {
        var textBlock = new TextBlock
        {
            Text = "Value",
            FontSize = 14f,
        };
        textBlock.Measure(DefaultConstraints);

        var textBox = new TextBox
        {
            Text = "Value",
            FontSize = 14f,
        };
        textBox.Measure(DefaultConstraints);

        Assert.True(textBox.DesiredSize.Width > textBlock.DesiredSize.Width);
        Assert.True(textBox.DesiredSize.Height > textBlock.DesiredSize.Height);
    }
}
