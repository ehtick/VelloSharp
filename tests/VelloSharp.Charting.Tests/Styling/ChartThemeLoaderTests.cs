using System;
using System.Collections.Generic;
using VelloSharp.Charting.Styling;
using VelloSharp.Charting.Styling.Configuration;
using Xunit;
using ChartRgbaColor = VelloSharp.Charting.Styling.RgbaColor;

namespace VelloSharp.Charting.Tests.Styling;

public sealed class ChartThemeLoaderTests
{
    [Fact]
    public void LoadFromJson_ParsesCompleteThemeDefinition()
    {
        var json = """
        {
          "name": "Neon",
          "palette": {
            "background": "#101020FF",
            "foreground": "#F4F6FAFF",
            "axisLine": "#40455AFF",
            "axisTick": "#50566AFF",
            "gridLine": "#1E2233FF",
            "legendBackground": "#161A2AFF",
            "legendBorder": "#262C3CFF",
            "series": ["#3AB8FFFF", "#F45E8CFF", "#81FFF9FF"]
          },
          "axis": {
            "lineColor": "#5A60A5FF",
            "tickColor": "#5A60A5FF",
            "tickLength": 12,
            "labelMargin": 8,
            "labelTypography": {
              "fontFamily": "Inter",
              "fontSize": 13,
              "fontWeight": "SemiBold"
            }
          },
          "legend": {
            "background": "#171C2AFF",
            "border": "#2F3649FF",
            "borderThickness": 2,
            "markerSize": 14,
            "itemSpacing": 9,
            "padding": 16,
            "labelSpacing": 7,
            "labelTypography": {
              "fontFamily": "Inter",
              "fontSize": 12,
              "fontWeight": "Medium"
            }
          },
          "typography": {
            "axisLabel": {
              "fontFamily": "Inter",
              "fontSize": 11,
              "fontWeight": "Regular"
            },
            "body": {
              "fontFamily": "Source Sans",
              "fontSize": 13,
              "lineHeight": 17
            }
          }
        }
        """;

        var theme = ChartThemeLoader.LoadFromJson(json);

        Assert.Equal("Neon", theme.Name);

        Assert.Equal(ChartRgbaColor.FromHex(0x101020FF), theme.Palette.Background);
        Assert.Equal(ChartRgbaColor.FromHex(0xF4F6FAFF), theme.Palette.Foreground);
        Assert.Equal(3, theme.Palette.Series.Count);
        Assert.Equal(ChartRgbaColor.FromHex(0x3AB8FFFF), theme.Palette.Series[0]);

        Assert.Equal(ChartRgbaColor.FromHex(0x5A60A5FF), theme.Axis.LineColor);
        Assert.Equal(12d, theme.Axis.TickLength);
        Assert.Equal(8d, theme.Axis.LabelMargin);
        Assert.Equal("Inter", theme.Axis.LabelTypography.FontFamily);
        Assert.Equal(13d, theme.Axis.LabelTypography.FontSize);
        Assert.Equal("SemiBold", theme.Axis.LabelTypography.FontWeight);

        Assert.Equal(ChartRgbaColor.FromHex(0x171C2AFF), theme.Legend.Background);
        Assert.Equal(ChartRgbaColor.FromHex(0x2F3649FF), theme.Legend.Border);
        Assert.Equal(2d, theme.Legend.BorderThickness);
        Assert.Equal(14d, theme.Legend.MarkerSize);
        Assert.Equal(9d, theme.Legend.ItemSpacing);
        Assert.Equal(16d, theme.Legend.Padding);
        Assert.Equal(7d, theme.Legend.LabelSpacing);
        Assert.Equal("Inter", theme.Legend.LabelTypography.FontFamily);
        Assert.Equal(12d, theme.Legend.LabelTypography.FontSize);
        Assert.Equal("Medium", theme.Legend.LabelTypography.FontWeight);

        Assert.True(theme.TypographyVariants.ContainsKey("axisLabel"));
        Assert.Equal("Inter", theme.TypographyVariants["AxisLabel"].FontFamily);
        Assert.Equal(11d, theme.TypographyVariants["axislabel"].FontSize);
        Assert.True(theme.TypographyVariants.ContainsKey("Body"));
        Assert.Equal("Source Sans", theme.TypographyVariants["body"].FontFamily);
        Assert.Equal(13d, theme.TypographyVariants["body"].FontSize);
        Assert.Equal(17d, theme.TypographyVariants["body"].LineHeight);
    }

    [Fact]
    public void LoadManyFromJson_ParsesArraysAndDefaultsMissingNames()
    {
        var json = """
        [
          {
            "name": "Day",
            "palette": {
              "background": "#F5F7FBFF",
              "foreground": "#1F2430FF",
              "axisLine": "#5A6478FF",
              "axisTick": "#5A6478FF",
              "gridLine": "#D4DAE6FF",
              "legendBackground": "#FFFFFFFF",
              "legendBorder": "#CED7EAFF",
              "series": ["#3AB8FFFF"]
            }
          },
          {
            "palette": {
              "background": "#10151FFF",
              "foreground": "#ECEFF4FF",
              "axisLine": "#8FA2C3FF",
              "axisTick": "#8FA2C3FF",
              "gridLine": "#1E2838FF",
              "legendBackground": "#1B2333FF",
              "legendBorder": "#2F3A4FFF",
              "series": ["#F45E8CFF", "#81FFF9FF"]
            }
          }
        ]
        """;

        var themes = ChartThemeLoader.LoadManyFromJson(json);

        Assert.Collection(
            themes,
            day =>
            {
                Assert.Equal("Day", day.Name);
                Assert.Single(day.Palette.Series);
            },
            night =>
            {
                Assert.Equal("Custom", night.Name);
                Assert.Equal(2, night.Palette.Series.Count);
                Assert.Equal(ChartRgbaColor.FromHex(0xF45E8CFF), night.Palette.Series[0]);
            });
    }

    [Fact]
    public void LoadFromJson_ThrowsWhenPaletteMissing()
    {
        var json = """
        {
          "name": "Invalid"
        }
        """;

        var ex = Assert.Throws<InvalidOperationException>(() => ChartThemeLoader.LoadFromJson(json));
        Assert.Contains("palette", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
