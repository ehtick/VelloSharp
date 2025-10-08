use std::ffi::c_uchar;
use std::{ptr, slice, str};

use crate::animation::{
    DirtyIntent, EasingFunction, EasingTrackDescriptor, RepeatMode, SpringTrackDescriptor,
    TimelineGroupConfig, TimelineSample, TimelineSystem,
};
use crate::constraints::ScalarConstraint;
use crate::layout::{self, PlotArea};
use crate::linear_layout::{self, LinearLayoutItem};
use crate::materials::{
    CompositionColor, CompositionMaterialDescriptor, CompositionShaderDescriptor,
    register_material, register_shader, resolve_material_color, unregister_material,
    unregister_shader,
};
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
