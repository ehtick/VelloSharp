using System;
using System.ComponentModel;
using System.Globalization;

namespace VelloSharp.Avalonia.Svg;

/// <summary>
/// Converts between string paths and <see cref="SvgSource"/> instances.
/// </summary>
public sealed class SvgSourceTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        if (sourceType == typeof(string))
        {
            return true;
        }

        return base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text && !string.IsNullOrWhiteSpace(text))
        {
            Uri? baseUri = null;
            if (context is IServiceProvider services)
            {
                baseUri = services.GetContextBaseUri();
            }

            return SvgSource.Load(text, baseUri);
        }

        return base.ConvertFrom(context, culture, value);
    }
}
