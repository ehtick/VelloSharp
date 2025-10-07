#![cfg_attr(not(test), deny(clippy::all))]
#![allow(clippy::missing_docs_in_private_items)]
#![allow(clippy::too_many_arguments)]

mod constraints;
mod interop;
mod layout;
mod linear_layout;
mod scene_cache;
mod text;

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
pub use scene_cache::{DirtyRegion, SceneGraphCache, SceneNodeId};
pub use text::{LabelLayout, TextShaper, label_font, layout_label};

pub mod ffi {
    pub use crate::interop::{
        CompositionDirtyRegion, CompositionLabelMetrics, CompositionLinearLayoutItem,
        CompositionLinearLayoutSlot, CompositionPlotArea, vello_composition_compute_plot_area,
        vello_composition_measure_label, vello_composition_scene_cache_clear,
        vello_composition_scene_cache_create, vello_composition_scene_cache_create_node,
        vello_composition_scene_cache_destroy, vello_composition_scene_cache_dispose_node,
        vello_composition_scene_cache_mark_dirty, vello_composition_scene_cache_mark_dirty_bounds,
        vello_composition_scene_cache_take_dirty, vello_composition_solve_linear_layout,
    };
}
