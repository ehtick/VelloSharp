use std::collections::HashMap;
use std::sync::RwLock;

use once_cell::sync::Lazy;
use vello::kurbo::{Affine, Rect, RoundedRect};
use vello::peniko::{Brush, Fill};
use vello::Scene;

use crate::color::VelloTdgColor;
use crate::error::{clear_last_error, set_last_error};
use crate::types::ColumnStrip;

pub type ShaderHandle = u32;
pub type MaterialHandle = u32;
pub type RenderHookHandle = u32;

static SHADER_REGISTRY: Lazy<RwLock<HashMap<ShaderHandle, ShaderEntry>>> =
    Lazy::new(|| RwLock::new(HashMap::new()));
static MATERIAL_REGISTRY: Lazy<RwLock<HashMap<MaterialHandle, MaterialEntry>>> =
    Lazy::new(|| RwLock::new(HashMap::new()));
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

#[derive(Clone, Copy, Debug)]
enum ShaderEntry {
    Solid(VelloTdgColor),
}

impl ShaderEntry {
    fn to_brush(self, opacity: f32) -> Brush {
        match self {
            ShaderEntry::Solid(color) => {
                let alpha = (color.a * opacity).clamp(0.0, 1.0);
                let tinted = VelloTdgColor {
                    r: color.r,
                    g: color.g,
                    b: color.b,
                    a: alpha,
                };
                Brush::Solid(tinted.to_color())
            }
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgMaterialDescriptor {
    pub shader: ShaderHandle,
    pub opacity: f32,
}

#[derive(Clone, Copy, Debug)]
struct MaterialEntry {
    shader: ShaderHandle,
    opacity: f32,
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
        render_fill(scene, column, height, self.material, self.inset, self.radius)
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

    let brush = resolve_material_brush(material)?;

    let inset = inset.clamp(0.0, column.width * 0.5).clamp(0.0, height * 0.5);
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

fn resolve_material_brush(handle: MaterialHandle) -> Option<Brush> {
    let materials = MATERIAL_REGISTRY.read().ok()?;
    let entry = materials.get(&handle).copied()?;
    drop(materials);

    let shaders = SHADER_REGISTRY.read().ok()?;
    let shader = shaders.get(&entry.shader).copied()?;
    drop(shaders);

    Some(shader.to_brush(entry.opacity.clamp(0.0, 1.0)))
}

fn register_shader_internal(
    handle: ShaderHandle,
    descriptor: &VelloTdgShaderDescriptor,
) -> Result<(), &'static str> {
    if handle == 0 {
        return Err("shader handle must be non-zero");
    }

    let entry = match descriptor.kind {
        VelloTdgShaderKind::Solid => ShaderEntry::Solid(descriptor.solid),
    };

    let mut registry = SHADER_REGISTRY
        .write()
        .map_err(|_| "shader registry poisoned")?;
    registry.insert(handle, entry);
    Ok(())
}

fn register_material_internal(
    handle: MaterialHandle,
    descriptor: &VelloTdgMaterialDescriptor,
) -> Result<(), &'static str> {
    if handle == 0 {
        return Err("material handle must be non-zero");
    }

    {
        let shaders = SHADER_REGISTRY
            .read()
            .map_err(|_| "shader registry poisoned")?;
        if !shaders.contains_key(&descriptor.shader) {
            return Err("material references unknown shader");
        }
    }

    let entry = MaterialEntry {
        shader: descriptor.shader,
        opacity: descriptor.opacity.clamp(0.0, 1.0),
    };

    let mut registry = MATERIAL_REGISTRY
        .write()
        .map_err(|_| "material registry poisoned")?;
    registry.insert(handle, entry);
    Ok(())
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

    {
        let materials = MATERIAL_REGISTRY
            .read()
            .map_err(|_| "material registry poisoned")?;
        if !materials.contains_key(&descriptor.material) {
            return Err("render hook references unknown material");
        }
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
    if let Ok(mut registry) = SHADER_REGISTRY.write() {
        registry.remove(&handle);
    }
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
    if let Ok(mut registry) = MATERIAL_REGISTRY.write() {
        registry.remove(&handle);
    }
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
    let materials = MATERIAL_REGISTRY.read().ok()?;
    let entry = materials.get(&material).copied()?;
    drop(materials);

    let shaders = SHADER_REGISTRY.read().ok()?;
    let shader = shaders.get(&entry.shader).copied()?;

    match shader {
        ShaderEntry::Solid(color) => {
            let alpha = (color.a * entry.opacity).clamp(0.0, 1.0);
            Some(VelloTdgColor {
                r: color.r,
                g: color.g,
                b: color.b,
                a: alpha,
            })
        }
    }
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
