using System;

namespace VelloSharp.TreeDataGrid.Rendering;

public enum TreeShaderKind
{
    Solid = 0,
}

public readonly record struct TreeShaderDescriptor(TreeShaderKind Kind, TreeColor SolidColor);

public static class TreeShaderRegistry
{
    public static void Register(uint shaderId, in TreeShaderDescriptor descriptor)
    {
        if (shaderId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shaderId), "Shader identifier must be non-zero.");
        }

        var native = new NativeMethods.VelloTdgShaderDescriptor
        {
            Kind = descriptor.Kind switch
            {
                TreeShaderKind.Solid => NativeMethods.VelloTdgShaderKind.Solid,
                _ => NativeMethods.VelloTdgShaderKind.Solid,
            },
            Solid = descriptor.SolidColor.ToNative(),
        };

        TreeInterop.ThrowIfFalse(
            NativeMethods.vello_tdg_shader_register(shaderId, native),
            "Failed to register shader.");
    }

    public static void Unregister(uint shaderId)
    {
        if (shaderId == 0)
        {
            return;
        }

        NativeMethods.vello_tdg_shader_unregister(shaderId);
    }
}

public readonly record struct TreeMaterialDescriptor(uint ShaderId, float Opacity = 1f);

public static class TreeMaterialRegistry
{
    public static void Register(uint materialId, in TreeMaterialDescriptor descriptor)
    {
        if (materialId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(materialId), "Material identifier must be non-zero.");
        }

        if (descriptor.ShaderId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor.ShaderId), "Shader identifier must be non-zero.");
        }

        var native = new NativeMethods.VelloTdgMaterialDescriptor
        {
            Shader = descriptor.ShaderId,
            Opacity = Math.Clamp(descriptor.Opacity, 0f, 1f),
        };

        TreeInterop.ThrowIfFalse(
            NativeMethods.vello_tdg_material_register(materialId, native),
            "Failed to register material.");
    }

    public static void Unregister(uint materialId)
    {
        if (materialId == 0)
        {
            return;
        }

        NativeMethods.vello_tdg_material_unregister(materialId);
    }
}

public enum TreeRenderHookKind
{
    FillRounded = 0,
}

public readonly record struct TreeRenderHookDescriptor(
    TreeRenderHookKind Kind,
    uint MaterialId,
    double Inset = 0d,
    double CornerRadius = 0d);

public static class TreeRenderHookRegistry
{
    public static void Register(uint hookId, in TreeRenderHookDescriptor descriptor)
    {
        if (hookId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hookId), "Render hook identifier must be non-zero.");
        }

        if (descriptor.MaterialId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor.MaterialId), "Material identifier must be non-zero.");
        }

        var native = new NativeMethods.VelloTdgRenderHookDescriptor
        {
            Kind = NativeMethods.VelloTdgRenderHookKind.FillRounded,
            Material = descriptor.MaterialId,
            Inset = descriptor.Inset,
            Radius = descriptor.CornerRadius,
        };

        TreeInterop.ThrowIfFalse(
            NativeMethods.vello_tdg_render_hook_register(hookId, native),
            "Failed to register render hook.");
    }

    public static void Unregister(uint hookId)
    {
        if (hookId == 0)
        {
            return;
        }

        NativeMethods.vello_tdg_render_hook_unregister(hookId);
    }
}
