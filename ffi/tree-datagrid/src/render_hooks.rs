use std::collections::HashMap;
use std::sync::RwLock;

use once_cell::sync::Lazy;
use vello::Scene;
use vello::kurbo::{Affine, Rect, RoundedRect};
use vello::peniko::{Brush, Fill};
use vello_composition::{
    CompositionColor as SharedColor, CompositionMaterialDescriptor as SharedMaterialDescriptor,
    CompositionShaderDescriptor as SharedShaderDescriptor,
    CompositionShaderKind as SharedShaderKind, register_material as composition_register_material,
    register_shader as composition_register_shader,
    resolve_material_color as composition_resolve_material_color, resolve_material_peniko_color,
    unregister_material as composition_unregister_material,
    unregister_shader as composition_unregister_shader,
};

use crate::color::VelloTdgColor;
use crate::error::{clear_last_error, set_last_error};
use crate::types::ColumnStrip;

pub type ShaderHandle = u32;
pub type MaterialHandle = u32;
pub type RenderHookHandle = u32;

static RENDER_HOOK_REGISTRY: Lazy<RwLock<HashMap<RenderHookHandle, RenderHookEntry>>> =
    Lazy::new(|| RwLock::new(HashMap::new()));

#[repr(u32)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum VelloTdgShaderKind {
    Solid = 0,
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgShaderDescriptor {
    pub kind: VelloTdgShaderKind,
    pub solid: VelloTdgColor,
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgMaterialDescriptor {
    pub shader: ShaderHandle,
    pub opacity: f32,
}

#[repr(u32)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum VelloTdgRenderHookKind {
    FillRounded = 0,
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgRenderHookDescriptor {
    pub kind: VelloTdgRenderHookKind,
    pub material: MaterialHandle,
    pub inset: f64,
    pub radius: f64,
}

#[derive(Clone, Copy, Debug)]
struct RenderHookEntry {
    material: MaterialHandle,
    inset: f64,
    radius: f64,
}

impl RenderHookEntry {
    fn render(self, scene: &mut Scene, column: &ColumnStrip, height: f64) -> bool {
        render_fill(
            scene,
            column,
            height,
            self.material,
            self.inset,
            self.radius,
        )
    }
}

fn render_fill(
    scene: &mut Scene,
    column: &ColumnStrip,
    height: f64,
    material: MaterialHandle,
    inset: f64,
    radius: f64,
) -> bool {
    let Some((brush, rect, clamped_radius)) =
        resolve_brush_and_rect(column, height, material, inset, radius)
    else {
        return false;
    };

    let rounded = RoundedRect::from_rect(rect, clamped_radius);
    scene.fill(Fill::NonZero, Affine::IDENTITY, &brush, None, &rounded);
    true
}

fn resolve_brush_and_rect(
    column: &ColumnStrip,
    height: f64,
    material: MaterialHandle,
    inset: f64,
    radius: f64,
) -> Option<(Brush, Rect, f64)> {
    if column.width <= 0.0 || height <= 0.0 {
        return None;
    }

    let brush = resolve_material_peniko_color(material).map(Brush::Solid)?;

    let inset = inset
        .clamp(0.0, column.width * 0.5)
        .clamp(0.0, height * 0.5);
    let rect = Rect::new(
        column.offset + inset,
        inset,
        column.offset + column.width - inset,
        height - inset,
    );

    if rect.width() <= 0.0 || rect.height() <= 0.0 {
        return None;
    }

    let max_radius = rect.width().min(rect.height()) * 0.5;
    let clamped_radius = radius.clamp(0.0, max_radius);

    Some((brush, rect, clamped_radius))
}

fn register_shader_internal(
    handle: ShaderHandle,
    descriptor: &VelloTdgShaderDescriptor,
) -> Result<(), &'static str> {
    if handle == 0 {
        return Err("shader handle must be non-zero");
    }

    let kind = match descriptor.kind {
        VelloTdgShaderKind::Solid => SharedShaderKind::Solid,
    };

    let shared_descriptor = SharedShaderDescriptor {
        kind,
        solid: SharedColor {
            r: descriptor.solid.r,
            g: descriptor.solid.g,
            b: descriptor.solid.b,
            a: descriptor.solid.a,
        },
    };

    composition_register_shader(handle, &shared_descriptor)
}

fn register_material_internal(
    handle: MaterialHandle,
    descriptor: &VelloTdgMaterialDescriptor,
) -> Result<(), &'static str> {
    if handle == 0 {
        return Err("material handle must be non-zero");
    }

    let shared_descriptor = SharedMaterialDescriptor {
        shader: descriptor.shader,
        opacity: descriptor.opacity,
    };

    composition_register_material(handle, &shared_descriptor)
}

fn register_render_hook_internal(
    handle: RenderHookHandle,
    descriptor: &VelloTdgRenderHookDescriptor,
) -> Result<(), &'static str> {
    if handle == 0 {
        return Err("render hook handle must be non-zero");
    }

    if descriptor.kind != VelloTdgRenderHookKind::FillRounded {
        return Err("unsupported render hook kind");
    }

    if composition_resolve_material_color(descriptor.material).is_none() {
        return Err("render hook references unknown material");
    }

    let entry = RenderHookEntry {
        material: descriptor.material,
        inset: descriptor.inset,
        radius: descriptor.radius,
    };

    let mut registry = RENDER_HOOK_REGISTRY
        .write()
        .map_err(|_| "render hook registry poisoned")?;
    registry.insert(handle, entry);
    Ok(())
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_shader_register(
    handle: ShaderHandle,
    descriptor: *const VelloTdgShaderDescriptor,
) -> bool {
    clear_last_error();
    if descriptor.is_null() {
        set_last_error("null shader descriptor");
        return false;
    }

    let descriptor = unsafe { &*descriptor };
    match register_shader_internal(handle, descriptor) {
        Ok(()) => true,
        Err(message) => {
            set_last_error(message);
            false
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_shader_unregister(handle: ShaderHandle) {
    clear_last_error();
    if handle == 0 {
        return;
    }
    composition_unregister_shader(handle);
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_material_register(
    handle: MaterialHandle,
    descriptor: *const VelloTdgMaterialDescriptor,
) -> bool {
    clear_last_error();
    if descriptor.is_null() {
        set_last_error("null material descriptor");
        return false;
    }

    let descriptor = unsafe { &*descriptor };
    match register_material_internal(handle, descriptor) {
        Ok(()) => true,
        Err(message) => {
            set_last_error(message);
            false
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_material_unregister(handle: MaterialHandle) {
    clear_last_error();
    if handle == 0 {
        return;
    }
    composition_unregister_material(handle);
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_render_hook_register(
    handle: RenderHookHandle,
    descriptor: *const VelloTdgRenderHookDescriptor,
) -> bool {
    clear_last_error();
    if descriptor.is_null() {
        set_last_error("null render hook descriptor");
        return false;
    }

    let descriptor = unsafe { &*descriptor };
    match register_render_hook_internal(handle, descriptor) {
        Ok(()) => true,
        Err(message) => {
            set_last_error(message);
            false
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_render_hook_unregister(handle: RenderHookHandle) {
    clear_last_error();
    if let Ok(mut registry) = RENDER_HOOK_REGISTRY.write() {
        registry.remove(&handle);
    }
}

pub fn fill_with_material(
    handle: MaterialHandle,
    scene: &mut Scene,
    column: &ColumnStrip,
    height: f64,
) -> bool {
    if let Some((brush, rect, _)) = resolve_brush_and_rect(column, height, handle, 0.0, 0.0) {
        scene.fill(Fill::NonZero, Affine::IDENTITY, &brush, None, &rect);
        true
    } else {
        false
    }
}

pub fn render_column_hook(
    handle: RenderHookHandle,
    scene: &mut Scene,
    column: &ColumnStrip,
    height: f64,
) -> bool {
    let registry = match RENDER_HOOK_REGISTRY.read() {
        Ok(registry) => registry,
        Err(_) => return false,
    };

    let Some(entry) = registry.get(&handle).copied() else {
        return false;
    };

    entry.render(scene, column, height)
}

pub fn resolve_column_color(material: MaterialHandle) -> Option<VelloTdgColor> {
    let color = composition_resolve_material_color(material)?;
    Some(VelloTdgColor {
        r: color.r,
        g: color.g,
        b: color.b,
        a: color.a,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn register_shader_material_and_hook() {
        clear_last_error();
        let shader = VelloTdgShaderDescriptor {
            kind: VelloTdgShaderKind::Solid,
            solid: VelloTdgColor {
                r: 0.2,
                g: 0.4,
                b: 0.6,
                a: 1.0,
            },
        };

        register_shader_internal(1, &shader).expect("shader register");

        let material = VelloTdgMaterialDescriptor {
            shader: 1,
            opacity: 0.5,
        };

        register_material_internal(2, &material).expect("material register");

        let hook = VelloTdgRenderHookDescriptor {
            kind: VelloTdgRenderHookKind::FillRounded,
            material: 2,
            inset: 1.5,
            radius: 4.0,
        };

        register_render_hook_internal(3, &hook).expect("hook register");
    }
}
