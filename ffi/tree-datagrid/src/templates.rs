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
use vello_composition::{SceneGraphCache, layout_label, label_font};

const DEFAULT_TEXT_FONT_SIZE: f32 = 13.0;
const DEFAULT_TEXT_HORIZONTAL_PADDING: f64 = 8.0;
const DEFAULT_TEXT_VERTICAL_PADDING: f64 = 4.0;
const DEFAULT_TEXTBOX_HORIZONTAL_PADDING: f64 = 10.0;
const DEFAULT_TEXTBOX_VERTICAL_PADDING: f64 = 6.0;
fn default_textbox_background() -> Color {
    Color::from_rgba8(0x24, 0x2C, 0x3A, 0xFF)
}

fn default_textbox_foreground() -> Color {
    Color::from_rgba8(0xE4, 0xE9, 0xF2, 0xFF)
}

fn default_text_foreground() -> Color {
    Color::from_rgba8(0xE4, 0xE9, 0xF2, 0xFF)
}

#[repr(u32)]
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum VelloTdgTemplateOpCode {
    OpenNode = 0,
    CloseNode = 1,
    SetProperty = 2,
    BindProperty = 3,
}

fn is_text_node(kind: VelloTdgTemplateNodeKind) -> bool {
    matches!(
        kind,
        VelloTdgTemplateNodeKind::Text
            | VelloTdgTemplateNodeKind::AccessText
            | VelloTdgTemplateNodeKind::TextBox
    )
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
    AccessText = 12,
    TextBox = 13,
    Unknown = 14,
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
    text_index: Option<usize>,
}

#[derive(Clone, Debug, Default)]
struct TextTemplateBuilder {
    kind: VelloTdgTemplateNodeKind,
    literal: Option<String>,
    binding_path: Option<String>,
    foreground: Option<Color>,
    background: Option<Color>,
    font_size: Option<f32>,
}

impl TextTemplateBuilder {
    fn new(kind: VelloTdgTemplateNodeKind) -> Self {
        Self {
            kind,
            ..Default::default()
        }
    }

    fn apply_property(
        &mut self,
        property: &str,
        instruction: &VelloTdgTemplateInstruction,
        raw: Option<&str>,
    ) {
        match property {
            "Content" => {
                if let Some(value) = raw {
                    self.literal = Some(value.to_owned());
                }
            }
            "Foreground" => {
                if let Some(color) = parse_color(raw) {
                    self.foreground = Some(color);
                }
            }
            "Background" => {
                if let Some(color) = parse_color(raw) {
                    self.background = Some(color);
                }
            }
            "FontSize" => {
                if let Some(size) = parse_f32_value(instruction, raw) {
                    if size.is_finite() && size > 0.0 {
                        self.font_size = Some(size);
                    }
                }
            }
            _ => {}
        }
    }

    fn bind_property(
        &mut self,
        property: &str,
        instruction: &VelloTdgTemplateInstruction,
        raw: Option<&str>,
    ) {
        if property == "Content" {
            if let Some(path) = raw {
                if !path.is_empty() {
                    self.binding_path = Some(path.to_owned());
                }
            }
        }
    }

    fn into_template(
        mut self,
        pane: VelloTdgTemplatePaneKind,
        column_key: Option<u32>,
    ) -> Option<TextTemplate> {
        if self.literal.is_none() && self.binding_path.is_none() {
            return None;
        }

        let mut foreground = self.foreground;
        let mut background = self.background;
        let font_size = self.font_size.unwrap_or(DEFAULT_TEXT_FONT_SIZE);

        match self.kind {
            VelloTdgTemplateNodeKind::TextBox => {
                background = background.or(Some(default_textbox_background()));
                foreground = Some(foreground.unwrap_or(default_textbox_foreground()));
            }
            _ => {
                foreground = Some(foreground.unwrap_or(default_text_foreground()));
            }
        }

        Some(TextTemplate {
            pane,
            column_key,
            literal: self.literal,
            binding_path: self.binding_path,
            foreground,
            background,
            font_size,
            kind: self.kind,
        })
    }
}

#[derive(Clone, Debug)]
struct TextTemplate {
    pane: VelloTdgTemplatePaneKind,
    column_key: Option<u32>,
    literal: Option<String>,
    binding_path: Option<String>,
    foreground: Option<Color>,
    background: Option<Color>,
    font_size: f32,
    kind: VelloTdgTemplateNodeKind,
}

impl TextTemplate {
    fn resolve_content(&self, bindings: &BindingMap) -> Option<String> {
        if let Some(literal) = &self.literal {
            return Some(self.normalize_content(literal.clone()));
        }

        let path = self.binding_path.as_ref()?;
        let value = bindings.get(path)?;
        Some(self.normalize_content(value))
    }

    fn normalize_content(&self, value: String) -> String {
        match self.kind {
            VelloTdgTemplateNodeKind::AccessText => normalize_access_text(&value),
            _ => value,
        }
    }

    fn padding(&self) -> (f64, f64) {
        match self.kind {
            VelloTdgTemplateNodeKind::TextBox => (
                DEFAULT_TEXTBOX_HORIZONTAL_PADDING,
                DEFAULT_TEXTBOX_VERTICAL_PADDING,
            ),
            _ => (DEFAULT_TEXT_HORIZONTAL_PADDING, DEFAULT_TEXT_VERTICAL_PADDING),
        }
    }
}
pub struct TemplateProgram {
    pane_defaults: [PaneDefaults; 3],
    column_configs: HashMap<(u32, VelloTdgTemplatePaneKind), ColumnRenderConfig>,
    column_text: HashMap<(u32, VelloTdgTemplatePaneKind), Vec<TextTemplate>>,
    pane_text: HashMap<VelloTdgTemplatePaneKind, Vec<TextTemplate>>,
}

impl TemplateProgram {
    pub fn from_instructions(
        instructions: &[VelloTdgTemplateInstruction],
    ) -> Result<Self, &'static str> {
        let mut program = TemplateProgram {
            pane_defaults: [PaneDefaults::default(); 3],
            column_configs: HashMap::new(),
            column_text: HashMap::new(),
            pane_text: HashMap::new(),
        };

        let mut text_builders: Vec<TextTemplateBuilder> = Vec::new();
        let mut stack: Vec<NodeContext> = Vec::with_capacity(instructions.len());
        stack.push(NodeContext {
            kind: VelloTdgTemplateNodeKind::Templates,
            pane: VelloTdgTemplatePaneKind::Primary,
            color: None,
            material: None,
            render_hook: None,
            column_key: None,
            text_index: None,
        });

        for instruction in instructions {
            match instruction.op_code {
                VelloTdgTemplateOpCode::OpenNode => {
                    let parent = *stack.last().ok_or("template stack underflow")?;
                    let mut context = NodeContext {
                        kind: instruction.node_kind,
                        pane: parent.pane,
                        color: parent.color,
                        material: parent.material,
                        render_hook: parent.render_hook,
                        column_key: parent.column_key,
                        text_index: None,
                    };

                    if is_text_node(context.kind) {
                        let index = text_builders.len();
                        text_builders.push(TextTemplateBuilder::new(context.kind));
                        context.text_index = Some(index);
                    }

                    stack.push(context);
                }
                VelloTdgTemplateOpCode::SetProperty => {
                    if let Some(current) = stack.last_mut() {
                        if let Some(property) = cstr_to_str(instruction.property) {
                            let raw = cstr_to_str(instruction.value);
                            match (current.kind, property) {
                                (VelloTdgTemplateNodeKind::PaneTemplate, "Pane") => {
                                    if let Some(pane) = parse_pane_kind(raw) {
                                        current.pane = pane;
                                    }
                                }
                                (VelloTdgTemplateNodeKind::Rectangle, "Background") => {
                                    if let Some(color) = parse_color(raw) {
                                        current.color = Some(color);
                                    }
                                }
                                (VelloTdgTemplateNodeKind::CellTemplate, "ColumnKey") => {
                                    current.column_key =
                                        parse_column_key(instruction, raw);
                                }
                                (_, "Material") => {
                                    current.material = parse_u32_value(
                                        instruction,
                                        raw,
                                    )
                                    .map(|value| value as MaterialHandle);
                                }
                                (_, "RenderHook") => {
                                    current.render_hook = parse_u32_value(
                                        instruction,
                                        raw,
                                    )
                                    .map(|value| value as RenderHookHandle);
                                }
                                _ => {}
                            }

                            if let Some(index) = current.text_index {
                                if let Some(builder) = text_builders.get_mut(index) {
                                    builder.apply_property(property, instruction, raw);
                                }
                            }
                        }
                    }
                }
                VelloTdgTemplateOpCode::BindProperty => {
                    if let Some(current) = stack.last_mut() {
                        if let Some(property) = cstr_to_str(instruction.property) {
                            let raw = cstr_to_str(instruction.value);
                            if let Some(index) = current.text_index {
                                if let Some(builder) = text_builders.get_mut(index) {
                                    builder.bind_property(property, instruction, raw);
                                }
                            }
                        }
                    }
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

                    if let Some(index) = node.text_index {
                        if let Some(builder) = text_builders.get(index) {
                            if let Some(template) =
                                builder.clone().into_template(node.pane, node.column_key)
                            {
                                if let Some(key) = template.column_key {
                                    program
                                        .column_text
                                        .entry((key, template.pane))
                                        .or_insert_with(Vec::new)
                                        .push(template);
                                } else {
                                    program
                                        .pane_text
                                        .entry(template.pane)
                                        .or_insert_with(Vec::new)
                                        .push(template);
                                }
                            }
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
        bindings: &BindingMap,
    ) {
        scene.reset();
        if columns.is_empty() {
            return;
        }

        let height = 24.0;
        for column in columns {
            if self.render_with_column_config(scene, pane, column, height) {
                self.render_column_text(scene, pane, column, height, bindings);
                continue;
            }

            self.render_with_pane_defaults(scene, pane, column, height);
            self.render_column_text(scene, pane, column, height, bindings);
        }

        self.render_pane_text(scene, pane, columns, height, bindings);
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

    fn render_column_text(
        &self,
        scene: &mut Scene,
        pane: VelloTdgTemplatePaneKind,
        column: &ColumnStrip,
        height: f64,
        bindings: &BindingMap,
    ) {
        if let Some(templates) = self.column_text.get(&(column.key, pane)) {
            for template in templates {
                self.draw_text(scene, column, height, template, bindings);
            }
        }

        if pane != VelloTdgTemplatePaneKind::Primary {
            if let Some(templates) =
                self.column_text
                    .get(&(column.key, VelloTdgTemplatePaneKind::Primary))
            {
                for template in templates {
                    self.draw_text(scene, column, height, template, bindings);
                }
            }
        }
    }

    fn render_pane_text(
        &self,
        scene: &mut Scene,
        pane: VelloTdgTemplatePaneKind,
        columns: &[ColumnStrip],
        height: f64,
        bindings: &BindingMap,
    ) {
        if columns.is_empty() {
            return;
        }

        let left = columns.first().map(|c| c.offset).unwrap_or(0.0);
        let right = columns
            .last()
            .map(|c| c.offset + c.width)
            .unwrap_or(left);
        let width = (right - left).max(0.0);

        if width <= 0.0 {
            return;
        }

        let pane_strip = ColumnStrip::new(left, width, pane_to_frozen(pane), 0);

        if let Some(templates) = self.pane_text.get(&pane) {
            for template in templates {
                self.draw_text(scene, &pane_strip, height, template, bindings);
            }
        }

        if pane != VelloTdgTemplatePaneKind::Primary {
            if let Some(templates) =
                self.pane_text
                    .get(&VelloTdgTemplatePaneKind::Primary)
            {
                for template in templates {
                    self.draw_text(scene, &pane_strip, height, template, bindings);
                }
            }
        }
    }

    fn draw_text(
        &self,
        scene: &mut Scene,
        column: &ColumnStrip,
        height: f64,
        template: &TextTemplate,
        bindings: &BindingMap,
    ) {
        let Some(content) = template.resolve_content(bindings) else {
            return;
        };

        if let Some(background) = template.background {
            fill_with_color(scene, column, background, height);
        }

        if content.trim().is_empty() {
            return;
        }

        let layout = match layout_label(&content, template.font_size) {
            Some(layout) => layout,
            None => return,
        };

        let (padding_x, padding_y) = template.padding();
        let available_width = column.width - 2.0 * padding_x;
        if !available_width.is_finite() || available_width <= 0.0 {
            return;
        }

        let baseline_x = column.offset + padding_x;
        let text_height = f64::from(layout.height);
        let ascent = f64::from(layout.ascent);
        let mut baseline_y = (height - text_height) * 0.5 + ascent;
        if !baseline_y.is_finite() {
            baseline_y = ascent;
        }

        let brush_color = template
            .foreground
            .unwrap_or_else(default_text_foreground);

        scene
            .draw_glyphs(label_font())
            .font_size(template.font_size)
            .transform(Affine::translate((baseline_x, baseline_y)))
            .brush(Brush::Solid(brush_color))
            .draw(Fill::NonZero, layout.glyphs.into_iter());
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

#[derive(Default)]
struct BindingMap {
    values: HashMap<String, BindingValue>,
}

enum BindingValue {
    Text(String),
    Number(f64),
    Boolean(bool),
}

impl BindingMap {
    fn from_slice(
        bindings_ptr: *const VelloTdgTemplateBinding,
        binding_len: usize,
    ) -> Self {
        if bindings_ptr.is_null() || binding_len == 0 {
            return Self {
                values: HashMap::new(),
            };
        }

        let entries = unsafe { slice::from_raw_parts(bindings_ptr, binding_len) };
        let mut values = HashMap::with_capacity(entries.len());
        for entry in entries {
            let Some(path) = cstr_to_str(entry.path) else {
                continue;
            };

            let value = match entry.kind {
                VelloTdgTemplateValueKind::Number => BindingValue::Number(entry.number_value),
                VelloTdgTemplateValueKind::Boolean => BindingValue::Boolean(entry.boolean_value != 0),
                _ => {
                    let text = cstr_to_str(entry.string_value).unwrap_or_default();
                    BindingValue::Text(text.to_owned())
                }
            };

            values.insert(path.to_owned(), value);
        }

        Self { values }
    }

    fn get(&self, path: &str) -> Option<String> {
        let value = self.values.get(path)?;
        Some(match value {
            BindingValue::Text(text) => text.clone(),
            BindingValue::Number(number) => format!("{number}"),
            BindingValue::Boolean(flag) => {
                if *flag {
                    String::from("True")
                } else {
                    String::from("False")
                }
            }
        })
    }
}

fn parse_f32_value(
    instruction: &VelloTdgTemplateInstruction,
    raw: Option<&str>,
) -> Option<f32> {
    match instruction.value_kind {
        VelloTdgTemplateValueKind::Number => Some(instruction.number_value as f32),
        _ => raw?.trim().parse::<f32>().ok(),
    }
}

fn normalize_access_text(value: &str) -> String {
    if !value.contains('_') {
        return value.to_owned();
    }

    let mut result = String::with_capacity(value.len());
    let mut chars = value.chars().peekable();
    while let Some(ch) = chars.next() {
        if ch == '_' {
            match chars.peek() {
                Some('_') => {
                    result.push('_');
                    chars.next();
                }
                Some(_) => {
                    if let Some(next) = chars.next() {
                        result.push(next);
                    }
                }
                None => {}
            }
        } else {
            result.push(ch);
        }
    }

    result
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
    bindings_ptr: *const VelloTdgTemplateBinding,
    binding_len: usize,
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
    let bindings = BindingMap::from_slice(bindings_ptr, binding_len);
    program.encode_pane(scene, pane_kind, &columns, &bindings);
    true
}

fn pane_to_frozen(pane: VelloTdgTemplatePaneKind) -> FrozenKind {
    match pane {
        VelloTdgTemplatePaneKind::Leading => FrozenKind::Leading,
        VelloTdgTemplatePaneKind::Trailing => FrozenKind::Trailing,
        _ => FrozenKind::None,
    }
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
