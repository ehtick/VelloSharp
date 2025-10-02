using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using VelloSharp;

namespace VelloSharp.Text;

public enum VelloFontStyle
{
    Normal = 0,
    Italic = 1,
    Oblique = 2,
}

public readonly record struct ParleyFontQuery(
    string? FamilyName,
    float Weight,
    float Stretch,
    VelloFontStyle Style,
    CultureInfo? Culture);

public readonly record struct ParleyFontInfo(
    string FamilyName,
    byte[] FontData,
    uint FaceIndex,
    float Weight,
    float Stretch,
    VelloFontStyle Style,
    bool IsMonospace);

public readonly record struct ParleyVariationAxis(
    string Tag,
    float MinValue,
    float DefaultValue,
    float MaxValue);

public sealed class ParleyFontService
{
    private readonly object _sync = new();
    private string? _defaultFamily;
    private string[]? _installedFamilies;

    public static ParleyFontService Instance { get; } = new();

    private ParleyFontService()
    {
    }

    public string GetDefaultFamilyName()
    {
        lock (_sync)
        {
            if (!string.IsNullOrEmpty(_defaultFamily))
            {
                return _defaultFamily!;
            }

            var ptr = NativeMethods.vello_parley_get_default_family();
            try
            {
                _defaultFamily = ptr != IntPtr.Zero
                    ? Marshal.PtrToStringUTF8(ptr)
                    : "Roboto";
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    NativeMethods.vello_string_destroy(ptr);
                }
            }

            return _defaultFamily!;
        }
    }

    public IReadOnlyList<string> GetInstalledFamilyNames(bool refresh = false)
    {
        lock (_sync)
        {
            if (!refresh && _installedFamilies is { Length: > 0 })
            {
                return _installedFamilies;
            }

            var status = NativeMethods.vello_parley_get_family_names(out var handle, out var array);
            try
            {
                if (status != VelloStatus.Success || array.Count == 0 || array.Items == IntPtr.Zero)
                {
                    var fallback = new[] { GetDefaultFamilyName() };
                    _installedFamilies = fallback;
                    return fallback;
                }

                var count = checked((int)array.Count);
                var names = new string[count];
                unsafe
                {
                    var items = (IntPtr*)array.Items;
                    for (var i = 0; i < count; i++)
                    {
                        names[i] = Marshal.PtrToStringUTF8(items[i]) ?? string.Empty;
                    }
                }

                Array.Sort(names, StringComparer.OrdinalIgnoreCase);
                _installedFamilies = names;
                return names;
            }
            finally
            {
                if (handle != IntPtr.Zero)
                {
                    NativeMethods.vello_parley_string_array_destroy(handle);
                }
            }
        }
    }

    public bool TryMatchCharacter(uint codepoint, ParleyFontQuery query, out ParleyFontInfo info)
    {
        info = default;
        var locale = query.Culture?.Name;
        var status = NativeMethods.vello_parley_match_character(
            codepoint,
            query.Weight,
            query.Stretch,
            (int)query.Style,
            query.FamilyName,
            locale,
            out var handle,
            out var nativeInfo);

        if (status != VelloStatus.Success)
        {
            if (handle != IntPtr.Zero)
            {
                NativeMethods.vello_parley_font_handle_destroy(handle);
            }
            return false;
        }

        try
        {
            var managed = CopyFontData(nativeInfo);
            if (managed.FontData.Length == 0)
            {
                return false;
            }

            info = managed;
            return true;
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                NativeMethods.vello_parley_font_handle_destroy(handle);
            }
        }
    }

    public bool TryLoadTypeface(ParleyFontQuery query, out ParleyFontInfo info)
    {
        info = default;
        var status = NativeMethods.vello_parley_load_typeface(
            query.FamilyName ?? GetDefaultFamilyName(),
            query.Weight,
            query.Stretch,
            (int)query.Style,
            out var handle,
            out var nativeInfo);

        if (status != VelloStatus.Success)
        {
            if (handle != IntPtr.Zero)
            {
                NativeMethods.vello_parley_font_handle_destroy(handle);
            }
            return false;
        }

        try
        {
            var managed = CopyFontData(nativeInfo);
            if (managed.FontData.Length == 0)
            {
                return false;
            }

            info = managed;
            return true;
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                NativeMethods.vello_parley_font_handle_destroy(handle);
            }
        }
    }

    public IReadOnlyList<ParleyVariationAxis> GetVariationAxes(ParleyFontInfo fontInfo)
    {
        if (fontInfo.FontData.Length == 0)
        {
            return Array.Empty<ParleyVariationAxis>();
        }

        try
        {
            using var font = Font.Load(fontInfo.FontData, fontInfo.FaceIndex);
            var status = NativeMethods.vello_font_get_variation_axes(font.Handle, out var handle, out var array);

            if (status != VelloStatus.Success || array.Count == 0 || array.Axes == IntPtr.Zero)
            {
                if (handle != IntPtr.Zero)
                {
                    NativeMethods.vello_font_variation_axes_destroy(handle);
                }

                return Array.Empty<ParleyVariationAxis>();
            }

            try
            {
                unsafe
                {
                    var span = new ReadOnlySpan<VelloVariationAxisNative>((void*)array.Axes, checked((int)array.Count));
                    if (span.Length == 0)
                    {
                        return Array.Empty<ParleyVariationAxis>();
                    }

                    var result = new ParleyVariationAxis[span.Length];
                    for (var i = 0; i < span.Length; i++)
                    {
                        var axis = span[i];
                        result[i] = new ParleyVariationAxis(
                            DecodeTag(axis.Tag),
                            axis.MinValue,
                            axis.DefaultValue,
                            axis.MaxValue);
                    }

                    return result;
                }
            }
            finally
            {
                if (handle != IntPtr.Zero)
                {
                    NativeMethods.vello_font_variation_axes_destroy(handle);
                }
            }
        }
        catch
        {
            return Array.Empty<ParleyVariationAxis>();
        }
    }

    private static ParleyFontInfo CopyFontData(VelloParleyFontInfoNative native)
    {
        var resolvedFamily = Marshal.PtrToStringUTF8(native.FamilyName) ?? string.Empty;
        var data = CopyBytes(native.Data, native.Length);
        return new ParleyFontInfo(
            resolvedFamily,
            data,
            native.Index,
            native.Weight,
            native.Stretch,
            (VelloFontStyle)native.Style,
            native.IsMonospace);
    }

    private static byte[] CopyBytes(IntPtr source, nuint length)
    {
        if (source == IntPtr.Zero || length == 0)
        {
            return Array.Empty<byte>();
        }

        var managed = new byte[checked((int)length)];
        Marshal.Copy(source, managed, 0, managed.Length);
        return managed;
    }

    private static string DecodeTag(uint tag)
    {
        Span<char> buffer = stackalloc char[4];
        buffer[0] = (char)((tag >> 24) & 0xFF);
        buffer[1] = (char)((tag >> 16) & 0xFF);
        buffer[2] = (char)((tag >> 8) & 0xFF);
        buffer[3] = (char)(tag & 0xFF);
        return new string(buffer);
    }
}
