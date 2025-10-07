use std::ffi::c_uchar;
use std::{ptr, slice, str};

use crate::constraints::ScalarConstraint;
use crate::layout::{self, PlotArea};
use crate::linear_layout::{self, LinearLayoutItem};
use crate::scene_cache::{DirtyRegion, SceneGraphCache, SceneNodeId};
use crate::text;

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
#[derive(Debug, Clone, Copy, Default)]
pub struct CompositionDirtyRegion {
    pub min_x: f64,
    pub max_x: f64,
    pub min_y: f64,
    pub max_y: f64,
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
