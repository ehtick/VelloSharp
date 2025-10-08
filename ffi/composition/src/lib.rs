#![cfg_attr(not(test), deny(clippy::all))]
#![allow(clippy::missing_docs_in_private_items)]
#![allow(clippy::too_many_arguments)]

mod animation;
mod constraints;
mod interop;
mod layout;
mod linear_layout;
mod materials;
mod panels;
mod scene_cache;
mod text;
mod virtualization;

pub use animation::{
    DirtyIntent, EasingFunction, EasingTrackDescriptor, RepeatMode, SAMPLE_FLAG_ACTIVE,
    SAMPLE_FLAG_AT_REST, SAMPLE_FLAG_COMPLETED, SAMPLE_FLAG_LOOPED, SAMPLE_FLAG_PINGPONG_REVERSED,
    SpringTrackDescriptor, TimelineGroupConfig, TimelineGroupId, TimelineSample, TimelineSystem,
    TimelineTrackId,
};
pub use constraints::{LayoutConstraints, LayoutSize, ScalarConstraint};
pub use interop::{
    CompositionDirtyRegion, CompositionLabelMetrics, CompositionLinearLayoutItem,
    CompositionLinearLayoutSlot, CompositionPlotArea, vello_composition_compute_plot_area,
    vello_composition_measure_label, vello_composition_scene_cache_clear,
    vello_composition_scene_cache_create, vello_composition_scene_cache_create_node,
    vello_composition_scene_cache_destroy, vello_composition_scene_cache_dispose_node,
    vello_composition_scene_cache_mark_dirty, vello_composition_scene_cache_mark_dirty_bounds,
    vello_composition_scene_cache_take_dirty, vello_composition_solve_linear_layout,
};
pub use layout::{
    AxisLayout, AxisTick, MIN_PLOT_DIMENSION, PlotArea, compute_axis_layout, compute_plot_area,
};
pub use linear_layout::{LinearLayoutItem, LinearLayoutSlot, solve_linear_layout};
pub use materials::{
    CompositionColor, CompositionMaterialDescriptor, CompositionShaderDescriptor,
    CompositionShaderKind, register_material, register_shader, resolve_material_color,
    resolve_material_peniko_color, unregister_material, unregister_shader,
};
pub use panels::{
    DockLayoutChild, DockLayoutOptions, DockSide, GridLayoutChild, GridLayoutOptions, GridTrack,
    GridTrackKind, LayoutAlignment, LayoutOrientation, LayoutRect, LayoutThickness,
    StackLayoutChild, StackLayoutOptions, WrapLayoutChild, WrapLayoutLine, WrapLayoutOptions,
    WrapLayoutResult, solve_dock_layout, solve_grid_layout, solve_stack_layout, solve_wrap_layout,
};
pub use scene_cache::{DirtyRegion, SceneGraphCache, SceneNodeId};
pub use text::{LabelLayout, TextShaper, label_font, layout_label};
pub use virtualization::{
    ColumnSlice, ColumnStrip, ColumnViewportMetrics, FrozenColumns, FrozenKind, HybridVirtualizer,
    RowAction, RowPlanEntry, RowViewportMetrics, VirtualNodeId, VirtualizerTelemetry,
};

pub mod ffi {
    pub use crate::interop::{
        CompositionColumnSlice, CompositionColumnViewportMetrics, CompositionDirtyRegion,
        CompositionDockLayoutChild, CompositionDockLayoutOptions, CompositionDockSide,
        CompositionFrozenKind, CompositionGridLayoutChild, CompositionGridLayoutOptions,
        CompositionGridTrack, CompositionGridTrackKind, CompositionLabelMetrics,
        CompositionLayoutAlignment, CompositionLayoutConstraints, CompositionLayoutOrientation,
        CompositionLayoutRect, CompositionLayoutThickness, CompositionLinearLayoutItem,
        CompositionLinearLayoutSlot, CompositionPlotArea, CompositionRowAction,
        CompositionRowPlanEntry, CompositionRowViewportMetrics, CompositionRowWindow,
        CompositionScalarConstraint, CompositionStackLayoutChild, CompositionStackLayoutOptions,
        CompositionTimelineDirtyBinding, CompositionTimelineDirtyKind, CompositionTimelineEasing,
        CompositionTimelineEasingTrackDesc, CompositionTimelineGroupConfig,
        CompositionTimelineRepeat, CompositionTimelineSample, CompositionTimelineSpringTrackDesc,
        CompositionVirtualColumnStrip, CompositionVirtualRowMetric,
        CompositionVirtualizerTelemetry, CompositionWrapLayoutChild, CompositionWrapLayoutLine,
        CompositionWrapLayoutOptions, vello_composition_compute_plot_area,
        vello_composition_dock_layout, vello_composition_grid_layout,
        vello_composition_measure_label, vello_composition_scene_cache_clear,
        vello_composition_scene_cache_create, vello_composition_scene_cache_create_node,
        vello_composition_scene_cache_destroy, vello_composition_scene_cache_dispose_node,
        vello_composition_scene_cache_mark_dirty, vello_composition_scene_cache_mark_dirty_bounds,
        vello_composition_scene_cache_take_dirty, vello_composition_solve_linear_layout,
        vello_composition_stack_layout, vello_composition_timeline_add_easing_track,
        vello_composition_timeline_add_spring_track, vello_composition_timeline_group_create,
        vello_composition_timeline_group_destroy, vello_composition_timeline_group_pause,
        vello_composition_timeline_group_play, vello_composition_timeline_group_set_speed,
        vello_composition_timeline_system_create, vello_composition_timeline_system_destroy,
        vello_composition_timeline_tick, vello_composition_timeline_track_remove,
        vello_composition_timeline_track_reset, vello_composition_timeline_track_set_spring_target,
        vello_composition_virtualizer_clear, vello_composition_virtualizer_column_slice,
        vello_composition_virtualizer_copy_plan, vello_composition_virtualizer_copy_recycle,
        vello_composition_virtualizer_create, vello_composition_virtualizer_destroy,
        vello_composition_virtualizer_plan, vello_composition_virtualizer_set_columns,
        vello_composition_virtualizer_set_rows, vello_composition_virtualizer_telemetry,
        vello_composition_virtualizer_window, vello_composition_wrap_layout,
    };
    pub use crate::interop::{
        vello_composition_material_register, vello_composition_material_resolve_color,
        vello_composition_material_unregister, vello_composition_shader_register,
        vello_composition_shader_unregister,
    };
    pub use crate::materials::{
        CompositionColor, CompositionMaterialDescriptor, CompositionShaderDescriptor,
        CompositionShaderKind,
    };
}
