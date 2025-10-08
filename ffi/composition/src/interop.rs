use std::ffi::c_uchar;
use std::{ptr, slice, str};

use crate::animation::{
    DirtyIntent, EasingFunction, EasingTrackDescriptor, RepeatMode, SpringTrackDescriptor,
    TimelineGroupConfig, TimelineSample, TimelineSystem,
};
use crate::constraints::{LayoutConstraints, LayoutSize, ScalarConstraint};
use crate::layout::{self, PlotArea};
use crate::linear_layout::{self, LinearLayoutItem};
use crate::materials::{
    CompositionColor, CompositionMaterialDescriptor, CompositionShaderDescriptor,
    register_material, register_shader, resolve_material_color, unregister_material,
    unregister_shader,
};
use crate::panels::{
    DockLayoutChild, DockLayoutOptions, DockSide, GridLayoutChild, GridLayoutOptions, GridTrack,
    LayoutAlignment, LayoutOrientation, LayoutRect, LayoutThickness, StackLayoutChild,
    StackLayoutOptions, WrapLayoutChild, WrapLayoutLine, WrapLayoutOptions, WrapLayoutResult,
    solve_dock_layout, solve_grid_layout, solve_stack_layout, solve_wrap_layout,
};
use crate::scene_cache::{DirtyRegion, SceneGraphCache, SceneNodeId};
use crate::text;
use crate::virtualization::{
    ColumnSlice, ColumnStrip, ColumnViewportMetrics, FrozenKind, HybridVirtualizer, RowAction,
    RowPlanEntry, RowViewportMetrics, VirtualNodeId, VirtualizerTelemetry,
};

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionPlotArea {
    pub left: f64,
    pub top: f64,
    pub width: f64,
    pub height: f64,
}

impl From<PlotArea> for CompositionPlotArea {
    fn from(area: PlotArea) -> Self {
        Self {
            left: area.left,
            top: area.top,
            width: area.width,
            height: area.height,
        }
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy, Default)]
pub struct CompositionLabelMetrics {
    pub width: f32,
    pub height: f32,
    pub ascent: f32,
}

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionLinearLayoutItem {
    pub min: f64,
    pub preferred: f64,
    pub max: f64,
    pub weight: f64,
    pub margin_leading: f64,
    pub margin_trailing: f64,
}

#[repr(C)]
#[derive(Debug, Clone, Copy, Default)]
pub struct CompositionLinearLayoutSlot {
    pub offset: f64,
    pub length: f64,
}

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionScalarConstraint {
    pub min: f64,
    pub preferred: f64,
    pub max: f64,
}

impl From<CompositionScalarConstraint> for ScalarConstraint {
    fn from(value: CompositionScalarConstraint) -> Self {
        ScalarConstraint::new(value.min, value.preferred, value.max)
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionLayoutConstraints {
    pub width: CompositionScalarConstraint,
    pub height: CompositionScalarConstraint,
}

impl From<CompositionLayoutConstraints> for LayoutConstraints {
    fn from(value: CompositionLayoutConstraints) -> Self {
        LayoutConstraints {
            width: value.width.into(),
            height: value.height.into(),
        }
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy, Default)]
pub struct CompositionLayoutThickness {
    pub left: f64,
    pub top: f64,
    pub right: f64,
    pub bottom: f64,
}

impl From<CompositionLayoutThickness> for LayoutThickness {
    fn from(value: CompositionLayoutThickness) -> Self {
        LayoutThickness {
            left: value.left,
            top: value.top,
            right: value.right,
            bottom: value.bottom,
        }
    }
}

#[repr(u32)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CompositionLayoutOrientation {
    Horizontal = 0,
    Vertical = 1,
}

impl From<CompositionLayoutOrientation> for LayoutOrientation {
    fn from(value: CompositionLayoutOrientation) -> Self {
        match value {
            CompositionLayoutOrientation::Horizontal => LayoutOrientation::Horizontal,
            CompositionLayoutOrientation::Vertical => LayoutOrientation::Vertical,
        }
    }
}

impl From<LayoutOrientation> for CompositionLayoutOrientation {
    fn from(value: LayoutOrientation) -> Self {
        match value {
            LayoutOrientation::Horizontal => CompositionLayoutOrientation::Horizontal,
            LayoutOrientation::Vertical => CompositionLayoutOrientation::Vertical,
        }
    }
}

#[repr(u32)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CompositionLayoutAlignment {
    Start = 0,
    Center = 1,
    End = 2,
    Stretch = 3,
}

impl From<CompositionLayoutAlignment> for LayoutAlignment {
    fn from(value: CompositionLayoutAlignment) -> Self {
        match value {
            CompositionLayoutAlignment::Start => LayoutAlignment::Start,
            CompositionLayoutAlignment::Center => LayoutAlignment::Center,
            CompositionLayoutAlignment::End => LayoutAlignment::End,
            CompositionLayoutAlignment::Stretch => LayoutAlignment::Stretch,
        }
    }
}

impl From<LayoutAlignment> for CompositionLayoutAlignment {
    fn from(value: LayoutAlignment) -> Self {
        match value {
            LayoutAlignment::Start => CompositionLayoutAlignment::Start,
            LayoutAlignment::Center => CompositionLayoutAlignment::Center,
            LayoutAlignment::End => CompositionLayoutAlignment::End,
            LayoutAlignment::Stretch => CompositionLayoutAlignment::Stretch,
        }
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy, Default)]
pub struct CompositionLayoutRect {
    pub x: f64,
    pub y: f64,
    pub width: f64,
    pub height: f64,
    pub primary_offset: f64,
    pub primary_length: f64,
    pub line_index: u32,
}

impl From<LayoutRect> for CompositionLayoutRect {
    fn from(value: LayoutRect) -> Self {
        Self {
            x: value.x,
            y: value.y,
            width: value.width,
            height: value.height,
            primary_offset: value.primary_offset,
            primary_length: value.primary_length,
            line_index: value.line_index,
        }
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionStackLayoutChild {
    pub constraints: CompositionLayoutConstraints,
    pub weight: f64,
    pub margin: CompositionLayoutThickness,
    pub cross_alignment: CompositionLayoutAlignment,
}

impl From<&CompositionStackLayoutChild> for StackLayoutChild {
    fn from(value: &CompositionStackLayoutChild) -> Self {
        StackLayoutChild {
            constraints: value.constraints.into(),
            weight: value.weight,
            margin: value.margin.into(),
            cross_alignment: value.cross_alignment.into(),
        }
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionStackLayoutOptions {
    pub orientation: CompositionLayoutOrientation,
    pub spacing: f64,
    pub padding: CompositionLayoutThickness,
    pub cross_alignment: CompositionLayoutAlignment,
}

impl From<CompositionStackLayoutOptions> for StackLayoutOptions {
    fn from(value: CompositionStackLayoutOptions) -> Self {
        StackLayoutOptions {
            orientation: value.orientation.into(),
            spacing: value.spacing,
            padding: value.padding.into(),
            cross_alignment: value.cross_alignment.into(),
        }
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionWrapLayoutChild {
    pub constraints: CompositionLayoutConstraints,
    pub margin: CompositionLayoutThickness,
    pub line_break: u32,
}

impl From<&CompositionWrapLayoutChild> for WrapLayoutChild {
    fn from(value: &CompositionWrapLayoutChild) -> Self {
        let mut child = WrapLayoutChild::new(value.constraints.into());
        child.margin = value.margin.into();
        child.line_break = value.line_break != 0;
        child
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionWrapLayoutOptions {
    pub orientation: CompositionLayoutOrientation,
    pub item_spacing: f64,
    pub line_spacing: f64,
    pub padding: CompositionLayoutThickness,
    pub line_alignment: CompositionLayoutAlignment,
    pub cross_alignment: CompositionLayoutAlignment,
}

impl From<CompositionWrapLayoutOptions> for WrapLayoutOptions {
    fn from(value: CompositionWrapLayoutOptions) -> Self {
        WrapLayoutOptions {
            orientation: value.orientation.into(),
            item_spacing: value.item_spacing,
            line_spacing: value.line_spacing,
            padding: value.padding.into(),
            line_alignment: value.line_alignment.into(),
            cross_alignment: value.cross_alignment.into(),
        }
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy, Default)]
pub struct CompositionWrapLayoutLine {
    pub line_index: u32,
    pub start: u32,
    pub count: u32,
    pub primary_offset: f64,
    pub primary_length: f64,
}

impl From<&WrapLayoutLine> for CompositionWrapLayoutLine {
    fn from(value: &WrapLayoutLine) -> Self {
        Self {
            line_index: value.line_index,
            start: value.start as u32,
            count: value.count as u32,
            primary_offset: value.primary_offset,
            primary_length: value.primary_length,
        }
    }
}

#[repr(u32)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CompositionGridTrackKind {
    Fixed = 0,
    Auto = 1,
    Star = 2,
}

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionGridTrack {
    pub kind: CompositionGridTrackKind,
    pub value: f64,
    pub min: f64,
    pub max: f64,
}

impl From<&CompositionGridTrack> for GridTrack {
    fn from(value: &CompositionGridTrack) -> Self {
        let mut track = match value.kind {
            CompositionGridTrackKind::Fixed => GridTrack::fixed(value.value),
            CompositionGridTrackKind::Auto => GridTrack::auto(),
            CompositionGridTrackKind::Star => GridTrack::star(value.value),
        };
        track.min = value.min.max(0.0);
        track.max = if value.max <= 0.0 {
            f64::INFINITY
        } else {
            value.max
        };
        track
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionGridLayoutChild {
    pub constraints: CompositionLayoutConstraints,
    pub column: u16,
    pub column_span: u16,
    pub row: u16,
    pub row_span: u16,
    pub margin: CompositionLayoutThickness,
    pub horizontal_alignment: CompositionLayoutAlignment,
    pub vertical_alignment: CompositionLayoutAlignment,
}

impl From<&CompositionGridLayoutChild> for GridLayoutChild {
    fn from(value: &CompositionGridLayoutChild) -> Self {
        let mut child = GridLayoutChild::new(value.constraints.into(), value.column, value.row);
        child.column_span = value.column_span.max(1);
        child.row_span = value.row_span.max(1);
        child.margin = value.margin.into();
        child.horizontal_alignment = value.horizontal_alignment.into();
        child.vertical_alignment = value.vertical_alignment.into();
        child
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionGridLayoutOptions {
    pub padding: CompositionLayoutThickness,
    pub column_spacing: f64,
    pub row_spacing: f64,
}

impl From<CompositionGridLayoutOptions> for GridLayoutOptions {
    fn from(value: CompositionGridLayoutOptions) -> Self {
        GridLayoutOptions {
            padding: value.padding.into(),
            column_spacing: value.column_spacing,
            row_spacing: value.row_spacing,
        }
    }
}

#[repr(u32)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CompositionDockSide {
    Left = 0,
    Top = 1,
    Right = 2,
    Bottom = 3,
    Fill = 4,
}

impl From<CompositionDockSide> for DockSide {
    fn from(value: CompositionDockSide) -> Self {
        match value {
            CompositionDockSide::Left => DockSide::Left,
            CompositionDockSide::Top => DockSide::Top,
            CompositionDockSide::Right => DockSide::Right,
            CompositionDockSide::Bottom => DockSide::Bottom,
            CompositionDockSide::Fill => DockSide::Fill,
        }
    }
}

impl From<DockSide> for CompositionDockSide {
    fn from(value: DockSide) -> Self {
        match value {
            DockSide::Left => CompositionDockSide::Left,
            DockSide::Top => CompositionDockSide::Top,
            DockSide::Right => CompositionDockSide::Right,
            DockSide::Bottom => CompositionDockSide::Bottom,
            DockSide::Fill => CompositionDockSide::Fill,
        }
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionDockLayoutChild {
    pub constraints: CompositionLayoutConstraints,
    pub margin: CompositionLayoutThickness,
    pub side: CompositionDockSide,
    pub horizontal_alignment: CompositionLayoutAlignment,
    pub vertical_alignment: CompositionLayoutAlignment,
}

impl From<&CompositionDockLayoutChild> for DockLayoutChild {
    fn from(value: &CompositionDockLayoutChild) -> Self {
        let mut child = DockLayoutChild::new(value.constraints.into(), value.side.into());
        child.margin = value.margin.into();
        child.horizontal_alignment = value.horizontal_alignment.into();
        child.vertical_alignment = value.vertical_alignment.into();
        child
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionDockLayoutOptions {
    pub padding: CompositionLayoutThickness,
    pub spacing: f64,
    pub last_child_fill: u32,
}

impl From<CompositionDockLayoutOptions> for DockLayoutOptions {
    fn from(value: CompositionDockLayoutOptions) -> Self {
        DockLayoutOptions {
            padding: value.padding.into(),
            spacing: value.spacing,
            last_child_fill: value.last_child_fill != 0,
        }
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy, Default)]
pub struct CompositionDirtyRegion {
    pub min_x: f64,
    pub max_x: f64,
    pub min_y: f64,
    pub max_y: f64,
}

#[repr(u32)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CompositionTimelineDirtyKind {
    None = 0,
    Point = 1,
    Bounds = 2,
}

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionTimelineDirtyBinding {
    pub kind: CompositionTimelineDirtyKind,
    pub reserved: u32,
    pub x: f64,
    pub y: f64,
    pub min_x: f64,
    pub max_x: f64,
    pub min_y: f64,
    pub max_y: f64,
}

impl Default for CompositionTimelineDirtyBinding {
    fn default() -> Self {
        Self {
            kind: CompositionTimelineDirtyKind::None,
            reserved: 0,
            x: 0.0,
            y: 0.0,
            min_x: 0.0,
            max_x: 0.0,
            min_y: 0.0,
            max_y: 0.0,
        }
    }
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_stack_layout(
    options: *const CompositionStackLayoutOptions,
    children: *const CompositionStackLayoutChild,
    child_count: usize,
    available_width: f64,
    available_height: f64,
    out_rects: *mut CompositionLayoutRect,
    out_len: usize,
) -> usize {
    if options.is_null() {
        return 0;
    }

    let options = unsafe { (*options).into() };
    let available = LayoutSize::new(available_width, available_height).clamp_non_negative();

    let child_slice = if child_count == 0 {
        &[]
    } else if children.is_null() {
        return 0;
    } else {
        unsafe { slice::from_raw_parts(children, child_count) }
    };

    let mut stack_children = Vec::with_capacity(child_slice.len());
    for child in child_slice {
        stack_children.push(StackLayoutChild::from(child));
    }

    let rects = solve_stack_layout(&stack_children, options, available);
    if !out_rects.is_null() && out_len > 0 {
        let copy_len = rects.len().min(out_len);
        let destination = unsafe { slice::from_raw_parts_mut(out_rects, copy_len) };
        for (dst, src) in destination.iter_mut().zip(rects.iter()) {
            *dst = CompositionLayoutRect::from(*src);
        }
    }

    rects.len()
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_wrap_layout(
    options: *const CompositionWrapLayoutOptions,
    children: *const CompositionWrapLayoutChild,
    child_count: usize,
    available_width: f64,
    available_height: f64,
    out_rects: *mut CompositionLayoutRect,
    rect_len: usize,
    out_lines: *mut CompositionWrapLayoutLine,
    line_len: usize,
    out_line_count: *mut usize,
) -> usize {
    if options.is_null() {
        return 0;
    }

    let options = unsafe { (*options).into() };
    let available = LayoutSize::new(available_width, available_height).clamp_non_negative();

    let child_slice = if child_count == 0 {
        &[]
    } else if children.is_null() {
        return 0;
    } else {
        unsafe { slice::from_raw_parts(children, child_count) }
    };

    let mut wrap_children = Vec::with_capacity(child_slice.len());
    for child in child_slice {
        wrap_children.push(WrapLayoutChild::from(child));
    }

    let WrapLayoutResult { items, lines } = solve_wrap_layout(&wrap_children, options, available);

    if !out_rects.is_null() && rect_len > 0 {
        let copy_len = items.len().min(rect_len);
        let destination = unsafe { slice::from_raw_parts_mut(out_rects, copy_len) };
        for (dst, src) in destination.iter_mut().zip(items.iter()) {
            *dst = CompositionLayoutRect::from(*src);
        }
    }

    if !out_lines.is_null() && line_len > 0 {
        let copy_len = lines.len().min(line_len);
        let destination = unsafe { slice::from_raw_parts_mut(out_lines, copy_len) };
        for (dst, src) in destination.iter_mut().zip(lines.iter()) {
            *dst = CompositionWrapLayoutLine::from(src);
        }
    }

    if !out_line_count.is_null() {
        unsafe {
            *out_line_count = lines.len();
        }
    }

    items.len()
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_grid_layout(
    columns_ptr: *const CompositionGridTrack,
    columns_len: usize,
    rows_ptr: *const CompositionGridTrack,
    rows_len: usize,
    options: *const CompositionGridLayoutOptions,
    children: *const CompositionGridLayoutChild,
    child_count: usize,
    available_width: f64,
    available_height: f64,
    out_rects: *mut CompositionLayoutRect,
    rect_len: usize,
) -> usize {
    if columns_ptr.is_null()
        || rows_ptr.is_null()
        || options.is_null()
        || (child_count > 0 && children.is_null())
    {
        return 0;
    }

    let columns_slice = unsafe { slice::from_raw_parts(columns_ptr, columns_len) };
    let rows_slice = unsafe { slice::from_raw_parts(rows_ptr, rows_len) };
    let mut columns = Vec::with_capacity(columns_slice.len());
    let mut rows = Vec::with_capacity(rows_slice.len());
    columns.extend(columns_slice.iter().map(GridTrack::from));
    rows.extend(rows_slice.iter().map(GridTrack::from));

    let child_slice = if child_count == 0 {
        &[]
    } else {
        unsafe { slice::from_raw_parts(children, child_count) }
    };

    let mut grid_children = Vec::with_capacity(child_slice.len());
    for child in child_slice {
        grid_children.push(GridLayoutChild::from(child));
    }

    let options = unsafe { (*options).into() };
    let available = LayoutSize::new(available_width, available_height).clamp_non_negative();
    let rects = solve_grid_layout(&columns, &rows, &grid_children, options, available);

    if !out_rects.is_null() && rect_len > 0 {
        let copy_len = rects.len().min(rect_len);
        let destination = unsafe { slice::from_raw_parts_mut(out_rects, copy_len) };
        for (dst, src) in destination.iter_mut().zip(rects.iter()) {
            *dst = CompositionLayoutRect::from(*src);
        }
    }

    rects.len()
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_dock_layout(
    options: *const CompositionDockLayoutOptions,
    children: *const CompositionDockLayoutChild,
    child_count: usize,
    available_width: f64,
    available_height: f64,
    out_rects: *mut CompositionLayoutRect,
    rect_len: usize,
) -> usize {
    if options.is_null() {
        return 0;
    }

    let options = unsafe { (*options).into() };
    let available = LayoutSize::new(available_width, available_height).clamp_non_negative();
    let child_slice = if child_count == 0 {
        &[]
    } else if children.is_null() {
        return 0;
    } else {
        unsafe { slice::from_raw_parts(children, child_count) }
    };

    let mut dock_children = Vec::with_capacity(child_slice.len());
    for child in child_slice {
        dock_children.push(DockLayoutChild::from(child));
    }

    let rects = solve_dock_layout(&dock_children, options, available);

    if !out_rects.is_null() && rect_len > 0 {
        let copy_len = rects.len().min(rect_len);
        let destination = unsafe { slice::from_raw_parts_mut(out_rects, copy_len) };
        for (dst, src) in destination.iter_mut().zip(rects.iter()) {
            *dst = CompositionLayoutRect::from(*src);
        }
    }

    rects.len()
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_shader_register(
    handle: u32,
    descriptor: *const CompositionShaderDescriptor,
) -> bool {
    if descriptor.is_null() {
        return false;
    }

    let descriptor = unsafe { &*descriptor };
    register_shader(handle, descriptor).is_ok()
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_shader_unregister(handle: u32) {
    unregister_shader(handle);
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_material_register(
    handle: u32,
    descriptor: *const CompositionMaterialDescriptor,
) -> bool {
    if descriptor.is_null() {
        return false;
    }

    let descriptor = unsafe { &*descriptor };
    register_material(handle, descriptor).is_ok()
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_material_unregister(handle: u32) {
    unregister_material(handle);
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_material_resolve_color(
    handle: u32,
    out_color: *mut CompositionColor,
) -> bool {
    if out_color.is_null() {
        return false;
    }

    if let Some(color) = resolve_material_color(handle) {
        unsafe {
            *out_color = color;
        }
        true
    } else {
        false
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionTimelineGroupConfig {
    pub speed: f32,
    pub autoplay: i32,
}

impl Default for CompositionTimelineGroupConfig {
    fn default() -> Self {
        Self {
            speed: 1.0,
            autoplay: 1,
        }
    }
}

#[repr(u32)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CompositionTimelineRepeat {
    Once = 0,
    Loop = 1,
    PingPong = 2,
}

#[repr(u32)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CompositionTimelineEasing {
    Linear = 0,
    EaseInQuad = 1,
    EaseOutQuad = 2,
    EaseInOutQuad = 3,
    EaseInCubic = 4,
    EaseOutCubic = 5,
    EaseInOutCubic = 6,
    EaseInQuart = 7,
    EaseOutQuart = 8,
    EaseInOutQuart = 9,
    EaseInQuint = 10,
    EaseOutQuint = 11,
    EaseInOutQuint = 12,
    EaseInSine = 13,
    EaseOutSine = 14,
    EaseInOutSine = 15,
    EaseInExpo = 16,
    EaseOutExpo = 17,
    EaseInOutExpo = 18,
    EaseInCirc = 19,
    EaseOutCirc = 20,
    EaseInOutCirc = 21,
}

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionTimelineEasingTrackDesc {
    pub node_id: u32,
    pub channel_id: u16,
    pub reserved: u16,
    pub repeat: CompositionTimelineRepeat,
    pub easing: CompositionTimelineEasing,
    pub start_value: f32,
    pub end_value: f32,
    pub duration: f32,
    pub dirty_binding: CompositionTimelineDirtyBinding,
}

impl Default for CompositionTimelineEasingTrackDesc {
    fn default() -> Self {
        Self {
            node_id: u32::MAX,
            channel_id: 0,
            reserved: 0,
            repeat: CompositionTimelineRepeat::Once,
            easing: CompositionTimelineEasing::Linear,
            start_value: 0.0,
            end_value: 1.0,
            duration: 1.0,
            dirty_binding: CompositionTimelineDirtyBinding::default(),
        }
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionTimelineSpringTrackDesc {
    pub node_id: u32,
    pub channel_id: u16,
    pub reserved: u16,
    pub stiffness: f32,
    pub damping: f32,
    pub mass: f32,
    pub start_value: f32,
    pub initial_velocity: f32,
    pub target_value: f32,
    pub rest_velocity: f32,
    pub rest_offset: f32,
    pub dirty_binding: CompositionTimelineDirtyBinding,
}

impl Default for CompositionTimelineSpringTrackDesc {
    fn default() -> Self {
        Self {
            node_id: u32::MAX,
            channel_id: 0,
            reserved: 0,
            stiffness: 150.0,
            damping: 20.0,
            mass: 1.0,
            start_value: 0.0,
            initial_velocity: 0.0,
            target_value: 1.0,
            rest_velocity: 0.001,
            rest_offset: 0.001,
            dirty_binding: CompositionTimelineDirtyBinding::default(),
        }
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy, Default)]
pub struct CompositionTimelineSample {
    pub track_id: u32,
    pub node_id: u32,
    pub channel_id: u16,
    pub flags: u16,
    pub value: f32,
    pub velocity: f32,
    pub progress: f32,
}

pub struct CompositionVirtualizerHandle {
    pub(crate) inner: HybridVirtualizer,
}

#[repr(u32)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CompositionFrozenKind {
    None = 0,
    Leading = 1,
    Trailing = 2,
}

impl From<CompositionFrozenKind> for FrozenKind {
    fn from(value: CompositionFrozenKind) -> Self {
        match value {
            CompositionFrozenKind::Leading => FrozenKind::Leading,
            CompositionFrozenKind::Trailing => FrozenKind::Trailing,
            _ => FrozenKind::None,
        }
    }
}

impl From<FrozenKind> for CompositionFrozenKind {
    fn from(value: FrozenKind) -> Self {
        match value {
            FrozenKind::Leading => CompositionFrozenKind::Leading,
            FrozenKind::Trailing => CompositionFrozenKind::Trailing,
            FrozenKind::None => CompositionFrozenKind::None,
        }
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionVirtualRowMetric {
    pub node_id: u32,
    pub height: f64,
}

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionVirtualColumnStrip {
    pub offset: f64,
    pub width: f64,
    pub frozen: CompositionFrozenKind,
    pub key: u32,
}

impl From<&CompositionVirtualColumnStrip> for ColumnStrip {
    fn from(value: &CompositionVirtualColumnStrip) -> Self {
        ColumnStrip::new(value.offset, value.width, value.frozen.into(), value.key)
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionRowViewportMetrics {
    pub scroll_offset: f64,
    pub viewport_extent: f64,
    pub overscan: f64,
}

impl From<CompositionRowViewportMetrics> for RowViewportMetrics {
    fn from(value: CompositionRowViewportMetrics) -> Self {
        RowViewportMetrics {
            scroll_offset: value.scroll_offset,
            viewport_extent: value.viewport_extent,
            overscan: value.overscan,
        }
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct CompositionColumnViewportMetrics {
    pub scroll_offset: f64,
    pub viewport_extent: f64,
    pub overscan: f64,
}

impl From<CompositionColumnViewportMetrics> for ColumnViewportMetrics {
    fn from(value: CompositionColumnViewportMetrics) -> Self {
        ColumnViewportMetrics {
            scroll_offset: value.scroll_offset,
            viewport_extent: value.viewport_extent,
            overscan: value.overscan,
        }
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy, Default)]
pub struct CompositionColumnSlice {
    pub primary_start: u32,
    pub primary_count: u32,
    pub frozen_leading: u32,
    pub frozen_trailing: u32,
}

impl From<ColumnSlice> for CompositionColumnSlice {
    fn from(value: ColumnSlice) -> Self {
        Self {
            primary_start: value.primary_start,
            primary_count: value.primary_count,
            frozen_leading: value.frozen_leading,
            frozen_trailing: value.frozen_trailing,
        }
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy, Default)]
pub struct CompositionVirtualizerTelemetry {
    pub rows_total: u32,
    pub window_len: u32,
    pub reused: u32,
    pub adopted: u32,
    pub allocated: u32,
    pub recycled: u32,
    pub active_buffers: u32,
    pub free_buffers: u32,
    pub evicted: u32,
}

impl From<VirtualizerTelemetry> for CompositionVirtualizerTelemetry {
    fn from(value: VirtualizerTelemetry) -> Self {
        Self {
            rows_total: value.rows_total,
            window_len: value.window_len,
            reused: value.reused,
            adopted: value.adopted,
            allocated: value.allocated,
            recycled: value.recycled,
            active_buffers: value.active_buffers,
            free_buffers: value.free_buffers,
            evicted: value.evicted,
        }
    }
}

#[repr(u32)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CompositionRowAction {
    Reuse = 0,
    Adopt = 1,
    Allocate = 2,
    Recycle = 3,
}

impl Default for CompositionRowAction {
    fn default() -> Self {
        CompositionRowAction::Reuse
    }
}

impl From<RowAction> for CompositionRowAction {
    fn from(value: RowAction) -> Self {
        match value {
            RowAction::Reuse => CompositionRowAction::Reuse,
            RowAction::Adopt => CompositionRowAction::Adopt,
            RowAction::Allocate => CompositionRowAction::Allocate,
            RowAction::Recycle => CompositionRowAction::Recycle,
        }
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy, Default)]
pub struct CompositionRowPlanEntry {
    pub node_id: u32,
    pub buffer_id: u32,
    pub top: f64,
    pub height: f32,
    pub action: CompositionRowAction,
}

impl From<&RowPlanEntry> for CompositionRowPlanEntry {
    fn from(value: &RowPlanEntry) -> Self {
        Self {
            node_id: value.node_id.0,
            buffer_id: value.buffer_id,
            top: value.top,
            height: value.height,
            action: value.action.into(),
        }
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy, Default)]
pub struct CompositionRowWindow {
    pub start_index: u32,
    pub end_index: u32,
    pub total_height: f64,
}

impl From<CompositionTimelineDirtyBinding> for DirtyIntent {
    fn from(binding: CompositionTimelineDirtyBinding) -> Self {
        match binding.kind {
            CompositionTimelineDirtyKind::Point => DirtyIntent::Point {
                x: binding.x,
                y: binding.y,
            },
            CompositionTimelineDirtyKind::Bounds => {
                let (min_x, max_x) = if binding.min_x <= binding.max_x {
                    (binding.min_x, binding.max_x)
                } else {
                    (binding.max_x, binding.min_x)
                };
                let (min_y, max_y) = if binding.min_y <= binding.max_y {
                    (binding.min_y, binding.max_y)
                } else {
                    (binding.max_y, binding.min_y)
                };
                DirtyIntent::Bounds {
                    min_x,
                    max_x,
                    min_y,
                    max_y,
                }
            }
            _ => DirtyIntent::None,
        }
    }
}

impl From<CompositionTimelineRepeat> for RepeatMode {
    fn from(value: CompositionTimelineRepeat) -> Self {
        match value {
            CompositionTimelineRepeat::Once => RepeatMode::Once,
            CompositionTimelineRepeat::Loop => RepeatMode::Loop,
            CompositionTimelineRepeat::PingPong => RepeatMode::PingPong,
        }
    }
}

impl From<CompositionTimelineEasing> for EasingFunction {
    fn from(value: CompositionTimelineEasing) -> Self {
        match value {
            CompositionTimelineEasing::Linear => EasingFunction::Linear,
            CompositionTimelineEasing::EaseInQuad => EasingFunction::EaseInQuad,
            CompositionTimelineEasing::EaseOutQuad => EasingFunction::EaseOutQuad,
            CompositionTimelineEasing::EaseInOutQuad => EasingFunction::EaseInOutQuad,
            CompositionTimelineEasing::EaseInCubic => EasingFunction::EaseInCubic,
            CompositionTimelineEasing::EaseOutCubic => EasingFunction::EaseOutCubic,
            CompositionTimelineEasing::EaseInOutCubic => EasingFunction::EaseInOutCubic,
            CompositionTimelineEasing::EaseInQuart => EasingFunction::EaseInQuart,
            CompositionTimelineEasing::EaseOutQuart => EasingFunction::EaseOutQuart,
            CompositionTimelineEasing::EaseInOutQuart => EasingFunction::EaseInOutQuart,
            CompositionTimelineEasing::EaseInQuint => EasingFunction::EaseInQuint,
            CompositionTimelineEasing::EaseOutQuint => EasingFunction::EaseOutQuint,
            CompositionTimelineEasing::EaseInOutQuint => EasingFunction::EaseInOutQuint,
            CompositionTimelineEasing::EaseInSine => EasingFunction::EaseInSine,
            CompositionTimelineEasing::EaseOutSine => EasingFunction::EaseOutSine,
            CompositionTimelineEasing::EaseInOutSine => EasingFunction::EaseInOutSine,
            CompositionTimelineEasing::EaseInExpo => EasingFunction::EaseInExpo,
            CompositionTimelineEasing::EaseOutExpo => EasingFunction::EaseOutExpo,
            CompositionTimelineEasing::EaseInOutExpo => EasingFunction::EaseInOutExpo,
            CompositionTimelineEasing::EaseInCirc => EasingFunction::EaseInCirc,
            CompositionTimelineEasing::EaseOutCirc => EasingFunction::EaseOutCirc,
            CompositionTimelineEasing::EaseInOutCirc => EasingFunction::EaseInOutCirc,
        }
    }
}

impl From<&TimelineSample> for CompositionTimelineSample {
    fn from(sample: &TimelineSample) -> Self {
        Self {
            track_id: sample.track_id,
            node_id: sample.node_id.index() as u32,
            channel_id: sample.channel_id,
            flags: sample.flags,
            value: sample.value,
            velocity: sample.velocity,
            progress: sample.progress,
        }
    }
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_compute_plot_area(
    width: f64,
    height: f64,
    out_area: *mut CompositionPlotArea,
) -> bool {
    if out_area.is_null() {
        return false;
    }
    let plot = layout::compute_plot_area(width, height);
    unsafe {
        ptr::write(out_area, CompositionPlotArea::from(plot));
    }
    true
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_measure_label(
    text_ptr: *const c_uchar,
    text_len: usize,
    font_size: f32,
    out_metrics: *mut CompositionLabelMetrics,
) -> bool {
    if out_metrics.is_null() {
        return false;
    }

    let text = if text_ptr.is_null() || text_len == 0 {
        ""
    } else {
        let bytes = unsafe { slice::from_raw_parts(text_ptr, text_len) };
        match str::from_utf8(bytes) {
            Ok(value) => value,
            Err(_) => return false,
        }
    };

    match text::layout_label(text, font_size) {
        Some(layout) => {
            let metrics = CompositionLabelMetrics {
                width: layout.width,
                height: layout.height,
                ascent: layout.ascent,
            };
            unsafe {
                ptr::write(out_metrics, metrics);
            }
            true
        }
        None => false,
    }
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_solve_linear_layout(
    items_ptr: *const CompositionLinearLayoutItem,
    item_count: usize,
    available: f64,
    spacing: f64,
    out_slots_ptr: *mut CompositionLinearLayoutSlot,
    out_slots_len: usize,
) -> usize {
    if items_ptr.is_null()
        || out_slots_ptr.is_null()
        || item_count == 0
        || out_slots_len < item_count
    {
        return 0;
    }

    let items = unsafe { slice::from_raw_parts(items_ptr, item_count) };
    let mut solver_items = Vec::with_capacity(item_count);
    for item in items {
        let constraint = ScalarConstraint::new(item.min, item.preferred, item.max);
        let config = LinearLayoutItem::new(constraint)
            .with_weight(item.weight)
            .with_margins(item.margin_leading, item.margin_trailing);
        solver_items.push(config);
    }

    let slots = linear_layout::solve_linear_layout(&solver_items, available, spacing);
    if slots.len() != item_count {
        return 0;
    }

    let out_slots = unsafe { slice::from_raw_parts_mut(out_slots_ptr, out_slots_len) };
    for (index, slot) in slots.iter().enumerate() {
        out_slots[index] = CompositionLinearLayoutSlot {
            offset: slot.offset,
            length: slot.length,
        };
    }

    item_count
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_scene_cache_create() -> *mut SceneGraphCache {
    Box::into_raw(Box::new(SceneGraphCache::new()))
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_scene_cache_destroy(cache: *mut SceneGraphCache) {
    if !cache.is_null() {
        unsafe {
            drop(Box::from_raw(cache));
        }
    }
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_scene_cache_create_node(
    cache: *mut SceneGraphCache,
    parent_id: u32,
) -> u32 {
    if cache.is_null() {
        return u32::MAX;
    }

    let cache = unsafe { &mut *cache };
    let parent = if parent_id == u32::MAX {
        None
    } else {
        Some(SceneNodeId(parent_id as usize))
    };
    cache.create_node(parent).0 as u32
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_scene_cache_dispose_node(
    cache: *mut SceneGraphCache,
    node_id: u32,
) {
    if cache.is_null() {
        return;
    }
    let cache = unsafe { &mut *cache };
    cache.dispose_node(SceneNodeId(node_id as usize));
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_scene_cache_mark_dirty(
    cache: *mut SceneGraphCache,
    node_id: u32,
    x: f64,
    y: f64,
) -> bool {
    if cache.is_null() {
        return false;
    }

    let cache = unsafe { &mut *cache };
    cache.mark_dirty(SceneNodeId(node_id as usize), x, y);
    true
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_scene_cache_mark_dirty_bounds(
    cache: *mut SceneGraphCache,
    node_id: u32,
    min_x: f64,
    max_x: f64,
    min_y: f64,
    max_y: f64,
) -> bool {
    if cache.is_null() {
        return false;
    }

    let cache = unsafe { &mut *cache };
    cache.mark_dirty_bounds(SceneNodeId(node_id as usize), min_x, max_x, min_y, max_y);
    true
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_scene_cache_take_dirty(
    cache: *mut SceneGraphCache,
    node_id: u32,
    out_region: *mut CompositionDirtyRegion,
) -> bool {
    if cache.is_null() || out_region.is_null() {
        return false;
    }
    let cache = unsafe { &mut *cache };
    match cache.take_dirty_recursive(SceneNodeId(node_id as usize)) {
        Some(region) => {
            unsafe { ptr::write(out_region, CompositionDirtyRegion::from(region)) };
            true
        }
        None => false,
    }
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_scene_cache_clear(
    cache: *mut SceneGraphCache,
    node_id: u32,
) {
    if cache.is_null() {
        return;
    }
    let cache = unsafe { &mut *cache };
    cache.clear(SceneNodeId(node_id as usize));
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_timeline_system_create() -> *mut TimelineSystem {
    Box::into_raw(Box::new(TimelineSystem::new()))
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_timeline_system_destroy(system: *mut TimelineSystem) {
    if !system.is_null() {
        unsafe {
            drop(Box::from_raw(system));
        }
    }
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_timeline_group_create(
    system: *mut TimelineSystem,
    config: CompositionTimelineGroupConfig,
) -> u32 {
    if system.is_null() {
        return u32::MAX;
    }

    let autoplay = config.autoplay != 0;
    let group_config = TimelineGroupConfig {
        speed: config.speed.max(0.0),
        autoplay,
    };

    let system = unsafe { &mut *system };
    system.create_group(group_config)
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_timeline_group_destroy(
    system: *mut TimelineSystem,
    group_id: u32,
) {
    if system.is_null() {
        return;
    }

    let system = unsafe { &mut *system };
    system.destroy_group(group_id);
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_timeline_group_play(
    system: *mut TimelineSystem,
    group_id: u32,
) {
    if system.is_null() {
        return;
    }
    let system = unsafe { &mut *system };
    system.set_group_playing(group_id, true);
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_timeline_group_pause(
    system: *mut TimelineSystem,
    group_id: u32,
) {
    if system.is_null() {
        return;
    }
    let system = unsafe { &mut *system };
    system.set_group_playing(group_id, false);
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_timeline_group_set_speed(
    system: *mut TimelineSystem,
    group_id: u32,
    speed: f32,
) {
    if system.is_null() {
        return;
    }
    let system = unsafe { &mut *system };
    system.set_group_speed(group_id, speed);
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_timeline_add_easing_track(
    system: *mut TimelineSystem,
    group_id: u32,
    descriptor: *const CompositionTimelineEasingTrackDesc,
) -> u32 {
    if system.is_null() || descriptor.is_null() {
        return u32::MAX;
    }

    let system = unsafe { &mut *system };
    let descriptor = unsafe { ptr::read(descriptor) };

    let node_id = if descriptor.node_id == u32::MAX {
        return u32::MAX;
    } else {
        SceneNodeId(descriptor.node_id as usize)
    };

    let track_descriptor = EasingTrackDescriptor {
        node_id,
        channel_id: descriptor.channel_id,
        repeat: descriptor.repeat.into(),
        easing: descriptor.easing.into(),
        start_value: descriptor.start_value,
        end_value: descriptor.end_value,
        duration: descriptor.duration,
        dirty_intent: descriptor.dirty_binding.into(),
    };

    system
        .add_easing_track(group_id, track_descriptor)
        .unwrap_or(u32::MAX)
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_timeline_add_spring_track(
    system: *mut TimelineSystem,
    group_id: u32,
    descriptor: *const CompositionTimelineSpringTrackDesc,
) -> u32 {
    if system.is_null() || descriptor.is_null() {
        return u32::MAX;
    }

    let system = unsafe { &mut *system };
    let descriptor = unsafe { ptr::read(descriptor) };

    let node_id = if descriptor.node_id == u32::MAX {
        return u32::MAX;
    } else {
        SceneNodeId(descriptor.node_id as usize)
    };

    let track_descriptor = SpringTrackDescriptor {
        node_id,
        channel_id: descriptor.channel_id,
        stiffness: descriptor.stiffness,
        damping: descriptor.damping,
        mass: descriptor.mass,
        start_value: descriptor.start_value,
        initial_velocity: descriptor.initial_velocity,
        target_value: descriptor.target_value,
        rest_velocity: descriptor.rest_velocity,
        rest_offset: descriptor.rest_offset,
        dirty_intent: descriptor.dirty_binding.into(),
    };

    system
        .add_spring_track(group_id, track_descriptor)
        .unwrap_or(u32::MAX)
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_timeline_track_remove(
    system: *mut TimelineSystem,
    track_id: u32,
) {
    if system.is_null() {
        return;
    }
    let system = unsafe { &mut *system };
    system.remove_track(track_id);
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_timeline_track_reset(
    system: *mut TimelineSystem,
    track_id: u32,
) {
    if system.is_null() {
        return;
    }

    let system = unsafe { &mut *system };
    system.reset_track(track_id);
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_timeline_track_set_spring_target(
    system: *mut TimelineSystem,
    track_id: u32,
    target_value: f32,
) {
    if system.is_null() {
        return;
    }
    let system = unsafe { &mut *system };
    system.set_spring_target(track_id, target_value);
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_timeline_tick(
    system: *mut TimelineSystem,
    delta_seconds: f64,
    cache: *mut SceneGraphCache,
    out_samples: *mut CompositionTimelineSample,
    out_len: usize,
) -> usize {
    if system.is_null() {
        return 0;
    }

    let system = unsafe { &mut *system };
    let cache_option = if cache.is_null() {
        None
    } else {
        Some(unsafe { &mut *cache })
    };

    let samples = system.tick(delta_seconds, cache_option);
    let produced = samples.len();

    if !out_samples.is_null() && out_len > 0 {
        let copy_len = produced.min(out_len);
        let destination = unsafe { slice::from_raw_parts_mut(out_samples, copy_len) };
        for (dst, src) in destination.iter_mut().zip(samples.iter()) {
            *dst = CompositionTimelineSample::from(src);
        }
    }

    produced
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_virtualizer_create() -> *mut CompositionVirtualizerHandle
{
    let handle = CompositionVirtualizerHandle {
        inner: HybridVirtualizer::new(),
    };
    Box::into_raw(Box::new(handle))
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_virtualizer_destroy(
    handle: *mut CompositionVirtualizerHandle,
) {
    if handle.is_null() {
        return;
    }

    unsafe {
        drop(Box::from_raw(handle));
    }
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_virtualizer_clear(
    handle: *mut CompositionVirtualizerHandle,
) {
    if handle.is_null() {
        return;
    }

    let handle = unsafe { &mut *handle };
    handle.inner.clear();
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_virtualizer_set_rows(
    handle: *mut CompositionVirtualizerHandle,
    rows_ptr: *const CompositionVirtualRowMetric,
    rows_len: usize,
) {
    if handle.is_null() {
        return;
    }

    let rows_slice = if rows_len == 0 {
        &[]
    } else if rows_ptr.is_null() {
        return;
    } else {
        unsafe { slice::from_raw_parts(rows_ptr, rows_len) }
    };

    let mut rows = Vec::with_capacity(rows_slice.len());
    for row in rows_slice {
        rows.push((VirtualNodeId(row.node_id), row.height.max(0.0)));
    }

    let handle = unsafe { &mut *handle };
    handle.inner.set_rows(&rows);
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_virtualizer_set_columns(
    handle: *mut CompositionVirtualizerHandle,
    columns_ptr: *const CompositionVirtualColumnStrip,
    columns_len: usize,
) {
    if handle.is_null() {
        return;
    }

    let columns_slice = if columns_len == 0 {
        &[]
    } else if columns_ptr.is_null() {
        return;
    } else {
        unsafe { slice::from_raw_parts(columns_ptr, columns_len) }
    };

    let mut columns = Vec::with_capacity(columns_slice.len());
    for strip in columns_slice {
        columns.push(ColumnStrip::from(strip));
    }

    let handle = unsafe { &mut *handle };
    handle.inner.set_columns(&columns);
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_virtualizer_plan(
    handle: *mut CompositionVirtualizerHandle,
    row_metrics: CompositionRowViewportMetrics,
    column_metrics: CompositionColumnViewportMetrics,
) {
    if handle.is_null() {
        return;
    }

    let handle = unsafe { &mut *handle };
    let row_metrics = RowViewportMetrics::from(row_metrics);
    let column_metrics = ColumnViewportMetrics::from(column_metrics);
    handle.inner.plan(row_metrics, column_metrics);
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_virtualizer_copy_plan(
    handle: *const CompositionVirtualizerHandle,
    out_entries: *mut CompositionRowPlanEntry,
    out_len: usize,
) -> usize {
    if handle.is_null() {
        return 0;
    }

    let handle = unsafe { &*handle };
    let plan = handle.inner.row_plan();
    if out_entries.is_null() || out_len == 0 {
        return plan.len();
    }

    let copy_len = plan.len().min(out_len);
    let destination = unsafe { slice::from_raw_parts_mut(out_entries, copy_len) };
    for (dst, src) in destination.iter_mut().zip(plan.iter()) {
        *dst = CompositionRowPlanEntry::from(src);
    }

    plan.len()
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_virtualizer_copy_recycle(
    handle: *const CompositionVirtualizerHandle,
    out_entries: *mut CompositionRowPlanEntry,
    out_len: usize,
) -> usize {
    if handle.is_null() {
        return 0;
    }

    let handle = unsafe { &*handle };
    let plan = handle.inner.recycle_plan();
    if out_entries.is_null() || out_len == 0 {
        return plan.len();
    }

    let copy_len = plan.len().min(out_len);
    let destination = unsafe { slice::from_raw_parts_mut(out_entries, copy_len) };
    for (dst, src) in destination.iter_mut().zip(plan.iter()) {
        *dst = CompositionRowPlanEntry::from(src);
    }

    plan.len()
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_virtualizer_window(
    handle: *const CompositionVirtualizerHandle,
    out_window: *mut CompositionRowWindow,
) -> bool {
    if handle.is_null() || out_window.is_null() {
        return false;
    }

    let handle = unsafe { &*handle };
    let window = handle.inner.row_window().clone();
    let total = handle.inner.total_height();
    let output = unsafe { &mut *out_window };
    output.start_index = window.start as u32;
    output.end_index = window.end as u32;
    output.total_height = total;
    true
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_virtualizer_telemetry(
    handle: *const CompositionVirtualizerHandle,
    out_telemetry: *mut CompositionVirtualizerTelemetry,
) -> bool {
    if handle.is_null() || out_telemetry.is_null() {
        return false;
    }

    let handle = unsafe { &*handle };
    let telemetry = handle.inner.telemetry();
    let output = unsafe { &mut *out_telemetry };
    *output = CompositionVirtualizerTelemetry::from(telemetry);
    true
}

#[unsafe(no_mangle)]
#[allow(clippy::missing_safety_doc)]
pub unsafe extern "C" fn vello_composition_virtualizer_column_slice(
    handle: *const CompositionVirtualizerHandle,
    out_slice: *mut CompositionColumnSlice,
) -> bool {
    if handle.is_null() || out_slice.is_null() {
        return false;
    }

    let handle = unsafe { &*handle };
    let slice = handle.inner.column_slice();
    let output = unsafe { &mut *out_slice };
    *output = CompositionColumnSlice::from(slice);
    true
}

impl From<DirtyRegion> for CompositionDirtyRegion {
    fn from(region: DirtyRegion) -> Self {
        Self {
            min_x: region.min_x,
            max_x: region.max_x,
            min_y: region.min_y,
            max_y: region.max_y,
        }
    }
}
