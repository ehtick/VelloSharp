use std::{ffi::c_char, ptr, slice};

use crate::color::VelloTdgColor;
use crate::data_model::{
    ModelDiff, ModelDiffKind, NodeDescriptor, NodeId, RowKind, SelectionDiff, SelectionMode,
    TreeDataModel,
};
use crate::error::{clear_last_error, last_error_ptr, set_last_error};
use crate::renderer::{FrameStats, RendererLoop, RendererOptions};
use crate::scene::{
    GroupHeaderVisual, RowChromeVisual, RowVisual, SummaryVisual, encode_chrome,
    encode_group_header, encode_row, encode_summary,
};
use crate::types::{ColumnStrip, FrozenColumns, FrozenKind};
use crate::virtualization::{
    ColumnSlice, ColumnViewportMetrics, HybridVirtualizer, RowAction, RowPlanEntry,
    RowViewportMetrics, VirtualizerTelemetry,
};
use vello_composition::SceneGraphCache;

pub struct VelloTdgModelHandle {
    pub(crate) inner: TreeDataModel,
}

pub struct VelloTdgVirtualizerHandle {
    pub(crate) inner: HybridVirtualizer,
}

pub struct VelloTdgRendererHandle {
    pub(crate) inner: RendererLoop,
}

#[repr(u32)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum VelloTdgRowKind {
    Data = 0,
    GroupHeader = 1,
    Summary = 2,
}

impl From<VelloTdgRowKind> for RowKind {
    fn from(kind: VelloTdgRowKind) -> Self {
        match kind {
            VelloTdgRowKind::Data => RowKind::Data,
            VelloTdgRowKind::GroupHeader => RowKind::GroupHeader,
            VelloTdgRowKind::Summary => RowKind::Summary,
        }
    }
}

impl From<RowKind> for VelloTdgRowKind {
    fn from(kind: RowKind) -> Self {
        match kind {
            RowKind::Data => VelloTdgRowKind::Data,
            RowKind::GroupHeader => VelloTdgRowKind::GroupHeader,
            RowKind::Summary => VelloTdgRowKind::Summary,
        }
    }
}

#[repr(u32)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum VelloTdgModelDiffKind {
    Inserted = 0,
    Removed = 1,
    Expanded = 2,
    Collapsed = 3,
}

impl From<ModelDiffKind> for VelloTdgModelDiffKind {
    fn from(kind: ModelDiffKind) -> Self {
        match kind {
            ModelDiffKind::Inserted => VelloTdgModelDiffKind::Inserted,
            ModelDiffKind::Removed => VelloTdgModelDiffKind::Removed,
            ModelDiffKind::Expanded => VelloTdgModelDiffKind::Expanded,
            ModelDiffKind::Collapsed => VelloTdgModelDiffKind::Collapsed,
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgNodeDescriptor {
    pub key: u64,
    pub row_kind: VelloTdgRowKind,
    pub height: f32,
    pub has_children: u32,
}

impl From<VelloTdgNodeDescriptor> for NodeDescriptor {
    fn from(value: VelloTdgNodeDescriptor) -> Self {
        NodeDescriptor {
            key: value.key,
            row_kind: value.row_kind.into(),
            height: value.height.max(0.0),
            has_children: value.has_children != 0,
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgModelDiff {
    pub node_id: u32,
    pub parent_id: u32,
    pub index: u32,
    pub depth: u32,
    pub row_kind: VelloTdgRowKind,
    pub kind: VelloTdgModelDiffKind,
    pub height: f32,
    pub has_children: u32,
    pub is_expanded: u32,
    pub key: u64,
}

impl From<&ModelDiff> for VelloTdgModelDiff {
    fn from(diff: &ModelDiff) -> Self {
        Self {
            node_id: diff.node_id.0,
            parent_id: diff.parent_id.unwrap_or(NodeId(u32::MAX)).0,
            index: diff.index,
            depth: diff.depth,
            row_kind: diff.row_kind.into(),
            kind: diff.kind.into(),
            height: diff.height,
            has_children: diff.has_children as u32,
            is_expanded: diff.is_expanded as u32,
            key: diff.key,
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgSelectionDiff {
    pub node_id: u32,
    pub is_selected: u32,
}

impl From<&SelectionDiff> for VelloTdgSelectionDiff {
    fn from(diff: &SelectionDiff) -> Self {
        Self {
            node_id: diff.node_id.0,
            is_selected: diff.is_selected as u32,
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgNodeMetadata {
    pub key: u64,
    pub depth: u32,
    pub height: f32,
    pub row_kind: VelloTdgRowKind,
    pub is_expanded: u32,
    pub is_selected: u32,
    pub has_children: u32,
}

#[repr(u32)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum VelloTdgSelectionMode {
    Replace = 0,
    Add = 1,
    Toggle = 2,
    Range = 3,
}

impl From<VelloTdgSelectionMode> for SelectionMode {
    fn from(mode: VelloTdgSelectionMode) -> Self {
        match mode {
            VelloTdgSelectionMode::Replace => SelectionMode::Replace,
            VelloTdgSelectionMode::Add => SelectionMode::Add,
            VelloTdgSelectionMode::Toggle => SelectionMode::Toggle,
            VelloTdgSelectionMode::Range => SelectionMode::Range,
        }
    }
}

#[repr(u32)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum VelloTdgFrozenKind {
    None = 0,
    Leading = 1,
    Trailing = 2,
}

impl From<VelloTdgFrozenKind> for FrozenKind {
    fn from(kind: VelloTdgFrozenKind) -> Self {
        match kind {
            VelloTdgFrozenKind::None => FrozenKind::None,
            VelloTdgFrozenKind::Leading => FrozenKind::Leading,
            VelloTdgFrozenKind::Trailing => FrozenKind::Trailing,
        }
    }
}

impl From<FrozenKind> for VelloTdgFrozenKind {
    fn from(kind: FrozenKind) -> Self {
        match kind {
            FrozenKind::None => VelloTdgFrozenKind::None,
            FrozenKind::Leading => VelloTdgFrozenKind::Leading,
            FrozenKind::Trailing => VelloTdgFrozenKind::Trailing,
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgColumnPlan {
    pub offset: f64,
    pub width: f64,
    pub frozen: VelloTdgFrozenKind,
    pub key: u32,
}

impl From<&VelloTdgColumnPlan> for ColumnStrip {
    fn from(plan: &VelloTdgColumnPlan) -> Self {
        ColumnStrip {
            offset: plan.offset,
            width: plan.width,
            frozen: plan.frozen.into(),
            key: plan.key,
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgRowVisual {
    pub width: f64,
    pub height: f64,
    pub depth: u32,
    pub indent: f64,
    pub background: VelloTdgColor,
    pub hover_background: VelloTdgColor,
    pub selection_fill: VelloTdgColor,
    pub outline: VelloTdgColor,
    pub outline_width: f32,
    pub stripe: VelloTdgColor,
    pub stripe_width: f32,
    pub is_selected: u32,
    pub is_hovered: u32,
}

impl From<&VelloTdgRowVisual> for RowVisual {
    fn from(visual: &VelloTdgRowVisual) -> Self {
        RowVisual {
            width: visual.width,
            height: visual.height,
            depth: visual.depth,
            indent: visual.indent,
            background: visual.background,
            hover_background: visual.hover_background,
            selection_fill: visual.selection_fill,
            outline: visual.outline,
            outline_width: visual.outline_width,
            stripe: visual.stripe,
            stripe_width: visual.stripe_width,
            is_selected: visual.is_selected != 0,
            is_hovered: visual.is_hovered != 0,
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgGroupHeaderVisual {
    pub width: f64,
    pub height: f64,
    pub depth: u32,
    pub indent: f64,
    pub background: VelloTdgColor,
    pub accent: VelloTdgColor,
    pub outline: VelloTdgColor,
    pub outline_width: f32,
}

impl From<&VelloTdgGroupHeaderVisual> for GroupHeaderVisual {
    fn from(visual: &VelloTdgGroupHeaderVisual) -> Self {
        GroupHeaderVisual {
            width: visual.width,
            height: visual.height,
            depth: visual.depth,
            indent: visual.indent,
            background: visual.background,
            accent: visual.accent,
            outline: visual.outline,
            outline_width: visual.outline_width,
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgSummaryVisual {
    pub width: f64,
    pub height: f64,
    pub highlight: VelloTdgColor,
    pub background: VelloTdgColor,
    pub outline: VelloTdgColor,
    pub outline_width: f32,
}

impl From<&VelloTdgSummaryVisual> for SummaryVisual {
    fn from(visual: &VelloTdgSummaryVisual) -> Self {
        SummaryVisual {
            width: visual.width,
            height: visual.height,
            highlight: visual.highlight,
            background: visual.background,
            outline: visual.outline,
            outline_width: visual.outline_width,
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgRowChromeVisual {
    pub width: f64,
    pub height: f64,
    pub grid_color: VelloTdgColor,
    pub grid_width: f32,
    pub frozen_leading: u32,
    pub frozen_trailing: u32,
    pub frozen_fill: VelloTdgColor,
}

impl From<&VelloTdgRowChromeVisual> for RowChromeVisual {
    fn from(visual: &VelloTdgRowChromeVisual) -> Self {
        RowChromeVisual {
            width: visual.width,
            height: visual.height,
            grid_color: visual.grid_color,
            grid_width: visual.grid_width,
            frozen: FrozenColumns {
                leading: visual.frozen_leading,
                trailing: visual.frozen_trailing,
            },
            frozen_fill: visual.frozen_fill,
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgRowMetric {
    pub node_id: u32,
    pub height: f32,
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgColumnMetric {
    pub offset: f64,
    pub width: f64,
    pub frozen: VelloTdgFrozenKind,
    pub key: u32,
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgViewportMetrics {
    pub row_scroll_offset: f64,
    pub row_viewport_height: f64,
    pub row_overscan: f64,
    pub column_scroll_offset: f64,
    pub column_viewport_width: f64,
    pub column_overscan: f64,
}

#[repr(u32)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum VelloTdgRowAction {
    Reuse = 0,
    Adopt = 1,
    Allocate = 2,
    Recycle = 3,
}

impl From<RowAction> for VelloTdgRowAction {
    fn from(action: RowAction) -> Self {
        match action {
            RowAction::Reuse => VelloTdgRowAction::Reuse,
            RowAction::Adopt => VelloTdgRowAction::Adopt,
            RowAction::Allocate => VelloTdgRowAction::Allocate,
            RowAction::Recycle => VelloTdgRowAction::Recycle,
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgRowPlanEntry {
    pub node_id: u32,
    pub buffer_id: u32,
    pub top: f64,
    pub height: f32,
    pub action: VelloTdgRowAction,
}

impl From<&RowPlanEntry> for VelloTdgRowPlanEntry {
    fn from(entry: &RowPlanEntry) -> Self {
        Self {
            node_id: entry.node_id.0,
            buffer_id: entry.buffer_id,
            top: entry.top,
            height: entry.height,
            action: entry.action.into(),
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgColumnSlice {
    pub primary_start: u32,
    pub primary_count: u32,
    pub frozen_leading: u32,
    pub frozen_trailing: u32,
}

impl From<ColumnSlice> for VelloTdgColumnSlice {
    fn from(slice: ColumnSlice) -> Self {
        Self {
            primary_start: slice.primary_start,
            primary_count: slice.primary_count,
            frozen_leading: slice.frozen_leading,
            frozen_trailing: slice.frozen_trailing,
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgVirtualizerTelemetry {
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

impl From<VirtualizerTelemetry> for VelloTdgVirtualizerTelemetry {
    fn from(telemetry: VirtualizerTelemetry) -> Self {
        Self {
            rows_total: telemetry.rows_total,
            window_len: telemetry.window_len,
            reused: telemetry.reused,
            adopted: telemetry.adopted,
            allocated: telemetry.allocated,
            recycled: telemetry.recycled,
            active_buffers: telemetry.active_buffers,
            free_buffers: telemetry.free_buffers,
            evicted: telemetry.evicted,
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgRowWindow {
    pub start_index: u32,
    pub end_index: u32,
    pub total_height: f64,
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgRendererOptions {
    pub target_fps: f32,
}

impl From<VelloTdgRendererOptions> for RendererOptions {
    fn from(options: VelloTdgRendererOptions) -> Self {
        RendererOptions {
            target_fps: if options.target_fps.is_finite() && options.target_fps > 0.0 {
                options.target_fps
            } else {
                RendererOptions::default().target_fps
            },
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgFrameStats {
    pub frame_index: u64,
    pub cpu_time_ms: f32,
    pub gpu_time_ms: f32,
    pub queue_time_ms: f32,
    pub frame_interval_ms: f32,
    pub gpu_sample_count: u32,
    pub timestamp_ms: i64,
}

impl From<FrameStats> for VelloTdgFrameStats {
    fn from(stats: FrameStats) -> Self {
        Self {
            frame_index: stats.frame_index,
            cpu_time_ms: stats.cpu_time_ms,
            gpu_time_ms: stats.gpu_time_ms,
            queue_time_ms: stats.queue_time_ms,
            frame_interval_ms: stats.frame_interval_ms,
            gpu_sample_count: stats.gpu_sample_count,
            timestamp_ms: stats.timestamp_ms,
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct VelloTdgGpuTimestampSummary {
    pub gpu_time_ms: f32,
    pub queue_time_ms: f32,
    pub sample_count: u32,
}

#[unsafe(no_mangle)]
pub extern "C" fn vello_tdg_last_error_message() -> *const c_char {
    last_error_ptr()
}

#[unsafe(no_mangle)]
pub extern "C" fn vello_tdg_model_create() -> *mut VelloTdgModelHandle {
    clear_last_error();
    Box::into_raw(Box::new(VelloTdgModelHandle {
        inner: TreeDataModel::new(),
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_model_destroy(handle: *mut VelloTdgModelHandle) {
    if !handle.is_null() {
        unsafe {
            drop(Box::from_raw(handle));
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_model_clear(handle: *mut VelloTdgModelHandle) {
    if let Some(model) = unsafe { handle.as_mut() } {
        model.inner.clear();
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_model_attach_roots(
    handle: *mut VelloTdgModelHandle,
    descriptors: *const VelloTdgNodeDescriptor,
    descriptor_count: usize,
) -> bool {
    clear_last_error();
    let Some(model) = (unsafe { handle.as_mut() }) else {
        set_last_error("null model handle passed to attach_roots");
        return false;
    };

    let slice = if descriptor_count == 0 {
        &[][..]
    } else if descriptors.is_null() {
        set_last_error("null descriptor pointer passed to attach_roots");
        return false;
    } else {
        unsafe { slice::from_raw_parts(descriptors, descriptor_count) }
    };

    let descriptors: Vec<NodeDescriptor> = slice.iter().copied().map(Into::into).collect();
    match model.inner.attach_roots(&descriptors) {
        Ok(()) => true,
        Err(err) => {
            set_last_error(err.message());
            false
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_model_attach_children(
    handle: *mut VelloTdgModelHandle,
    parent_id: u32,
    descriptors: *const VelloTdgNodeDescriptor,
    descriptor_count: usize,
) -> bool {
    clear_last_error();
    let Some(model) = (unsafe { handle.as_mut() }) else {
        set_last_error("null model handle passed to attach_children");
        return false;
    };

    let slice = if descriptor_count == 0 {
        &[][..]
    } else if descriptors.is_null() {
        set_last_error("null descriptor pointer passed to attach_children");
        return false;
    } else {
        unsafe { slice::from_raw_parts(descriptors, descriptor_count) }
    };

    let descriptors: Vec<NodeDescriptor> = slice.iter().copied().map(Into::into).collect();
    match model.inner.attach_children(NodeId(parent_id), &descriptors) {
        Ok(()) => true,
        Err(err) => {
            set_last_error(err.message());
            false
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_model_set_expanded(
    handle: *mut VelloTdgModelHandle,
    node_id: u32,
    expanded: u32,
) -> bool {
    clear_last_error();
    let Some(model) = (unsafe { handle.as_mut() }) else {
        set_last_error("null model handle passed to set_expanded");
        return false;
    };

    match model.inner.set_expanded(NodeId(node_id), expanded != 0) {
        Ok(_) => true,
        Err(err) => {
            set_last_error(err.message());
            false
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_model_set_selected(
    handle: *mut VelloTdgModelHandle,
    node_id: u32,
    mode: VelloTdgSelectionMode,
) -> bool {
    clear_last_error();
    let Some(model) = (unsafe { handle.as_mut() }) else {
        set_last_error("null model handle passed to set_selected");
        return false;
    };

    match model.inner.set_selected(NodeId(node_id), mode.into()) {
        Ok(()) => true,
        Err(err) => {
            set_last_error(err.message());
            false
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_model_select_range(
    handle: *mut VelloTdgModelHandle,
    anchor_id: u32,
    focus_id: u32,
) -> bool {
    clear_last_error();
    let Some(model) = (unsafe { handle.as_mut() }) else {
        set_last_error("null model handle passed to select_range");
        return false;
    };

    if let Err(err) = model
        .inner
        .set_selected(NodeId(anchor_id), SelectionMode::Replace)
    {
        set_last_error(err.message());
        return false;
    }

    match model
        .inner
        .set_selected(NodeId(focus_id), SelectionMode::Range)
    {
        Ok(()) => true,
        Err(err) => {
            set_last_error(err.message());
            false
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_model_diff_count(handle: *mut VelloTdgModelHandle) -> usize {
    if let Some(model) = unsafe { handle.as_ref() } {
        model.inner.model_diffs().len()
    } else {
        0
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_model_copy_diffs(
    handle: *mut VelloTdgModelHandle,
    out_ptr: *mut VelloTdgModelDiff,
    out_len: usize,
) -> usize {
    let Some(model) = (unsafe { handle.as_mut() }) else {
        return 0;
    };
    let diffs = model.inner.model_diffs();
    if out_ptr.is_null() || out_len == 0 {
        return diffs.len();
    }
    let count = diffs.len().min(out_len);
    let target = unsafe { slice::from_raw_parts_mut(out_ptr, count) };
    for (idx, diff) in diffs.iter().take(count).enumerate() {
        target[idx] = VelloTdgModelDiff::from(diff);
    }
    model.inner.drain_model_diffs(count);
    count
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_model_selection_diff_count(
    handle: *mut VelloTdgModelHandle,
) -> usize {
    if let Some(model) = unsafe { handle.as_ref() } {
        model.inner.selection_diffs().len()
    } else {
        0
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_model_copy_selection_diffs(
    handle: *mut VelloTdgModelHandle,
    out_ptr: *mut VelloTdgSelectionDiff,
    out_len: usize,
) -> usize {
    let Some(model) = (unsafe { handle.as_mut() }) else {
        return 0;
    };
    let diffs = model.inner.selection_diffs();
    if out_ptr.is_null() || out_len == 0 {
        return diffs.len();
    }
    let count = diffs.len().min(out_len);
    let target = unsafe { slice::from_raw_parts_mut(out_ptr, count) };
    for (idx, diff) in diffs.iter().take(count).enumerate() {
        target[idx] = VelloTdgSelectionDiff::from(diff);
    }
    model.inner.drain_selection_diffs(count);
    count
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_model_dequeue_materialization(
    handle: *mut VelloTdgModelHandle,
    out_node_id: *mut u32,
) -> bool {
    let Some(model) = (unsafe { handle.as_mut() }) else {
        return false;
    };

    let Some(node_id) = model.inner.dequeue_materialization() else {
        return false;
    };

    if !out_node_id.is_null() {
        unsafe {
            ptr::write(out_node_id, node_id.0);
        }
    }

    true
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_model_node_metadata(
    handle: *mut VelloTdgModelHandle,
    node_id: u32,
    out_metadata: *mut VelloTdgNodeMetadata,
) -> bool {
    let Some(model) = (unsafe { handle.as_mut() }) else {
        return false;
    };
    let Some(metadata) = model.inner.node_metadata(NodeId(node_id)) else {
        return false;
    };
    if out_metadata.is_null() {
        return false;
    }

    unsafe {
        ptr::write(
            out_metadata,
            VelloTdgNodeMetadata {
                key: metadata.key,
                depth: metadata.depth,
                height: metadata.height,
                row_kind: metadata.row_kind.into(),
                is_expanded: metadata.is_expanded as u32,
                is_selected: metadata.is_selected as u32,
                has_children: metadata.has_children as u32,
            },
        );
    }

    true
}

#[unsafe(no_mangle)]
pub extern "C" fn vello_tdg_virtualizer_create() -> *mut VelloTdgVirtualizerHandle {
    clear_last_error();
    Box::into_raw(Box::new(VelloTdgVirtualizerHandle {
        inner: HybridVirtualizer::new(),
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_virtualizer_destroy(handle: *mut VelloTdgVirtualizerHandle) {
    if !handle.is_null() {
        unsafe {
            drop(Box::from_raw(handle));
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_virtualizer_clear(handle: *mut VelloTdgVirtualizerHandle) {
    if let Some(virtualizer) = unsafe { handle.as_mut() } {
        virtualizer.inner.clear();
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_virtualizer_set_rows(
    handle: *mut VelloTdgVirtualizerHandle,
    metrics_ptr: *const VelloTdgRowMetric,
    metrics_len: usize,
) -> bool {
    clear_last_error();
    let Some(virtualizer) = (unsafe { handle.as_mut() }) else {
        set_last_error("null virtualizer handle passed to set_rows");
        return false;
    };

    let rows = if metrics_len == 0 {
        Vec::new()
    } else if metrics_ptr.is_null() {
        set_last_error("null metrics pointer passed to set_rows");
        return false;
    } else {
        unsafe { slice::from_raw_parts(metrics_ptr, metrics_len) }
            .iter()
            .map(|metric| (NodeId(metric.node_id), metric.height.max(0.0)))
            .collect()
    };

    virtualizer.inner.set_rows(&rows);
    true
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_virtualizer_set_columns(
    handle: *mut VelloTdgVirtualizerHandle,
    columns_ptr: *const VelloTdgColumnMetric,
    columns_len: usize,
) -> bool {
    clear_last_error();
    let Some(virtualizer) = (unsafe { handle.as_mut() }) else {
        set_last_error("null virtualizer handle passed to set_columns");
        return false;
    };

    let columns = if columns_len == 0 {
        Vec::new()
    } else if columns_ptr.is_null() {
        set_last_error("null columns pointer passed to set_columns");
        return false;
    } else {
        unsafe { slice::from_raw_parts(columns_ptr, columns_len) }
            .iter()
            .map(|column| ColumnStrip {
                offset: column.offset,
                width: column.width,
                frozen: column.frozen.into(),
                key: column.key,
            })
            .collect()
    };

    virtualizer.inner.set_columns(&columns);
    true
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_virtualizer_plan(
    handle: *mut VelloTdgVirtualizerHandle,
    metrics: VelloTdgViewportMetrics,
    out_slice: *mut VelloTdgColumnSlice,
) -> bool {
    clear_last_error();
    let Some(virtualizer) = (unsafe { handle.as_mut() }) else {
        set_last_error("null virtualizer handle passed to plan");
        return false;
    };

    let row_metrics = RowViewportMetrics {
        scroll_offset: metrics.row_scroll_offset,
        viewport_height: metrics.row_viewport_height,
        overscan: metrics.row_overscan.max(0.0),
    };

    let column_metrics = ColumnViewportMetrics {
        scroll_offset: metrics.column_scroll_offset,
        viewport_width: metrics.column_viewport_width,
        overscan: metrics.column_overscan.max(0.0),
    };

    virtualizer.inner.plan(row_metrics, column_metrics);

    if !out_slice.is_null() {
        unsafe {
            ptr::write(
                out_slice,
                VelloTdgColumnSlice::from(virtualizer.inner.column_slice()),
            );
        }
    }

    true
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_virtualizer_copy_plan(
    handle: *mut VelloTdgVirtualizerHandle,
    out_ptr: *mut VelloTdgRowPlanEntry,
    out_len: usize,
) -> usize {
    let Some(virtualizer) = (unsafe { handle.as_mut() }) else {
        return 0;
    };
    let plan = virtualizer.inner.row_plan();
    if out_ptr.is_null() || out_len == 0 {
        return plan.len();
    }

    let count = plan.len().min(out_len);
    let target = unsafe { slice::from_raw_parts_mut(out_ptr, count) };
    for (idx, entry) in plan.iter().take(count).enumerate() {
        target[idx] = VelloTdgRowPlanEntry::from(entry);
    }
    count
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_virtualizer_copy_recycle(
    handle: *mut VelloTdgVirtualizerHandle,
    out_ptr: *mut VelloTdgRowPlanEntry,
    out_len: usize,
) -> usize {
    let Some(virtualizer) = (unsafe { handle.as_mut() }) else {
        return 0;
    };
    let plan = virtualizer.inner.recycle_plan();
    if out_ptr.is_null() || out_len == 0 {
        return plan.len();
    }

    let count = plan.len().min(out_len);
    let target = unsafe { slice::from_raw_parts_mut(out_ptr, count) };
    for (idx, entry) in plan.iter().take(count).enumerate() {
        target[idx] = VelloTdgRowPlanEntry::from(entry);
    }
    count
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_virtualizer_window(
    handle: *mut VelloTdgVirtualizerHandle,
    out_window: *mut VelloTdgRowWindow,
) -> bool {
    let Some(virtualizer) = (unsafe { handle.as_mut() }) else {
        return false;
    };
    if out_window.is_null() {
        return false;
    }
    let range = virtualizer.inner.row_window();
    unsafe {
        ptr::write(
            out_window,
            VelloTdgRowWindow {
                start_index: range.start as u32,
                end_index: range.end as u32,
                total_height: virtualizer.inner.total_height(),
            },
        );
    }
    true
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_virtualizer_telemetry(
    handle: *const VelloTdgVirtualizerHandle,
    out_stats: *mut VelloTdgVirtualizerTelemetry,
) -> bool {
    clear_last_error();
    let Some(virtualizer) = (unsafe { handle.as_ref() }) else {
        set_last_error("null virtualizer handle passed to telemetry");
        return false;
    };
    if out_stats.is_null() {
        set_last_error("null output buffer passed to virtualizer telemetry");
        return false;
    }
    let telemetry = VelloTdgVirtualizerTelemetry::from(virtualizer.inner.telemetry());
    unsafe {
        ptr::write(out_stats, telemetry);
    }
    true
}

#[unsafe(no_mangle)]
pub extern "C" fn vello_tdg_renderer_create(
    options: VelloTdgRendererOptions,
) -> *mut VelloTdgRendererHandle {
    clear_last_error();
    Box::into_raw(Box::new(VelloTdgRendererHandle {
        inner: RendererLoop::new(options.into()),
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_renderer_destroy(handle: *mut VelloTdgRendererHandle) {
    if !handle.is_null() {
        unsafe {
            drop(Box::from_raw(handle));
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_renderer_begin_frame(
    handle: *mut VelloTdgRendererHandle,
) -> bool {
    let Some(renderer) = (unsafe { handle.as_mut() }) else {
        return false;
    };
    renderer.inner.begin_frame()
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_renderer_record_gpu_summary(
    handle: *mut VelloTdgRendererHandle,
    summary: VelloTdgGpuTimestampSummary,
) -> bool {
    clear_last_error();
    let Some(renderer) = (unsafe { handle.as_mut() }) else {
        return false;
    };
    renderer.inner.record_gpu_summary(
        summary.gpu_time_ms,
        summary.queue_time_ms,
        summary.sample_count,
    );
    true
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_renderer_end_frame(
    handle: *mut VelloTdgRendererHandle,
    gpu_time_ms: f32,
    queue_time_ms: f32,
    out_stats: *mut VelloTdgFrameStats,
) -> bool {
    let Some(renderer) = (unsafe { handle.as_mut() }) else {
        return false;
    };
    if out_stats.is_null() {
        return false;
    }
    let stats = renderer.inner.end_frame(gpu_time_ms, queue_time_ms);
    unsafe {
        ptr::write(out_stats, VelloTdgFrameStats::from(stats));
    }
    true
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_scene_encode_row(
    cache: *mut SceneGraphCache,
    node_id: u32,
    visual: *const VelloTdgRowVisual,
    columns_ptr: *const VelloTdgColumnPlan,
    columns_len: usize,
) -> bool {
    clear_last_error();
    if cache.is_null() || visual.is_null() {
        set_last_error("null pointer passed to scene_encode_row");
        return false;
    }

    let cache = unsafe { &mut *cache };
    let Some(scene) = cache.scene_mut_by_index(node_id as usize) else {
        set_last_error("invalid scene node id in scene_encode_row");
        return false;
    };

    let columns = if columns_len == 0 {
        Vec::new()
    } else if columns_ptr.is_null() {
        set_last_error("null columns pointer passed to scene_encode_row");
        return false;
    } else {
        unsafe { slice::from_raw_parts(columns_ptr, columns_len) }
            .iter()
            .map(Into::into)
            .collect()
    };

    let visual = RowVisual::from(unsafe { &*visual });
    encode_row(scene, &visual, &columns);
    true
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_scene_encode_group_header(
    cache: *mut SceneGraphCache,
    node_id: u32,
    visual: *const VelloTdgGroupHeaderVisual,
    columns_ptr: *const VelloTdgColumnPlan,
    columns_len: usize,
) -> bool {
    clear_last_error();
    if cache.is_null() || visual.is_null() {
        set_last_error("null pointer passed to scene_encode_group_header");
        return false;
    }

    let cache = unsafe { &mut *cache };
    let Some(scene) = cache.scene_mut_by_index(node_id as usize) else {
        set_last_error("invalid scene node id in scene_encode_group_header");
        return false;
    };

    let columns = if columns_len == 0 {
        Vec::new()
    } else if columns_ptr.is_null() {
        set_last_error("null columns pointer passed to scene_encode_group_header");
        return false;
    } else {
        unsafe { slice::from_raw_parts(columns_ptr, columns_len) }
            .iter()
            .map(Into::into)
            .collect()
    };

    let visual = GroupHeaderVisual::from(unsafe { &*visual });
    encode_group_header(scene, &visual, &columns);
    true
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_scene_encode_summary(
    cache: *mut SceneGraphCache,
    node_id: u32,
    visual: *const VelloTdgSummaryVisual,
    columns_ptr: *const VelloTdgColumnPlan,
    columns_len: usize,
) -> bool {
    clear_last_error();
    if cache.is_null() || visual.is_null() {
        set_last_error("null pointer passed to scene_encode_summary");
        return false;
    }

    let cache = unsafe { &mut *cache };
    let Some(scene) = cache.scene_mut_by_index(node_id as usize) else {
        set_last_error("invalid scene node id in scene_encode_summary");
        return false;
    };

    let columns = if columns_len == 0 {
        Vec::new()
    } else if columns_ptr.is_null() {
        set_last_error("null columns pointer passed to scene_encode_summary");
        return false;
    } else {
        unsafe { slice::from_raw_parts(columns_ptr, columns_len) }
            .iter()
            .map(Into::into)
            .collect()
    };

    let visual = SummaryVisual::from(unsafe { &*visual });
    encode_summary(scene, &visual, &columns);
    true
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_tdg_scene_encode_chrome(
    cache: *mut SceneGraphCache,
    node_id: u32,
    visual: *const VelloTdgRowChromeVisual,
    columns_ptr: *const VelloTdgColumnPlan,
    columns_len: usize,
) -> bool {
    clear_last_error();
    if cache.is_null() || visual.is_null() {
        set_last_error("null pointer passed to scene_encode_chrome");
        return false;
    }

    let cache = unsafe { &mut *cache };
    let Some(scene) = cache.scene_mut_by_index(node_id as usize) else {
        set_last_error("invalid scene node id in scene_encode_chrome");
        return false;
    };

    let columns = if columns_len == 0 {
        Vec::new()
    } else if columns_ptr.is_null() {
        set_last_error("null columns pointer passed to scene_encode_chrome");
        return false;
    } else {
        unsafe { slice::from_raw_parts(columns_ptr, columns_len) }
            .iter()
            .map(Into::into)
            .collect()
    };

    let visual = RowChromeVisual::from(unsafe { &*visual });
    encode_chrome(scene, &visual, &columns);
    true
}
