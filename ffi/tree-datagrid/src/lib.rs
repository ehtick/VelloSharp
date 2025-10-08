#![cfg_attr(not(test), deny(clippy::all))]
#![allow(clippy::missing_docs_in_private_items)]
#![allow(clippy::too_many_arguments)]

mod color;
mod data_model;
mod error;
mod interop;
mod render_hooks;
mod renderer;
mod scene;
mod templates;
mod types;
mod virtualization;

pub use color::VelloTdgColor;
pub use data_model::{NodeDescriptor, NodeId, RowKind, SelectionMode, TreeDataModel};
pub use render_hooks::{
    MaterialHandle, RenderHookHandle, ShaderHandle, fill_with_material, render_column_hook,
    resolve_column_color,
};
pub use renderer::{RendererLoop, RendererOptions};
pub use scene::{GroupHeaderVisual, RowChromeVisual, RowVisual, SummaryVisual};
pub use types::{ColumnStrip, FrozenColumns};
pub use virtualization::{
    ColumnSlice, ColumnViewportMetrics, HybridVirtualizer, RowPlanEntry, RowViewportMetrics,
    VirtualizerTelemetry,
};

pub mod ffi {
    pub use crate::color::VelloTdgColor;
    pub use crate::interop::{
        VelloTdgColumnMetric, VelloTdgColumnPlan, VelloTdgColumnSlice, VelloTdgFrameStats,
        VelloTdgFrozenKind, VelloTdgGpuTimestampSummary, VelloTdgGroupHeaderVisual,
        VelloTdgModelDiff, VelloTdgModelDiffKind, VelloTdgNodeMetadata, VelloTdgRendererOptions,
        VelloTdgRowAction, VelloTdgRowChromeVisual, VelloTdgRowMetric, VelloTdgRowPlanEntry,
        VelloTdgRowVisual, VelloTdgSelectionDiff, VelloTdgSummaryVisual, VelloTdgViewportMetrics,
        VelloTdgVirtualizerTelemetry, vello_tdg_last_error_message,
        vello_tdg_model_attach_children, vello_tdg_model_attach_roots, vello_tdg_model_clear,
        vello_tdg_model_copy_diffs, vello_tdg_model_copy_selection_diffs, vello_tdg_model_create,
        vello_tdg_model_dequeue_materialization, vello_tdg_model_destroy,
        vello_tdg_model_diff_count, vello_tdg_model_node_metadata, vello_tdg_model_select_range,
        vello_tdg_model_selection_diff_count, vello_tdg_model_set_expanded,
        vello_tdg_model_set_selected, vello_tdg_renderer_begin_frame, vello_tdg_renderer_create,
        vello_tdg_renderer_destroy, vello_tdg_renderer_end_frame,
        vello_tdg_renderer_record_gpu_summary, vello_tdg_scene_encode_chrome,
        vello_tdg_scene_encode_group_header, vello_tdg_scene_encode_row,
        vello_tdg_scene_encode_summary, vello_tdg_virtualizer_clear,
        vello_tdg_virtualizer_copy_plan, vello_tdg_virtualizer_copy_recycle,
        vello_tdg_virtualizer_create, vello_tdg_virtualizer_destroy, vello_tdg_virtualizer_plan,
        vello_tdg_virtualizer_set_columns, vello_tdg_virtualizer_set_rows,
        vello_tdg_virtualizer_telemetry, vello_tdg_virtualizer_window,
    };
    pub use crate::render_hooks::{
        VelloTdgMaterialDescriptor, VelloTdgRenderHookDescriptor, VelloTdgRenderHookKind,
        VelloTdgShaderDescriptor, VelloTdgShaderKind, vello_tdg_material_register,
        vello_tdg_material_unregister, vello_tdg_render_hook_register,
        vello_tdg_render_hook_unregister, vello_tdg_shader_register, vello_tdg_shader_unregister,
    };
    pub use crate::templates::{
        TemplateProgram, VelloTdgTemplateBinding, VelloTdgTemplateInstruction,
        VelloTdgTemplateNodeKind, VelloTdgTemplateOpCode, VelloTdgTemplatePaneKind,
        VelloTdgTemplateValueKind, vello_tdg_template_program_create,
        vello_tdg_template_program_destroy, vello_tdg_template_program_encode_pane,
    };
}
