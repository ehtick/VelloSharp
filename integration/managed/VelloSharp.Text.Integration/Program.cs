using VelloSharp.Text;

Console.WriteLine("Verifying VelloSharp.Text package usage…");

var feature = new VelloOpenTypeFeature("kern", 1);
var variation = new VelloVariationAxisValue("wght", 400f);
var options = VelloTextShaperOptions.CreateDefault(16f, isRightToLeft: false) with
{
    Features = new[] { feature },
    VariationAxes = new[] { variation },
};

Console.WriteLine($"Feature: {feature.Tag}={feature.Value}");
Console.WriteLine($"Variation: {variation.Tag}={variation.Value}");
Console.WriteLine($"Options font size: {options.FontSize}");
Console.WriteLine($"Assembly location: {typeof(VelloTextShaperOptions).Assembly.Location}");

Console.WriteLine("VelloSharp.Text integration test completed.");
