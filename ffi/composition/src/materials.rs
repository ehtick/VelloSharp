use std::collections::HashMap;
use std::sync::RwLock;

use once_cell::sync::Lazy;
use vello::peniko::Color;

static SHADER_REGISTRY: Lazy<RwLock<HashMap<u32, ShaderEntry>>> =
    Lazy::new(|| RwLock::new(HashMap::new()));
static MATERIAL_REGISTRY: Lazy<RwLock<HashMap<u32, MaterialEntry>>> =
    Lazy::new(|| RwLock::new(HashMap::new()));

#[repr(C)]
#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct CompositionColor {
    pub r: f32,
    pub g: f32,
    pub b: f32,
    pub a: f32,
}

impl CompositionColor {
    #[inline]
    pub fn to_color(self) -> Color {
        let clamp = |value: f32| value.clamp(0.0, 1.0);
        Color::new([clamp(self.r), clamp(self.g), clamp(self.b), clamp(self.a)])
    }
}

#[repr(u32)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum CompositionShaderKind {
    Solid = 0,
}

impl Default for CompositionShaderKind {
    fn default() -> Self {
        Self::Solid
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct CompositionShaderDescriptor {
    pub kind: CompositionShaderKind,
    pub solid: CompositionColor,
}

#[derive(Clone, Copy, Debug)]
enum ShaderEntry {
    Solid(CompositionColor),
}

impl ShaderEntry {
    fn resolve_color(self, opacity: f32) -> CompositionColor {
        match self {
            ShaderEntry::Solid(color) => {
                let alpha = (color.a * opacity).clamp(0.0, 1.0);
                CompositionColor {
                    r: color.r,
                    g: color.g,
                    b: color.b,
                    a: alpha,
                }
            }
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct CompositionMaterialDescriptor {
    pub shader: u32,
    pub opacity: f32,
}

#[derive(Clone, Copy, Debug)]
struct MaterialEntry {
    shader: u32,
    opacity: f32,
}

pub fn register_shader(
    handle: u32,
    descriptor: &CompositionShaderDescriptor,
) -> Result<(), &'static str> {
    if handle == 0 {
        return Err("shader handle must be non-zero");
    }

    let entry = match descriptor.kind {
        CompositionShaderKind::Solid => ShaderEntry::Solid(descriptor.solid),
    };

    let mut registry = SHADER_REGISTRY
        .write()
        .map_err(|_| "shader registry lock poisoned")?;
    registry.insert(handle, entry);
    Ok(())
}

pub fn unregister_shader(handle: u32) {
    if handle == 0 {
        return;
    }

    if let Ok(mut registry) = SHADER_REGISTRY.write() {
        registry.remove(&handle);
    }

    if let Ok(mut materials) = MATERIAL_REGISTRY.write() {
        materials.retain(|_, material| material.shader != handle);
    }
}

pub fn register_material(
    handle: u32,
    descriptor: &CompositionMaterialDescriptor,
) -> Result<(), &'static str> {
    if handle == 0 {
        return Err("material handle must be non-zero");
    }

    if descriptor.shader == 0 {
        return Err("material shader handle must be non-zero");
    }

    {
        let registry = SHADER_REGISTRY
            .read()
            .map_err(|_| "shader registry lock poisoned")?;
        if !registry.contains_key(&descriptor.shader) {
            return Err("shader handle not registered");
        }
    }

    let opacity = descriptor.opacity.clamp(0.0, 1.0);
    let entry = MaterialEntry {
        shader: descriptor.shader,
        opacity,
    };

    let mut registry = MATERIAL_REGISTRY
        .write()
        .map_err(|_| "material registry lock poisoned")?;
    registry.insert(handle, entry);
    Ok(())
}

pub fn unregister_material(handle: u32) {
    if handle == 0 {
        return;
    }

    if let Ok(mut registry) = MATERIAL_REGISTRY.write() {
        registry.remove(&handle);
    }
}

pub fn resolve_material_color(handle: u32) -> Option<CompositionColor> {
    let materials = MATERIAL_REGISTRY.read().ok()?;
    let entry = materials.get(&handle).copied()?;
    drop(materials);

    let shaders = SHADER_REGISTRY.read().ok()?;
    let shader = shaders.get(&entry.shader).copied()?;
    Some(shader.resolve_color(entry.opacity))
}

pub fn resolve_material_peniko_color(handle: u32) -> Option<Color> {
    resolve_material_color(handle).map(|color| color.to_color())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn register_shader_and_material_resolves_color() {
        let shader = CompositionShaderDescriptor {
            kind: CompositionShaderKind::Solid,
            solid: CompositionColor {
                r: 0.25,
                g: 0.5,
                b: 0.75,
                a: 1.0,
            },
        };

        register_shader(42, &shader).expect("shader register");

        let material = CompositionMaterialDescriptor {
            shader: 42,
            opacity: 0.5,
        };

        register_material(7, &material).expect("material register");

        let color = resolve_material_color(7).expect("resolved color");
        assert!((color.r - 0.25).abs() < 1e-6);
        assert!((color.a - 0.5).abs() < 1e-6);

        unregister_material(7);
        unregister_shader(42);
    }
}
