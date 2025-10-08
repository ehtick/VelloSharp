use std::ffi::{CStr, c_char};
use std::ptr;
use std::slice;

use crate::error::{clear_last_error, set_last_error};
use crate::render_hooks::{
    MaterialHandle, RenderHookHandle, fill_with_material, render_column_hook, resolve_column_color,
};
use crate::types::{ColumnStrip, FrozenKind};
use hashbrown::HashMap;
use vello::Scene;
use vello::kurbo::{Affine, Rect};
use vello::peniko::{Brush, Color, Fill};
use vello_composition::SceneGraphCache;

#[repr(u32)]
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum VelloTdgTemplateOpCode {
    OpenNode = 0,
    CloseNode = 1,
    SetProperty = 2,
    BindProperty = 3,
}

#[repr(u32)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum VelloTdgTemplateNodeKind {
    Templates = 0,
    RowTemplate = 1,
    GroupHeaderTemplate = 2,
    SummaryTemplate = 3,
    ChromeTemplate = 4,
    PaneTemplate = 5,
    CellTemplate = 6,
    Stack = 7,
    Text = 8,
    Rectangle = 9,
    Image = 10,
    ContentPresenter = 11,
    Unknown = 12,
}

#[repr(u32)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum VelloTdgTemplateValueKind {
    String = 0,
    Number = 1,
    Boolean = 2,
    Binding = 3,
    Color = 4,
    Unknown = 5,
}

#[repr(u32)]
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum VelloTdgTemplatePaneKind {
    Primary = 0,
    Leading = 1,
    Trailing = 2,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct VelloTdgTemplateInstruction {
    pub op_code: VelloTdgTemplateOpCode,
    pub node_kind: VelloTdgTemplateNodeKind,
    pub value_kind: VelloTdgTemplateValueKind,
    pub property: *const c_char,
    pub value: *const c_char,
    pub number_value: f64,
    pub boolean_value: i32,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct VelloTdgTemplateBinding {
    pub path: *const c_char,
    pub kind: VelloTdgTemplateValueKind,
    pub number_value: f64,
    pub boolean_value: i32,
    pub string_value: *const c_char,
}

#[derive(Clone, Copy, Debug, Default)]
struct ColumnRenderConfig {
    color: Option<Color>,
    material: Option<MaterialHandle>,
    render_hook: Option<RenderHookHandle>,
}

#[derive(Clone, Copy, Debug, Default)]
struct PaneDefaults {
    color: Option<Color>,
    material: Option<MaterialHandle>,
    render_hook: Option<RenderHookHandle>,
}

#[derive(Clone, Copy)]
struct NodeContext {
    kind: VelloTdgTemplateNodeKind,
    pane: VelloTdgTemplatePaneKind,
    color: Option<Color>,
    material: Option<MaterialHandle>,
    render_hook: Option<RenderHookHandle>,
    column_key: Option<u32>,
}

pub struct TemplateProgram {
    pane_defaults: [PaneDefaults; 3],
    column_configs: HashMap<(u32, VelloTdgTemplatePaneKind), ColumnRenderConfig>,
}

impl TemplateProgram {
    pub fn from_instructions(
        instructions: &[VelloTdgTemplateInstruction],
    ) -> Result<Self, &'static str> {
        let mut program = TemplateProgram {
            pane_defaults: [PaneDefaults::default(); 3],
            column_configs: HashMap::new(),
        };

        let mut stack: Vec<NodeContext> = Vec::with_capacity(instructions.len());
        stack.push(NodeContext {
            kind: VelloTdgTemplateNodeKind::Templates,
            pane: VelloTdgTemplatePaneKind::Primary,
            color: None,
            material: None,
            render_hook: None,
            column_key: None,
        });

        for instruction in instructions {
            match instruction.op_code {
                VelloTdgTemplateOpCode::OpenNode => {
                    let parent = *stack.last().ok_or("template stack underflow")?;
                    stack.push(NodeContext {
                        kind: instruction.node_kind,
                        pane: parent.pane,
                        color: parent.color,
                        material: parent.material,
                        render_hook: parent.render_hook,
                        column_key: parent.column_key,
                    });
                }
                VelloTdgTemplateOpCode::SetProperty => {
                    if let Some(current) = stack.last_mut() {
                        if let Some(property) = cstr_to_str(instruction.property) {
                            match (current.kind, property) {
                                (VelloTdgTemplateNodeKind::PaneTemplate, "Pane") => {
                                    if let Some(pane) =
                                        parse_pane_kind(cstr_to_str(instruction.value))
                                    {
                                        current.pane = pane;
                                    }
                                }
                                (VelloTdgTemplateNodeKind::Rectangle, "Background") => {
                                    if let Some(color) = parse_color(cstr_to_str(instruction.value))
                                    {
                                        current.color = Some(color);
                                    }
                                }
                                (VelloTdgTemplateNodeKind::CellTemplate, "ColumnKey") => {
                                    current.column_key = parse_column_key(
                                        instruction,
                                        cstr_to_str(instruction.value),
                                    );
                                }
                                (_, "Material") => {
                                    current.material = parse_u32_value(
                                        instruction,
                                        cstr_to_str(instruction.value),
                                    )
                                    .map(|value| value as MaterialHandle);
                                }
                                (_, "RenderHook") => {
                                    current.render_hook = parse_u32_value(
                                        instruction,
                                        cstr_to_str(instruction.value),
                                    )
                                    .map(|value| value as RenderHookHandle);
                                }
                                _ => {}
                            }
                        }
                    }
                }
                VelloTdgTemplateOpCode::BindProperty => {
                    // Future binding support; currently ignored.
                }
                VelloTdgTemplateOpCode::CloseNode => {
                    let node = stack.pop().ok_or("template stack underflow")?;
                    if let Some(parent) = stack.last_mut() {
                        if node.kind == VelloTdgTemplateNodeKind::Rectangle {
                            if parent.color.is_none() {
                                parent.color = node.color;
                            }

                            if parent.material.is_none() {
                                parent.material = node.material;
                            }
                        }

                        if parent.render_hook.is_none() && node.render_hook.is_some() {
                            parent.render_hook = node.render_hook;
                        }

                        if parent.column_key.is_none() && node.column_key.is_some() {
                            parent.column_key = node.column_key;
                        }
                    }

                    match node.kind {
                        VelloTdgTemplateNodeKind::PaneTemplate => {
                            let slot = &mut program.pane_defaults[pane_index(node.pane)];
                            if slot.color.is_none() {
                                slot.color = node.color;
                            }
                            if slot.material.is_none() {
                                slot.material = node.material;
                            }
                            if slot.render_hook.is_none() {
                                slot.render_hook = node.render_hook;
                            }
                        }
                        VelloTdgTemplateNodeKind::CellTemplate => {
                            if let Some(key) = node.column_key {
                                let entry = program
                                    .column_configs
                                    .entry((key, node.pane))
                                    .or_insert_with(ColumnRenderConfig::default);
                                if entry.color.is_none() {
                                    entry.color = node.color;
                                }
                                if entry.material.is_none() {
                                    entry.material = node.material;
                                }
                                if entry.render_hook.is_none() {
                                    entry.render_hook = node.render_hook;
                                }
                            }
                        }
                        _ => {}
                    }
                }
            }
        }

        Ok(program)
    }

    pub fn encode_pane(
        &self,
        scene: &mut Scene,
        pane: VelloTdgTemplatePaneKind,
        columns: &[ColumnStrip],
    ) {
        scene.reset();
        if columns.is_empty() {
            return;
        }

        let height = 24.0;
        for column in columns {
            if self.render_with_column_config(scene, pane, column, height) {
                continue;
            }

            self.render_with_pane_defaults(scene, pane, column, height);
        }
    }

    fn render_with_column_config(
        &self,
        scene: &mut Scene,
        pane: VelloTdgTemplatePaneKind,
        column: &ColumnStrip,
        height: f64,
    ) -> bool {
        let key = column.key;
        if key != 0 {
            if let Some(config) = self.column_configs.get(&(key, pane)) {
                if self.apply_render_config(scene, column, height, config) {
                    return true;
                }
            }

            if pane != VelloTdgTemplatePaneKind::Primary {
                if let Some(config) = self
                    .column_configs
                    .get(&(key, VelloTdgTemplatePaneKind::Primary))
                {
                    if self.apply_render_config(scene, column, height, config) {
                        return true;
                    }
                }
            }
        }

        false
    }

    fn render_with_pane_defaults(
        &self,
        scene: &mut Scene,
        pane: VelloTdgTemplatePaneKind,
        column: &ColumnStrip,
        height: f64,
    ) {
        let defaults = self.pane_defaults[pane_index(pane)];

        if let Some(hook) = defaults.render_hook {
            if render_column_hook(hook, scene, column, height) {
                return;
            }
        }

        if let Some(material) = defaults.material {
            if self.fill_with_material_or_fallback(scene, column, height, material) {
                return;
            }
        }

        if let Some(color) = defaults.color {
            fill_with_color(scene, column, color, height);
            return;
        }

        fill_with_color(scene, column, default_color(), height);
    }

    fn apply_render_config(
        &self,
        scene: &mut Scene,
        column: &ColumnStrip,
        height: f64,
        config: &ColumnRenderConfig,
    ) -> bool {
        if let Some(hook) = config.render_hook {
            if render_column_hook(hook, scene, column, height) {
                return true;
            }
        }

        if let Some(material) = config.material {
            if self.fill_with_material_or_fallback(scene, column, height, material) {
                return true;
            }
        }

        if let Some(color) = config.color {
            fill_with_color(scene, column, color, height);
            return true;
        }

        false
    }

    fn fill_with_material_or_fallback(
        &self,
        scene: &mut Scene,
        column: &ColumnStrip,
        height: f64,
        material: MaterialHandle,
    ) -> bool {
        if fill_with_material(material, scene, column, height) {
            return true;
        }

        if let Some(color) = resolve_column_color(material) {
            fill_with_color(scene, column, color.to_color(), height);
            return true;
        }

        false
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_template_program_create(
    instructions_ptr: *const VelloTdgTemplateInstruction,
    instruction_count: usize,
) -> *mut TemplateProgram {
    clear_last_error();
    let instructions = if instruction_count == 0 {
        &[]
    } else if instructions_ptr.is_null() {
        set_last_error("null template instruction pointer");
        return ptr::null_mut();
    } else {
        unsafe { slice::from_raw_parts(instructions_ptr, instruction_count) }
    };

    match TemplateProgram::from_instructions(instructions) {
        Ok(program) => Box::into_raw(Box::new(program)),
        Err(message) => {
            set_last_error(message);
            ptr::null_mut()
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_template_program_destroy(handle: *mut TemplateProgram) {
    if !handle.is_null() {
        unsafe {
            drop(Box::from_raw(handle));
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_template_program_encode_pane(
    program: *mut TemplateProgram,
    cache: *mut SceneGraphCache,
    node_id: u32,
    pane_kind: VelloTdgTemplatePaneKind,
    columns_ptr: *const crate::interop::VelloTdgColumnPlan,
    column_len: usize,
    _bindings_ptr: *const VelloTdgTemplateBinding,
    _binding_len: usize,
) -> bool {
    clear_last_error();
    if program.is_null() {
        set_last_error("null template program handle");
        return false;
    }

    if cache.is_null() {
        set_last_error("null scene cache handle");
        return false;
    }

    let columns: Vec<ColumnStrip> = if column_len == 0 {
        Vec::new()
    } else if columns_ptr.is_null() {
        set_last_error("null columns pointer passed to template encode");
        return false;
    } else {
        unsafe { slice::from_raw_parts(columns_ptr, column_len) }
            .iter()
            .map(|plan| {
                ColumnStrip::new(
                    plan.offset,
                    plan.width,
                    FrozenKind::from(plan.frozen),
                    plan.key,
                )
            })
            .collect()
    };

    let cache = unsafe { &mut *cache };
    let Some(scene) = cache.scene_mut_by_index(node_id as usize) else {
        set_last_error("invalid scene node id in template encode");
        return false;
    };

    let program = unsafe { &mut *program };
    program.encode_pane(scene, pane_kind, &columns);
    true
}

fn pane_index(pane: VelloTdgTemplatePaneKind) -> usize {
    match pane {
        VelloTdgTemplatePaneKind::Primary => 0,
        VelloTdgTemplatePaneKind::Leading => 1,
        VelloTdgTemplatePaneKind::Trailing => 2,
    }
}

fn parse_u32_value(instruction: &VelloTdgTemplateInstruction, raw: Option<&str>) -> Option<u32> {
    match instruction.value_kind {
        VelloTdgTemplateValueKind::Number => {
            let value = instruction.number_value;
            if !value.is_finite() || value < 0.0 {
                return None;
            }

            Some(value.min(u32::MAX as f64).round() as u32)
        }
        _ => raw?.trim().parse().ok(),
    }
}

fn parse_column_key(instruction: &VelloTdgTemplateInstruction, raw: Option<&str>) -> Option<u32> {
    parse_u32_value(instruction, raw)
}

fn parse_pane_kind(value: Option<&str>) -> Option<VelloTdgTemplatePaneKind> {
    match value?.trim() {
        "Leading" => Some(VelloTdgTemplatePaneKind::Leading),
        "Trailing" => Some(VelloTdgTemplatePaneKind::Trailing),
        "Primary" => Some(VelloTdgTemplatePaneKind::Primary),
        _ => None,
    }
}

fn parse_color(value: Option<&str>) -> Option<Color> {
    let raw = value?.trim();
    if raw.is_empty() {
        return None;
    }

    if let Some(stripped) = raw.strip_prefix('#') {
        match stripped.len() {
            6 => {
                let r = u8::from_str_radix(&stripped[0..2], 16).ok()? as f32 / 255.0;
                let g = u8::from_str_radix(&stripped[2..4], 16).ok()? as f32 / 255.0;
                let b = u8::from_str_radix(&stripped[4..6], 16).ok()? as f32 / 255.0;
                return Some(Color::new([r, g, b, 1.0]));
            }
            8 => {
                let r = u8::from_str_radix(&stripped[0..2], 16).ok()? as f32 / 255.0;
                let g = u8::from_str_radix(&stripped[2..4], 16).ok()? as f32 / 255.0;
                let b = u8::from_str_radix(&stripped[4..6], 16).ok()? as f32 / 255.0;
                let a = u8::from_str_radix(&stripped[6..8], 16).ok()? as f32 / 255.0;
                return Some(Color::new([r, g, b, a]));
            }
            _ => {}
        }
    }

    let components: Vec<&str> = raw.split(',').map(str::trim).collect();
    if components.len() >= 3 {
        let r: f32 = components[0].parse().ok()?;
        let g: f32 = components[1].parse().ok()?;
        let b: f32 = components[2].parse().ok()?;
        let a: f32 = if components.len() >= 4 {
            components[3].parse().unwrap_or(1.0)
        } else {
            1.0
        };
        return Some(Color::new([
            r.clamp(0.0, 1.0),
            g.clamp(0.0, 1.0),
            b.clamp(0.0, 1.0),
            a.clamp(0.0, 1.0),
        ]));
    }

    None
}

fn default_color() -> Color {
    Color::new([0.18, 0.21, 0.28, 1.0])
}

fn cstr_to_str<'a>(ptr: *const c_char) -> Option<&'a str> {
    if ptr.is_null() {
        return None;
    }

    unsafe { CStr::from_ptr(ptr).to_str().ok() }
}

fn fill_with_color(scene: &mut Scene, column: &ColumnStrip, color: Color, height: f64) {
    if column.width <= 0.0 || height <= 0.0 {
        return;
    }

    let rect = Rect::new(column.offset, 0.0, column.offset + column.width, height);
    if rect.width() <= 0.0 || rect.height() <= 0.0 {
        return;
    }

    let brush = Brush::Solid(color);
    scene.fill(Fill::NonZero, Affine::IDENTITY, &brush, None, &rect);
}
