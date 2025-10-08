use std::ops::Range;

use hashbrown::{HashMap, HashSet};

use crate::data_model::NodeId;
use crate::types::{ColumnStrip, FrozenKind};

const MIN_BUFFER_RESERVE: usize = 128;
const BUFFER_RETENTION_MULTIPLIER: usize = 6;
const STALE_BUFFER_FRAME_THRESHOLD: u64 = 240;

#[derive(Clone, Copy, Debug, Default)]
pub struct RowViewportMetrics {
    pub scroll_offset: f64,
    pub viewport_height: f64,
    pub overscan: f64,
}

#[derive(Clone, Copy, Debug, Default)]
pub struct ColumnViewportMetrics {
    pub scroll_offset: f64,
    pub viewport_width: f64,
    pub overscan: f64,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum RowAction {
    Reuse,
    Adopt,
    Allocate,
    Recycle,
}

#[derive(Clone, Copy, Debug)]
pub struct RowPlanEntry {
    pub node_id: NodeId,
    pub buffer_id: u32,
    pub top: f64,
    pub height: f32,
    pub action: RowAction,
}

#[derive(Clone, Copy, Debug)]
pub struct ColumnSlice {
    pub primary_start: u32,
    pub primary_count: u32,
    pub frozen_leading: u32,
    pub frozen_trailing: u32,
}

impl Default for ColumnSlice {
    fn default() -> Self {
        Self {
            primary_start: 0,
            primary_count: 0,
            frozen_leading: 0,
            frozen_trailing: 0,
        }
    }
}

#[derive(Clone, Copy, Debug)]
struct RowMetric {
    node_id: NodeId,
    height: f32,
    top: f64,
    bottom: f64,
}

#[derive(Clone, Copy, Debug)]
struct ColumnMetric {
    strip: ColumnStrip,
    index: u32,
}

#[derive(Clone, Copy, Debug)]
struct BufferState {
    id: u32,
    last_used_frame: u64,
}

#[derive(Clone, Copy, Debug, Default)]
pub struct VirtualizerTelemetry {
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

pub struct HybridVirtualizer {
    rows: Vec<RowMetric>,
    columns: Vec<ColumnMetric>,
    row_window: Range<usize>,
    last_window: Range<usize>,
    row_plan: Vec<RowPlanEntry>,
    recycle_plan: Vec<RowPlanEntry>,
    column_slice: ColumnSlice,
    buffer_map: HashMap<NodeId, BufferState>,
    free_buffers: Vec<BufferState>,
    next_buffer_id: u32,
    frame_index: u64,
    total_height: f64,
    telemetry: VirtualizerTelemetry,
}

impl Default for HybridVirtualizer {
    fn default() -> Self {
        Self::new()
    }
}

impl HybridVirtualizer {
    pub fn new() -> Self {
        Self {
            rows: Vec::new(),
            columns: Vec::new(),
            row_window: 0..0,
            last_window: 0..0,
            row_plan: Vec::new(),
            recycle_plan: Vec::new(),
            column_slice: ColumnSlice::default(),
            buffer_map: HashMap::new(),
            free_buffers: Vec::new(),
            next_buffer_id: 1,
            frame_index: 0,
            total_height: 0.0,
            telemetry: VirtualizerTelemetry::default(),
        }
    }

    pub fn clear(&mut self) {
        self.rows.clear();
        self.columns.clear();
        self.row_window = 0..0;
        self.last_window = 0..0;
        self.row_plan.clear();
        self.recycle_plan.clear();
        self.column_slice = ColumnSlice::default();
        self.buffer_map.clear();
        self.free_buffers.clear();
        self.next_buffer_id = 1;
        self.frame_index = 0;
        self.total_height = 0.0;
        self.telemetry = VirtualizerTelemetry::default();
    }

    pub fn set_rows(&mut self, metrics: &[(NodeId, f32)]) {
        self.rows.clear();
        self.row_plan.clear();
        self.recycle_plan.clear();
        self.total_height = 0.0;
        let mut offset = 0.0;
        for &(node_id, height) in metrics {
            let clamped_height = height.max(0.0);
            let top = offset;
            offset += clamped_height as f64;
            let bottom = offset;
            self.rows.push(RowMetric {
                node_id,
                height: clamped_height,
                top,
                bottom,
            });
        }
        self.total_height = offset;
        self.last_window = 0..0;

        let valid: HashSet<NodeId> = self.rows.iter().map(|row| row.node_id).collect();
        self.buffer_map.retain(|node_id, state| {
            if valid.contains(node_id) {
                true
            } else {
                self.free_buffers.push(*state);
                false
            }
        });
        self.telemetry.rows_total = self.rows.len() as u32;
        self.telemetry.active_buffers = self.buffer_map.len() as u32;
        self.telemetry.free_buffers = self.free_buffers.len() as u32;
        self.telemetry.window_len = 0;
        self.telemetry.reused = 0;
        self.telemetry.adopted = 0;
        self.telemetry.allocated = 0;
        self.telemetry.recycled = 0;
        self.telemetry.evicted = 0;
    }

    pub fn set_columns(&mut self, columns: &[ColumnStrip]) {
        self.columns.clear();
        for (index, strip) in columns.iter().enumerate() {
            self.columns.push(ColumnMetric {
                strip: *strip,
                index: index as u32,
            });
        }
    }

    pub fn plan(
        &mut self,
        row_viewport: RowViewportMetrics,
        column_viewport: ColumnViewportMetrics,
    ) {
        self.row_plan.clear();
        self.recycle_plan.clear();
        self.telemetry.window_len = 0;
        self.telemetry.reused = 0;
        self.telemetry.adopted = 0;
        self.telemetry.allocated = 0;
        self.telemetry.recycled = 0;
        self.telemetry.evicted = 0;
        self.telemetry.rows_total = self.rows.len() as u32;
        if self.rows.is_empty() {
            self.row_window = 0..0;
            self.column_slice = ColumnSlice::default();
            self.last_window = 0..0;
            self.telemetry.active_buffers = self.buffer_map.len() as u32;
            self.telemetry.free_buffers = self.free_buffers.len() as u32;
            return;
        }

        let new_window = self.compute_row_window(row_viewport);
        self.telemetry.window_len = new_window.len() as u32;
        self.column_slice = self.compute_column_slice(column_viewport);
        self.emit_recycle_plan(&new_window);
        self.emit_row_plan(&new_window);
        self.last_window = self.row_window.clone();
        self.row_window = new_window;
        self.frame_index = self.frame_index.wrapping_add(1);
        let window_len = self.row_window.len();
        self.prune_free_buffers(window_len);
        self.telemetry.active_buffers = self.buffer_map.len() as u32;
        self.telemetry.free_buffers = self.free_buffers.len() as u32;
    }

    pub fn row_plan(&self) -> &[RowPlanEntry] {
        &self.row_plan
    }

    pub fn recycle_plan(&self) -> &[RowPlanEntry] {
        &self.recycle_plan
    }

    pub fn column_slice(&self) -> ColumnSlice {
        self.column_slice
    }

    pub fn row_window(&self) -> Range<usize> {
        self.row_window.clone()
    }

    pub fn total_height(&self) -> f64 {
        self.total_height
    }

    pub fn telemetry(&self) -> VirtualizerTelemetry {
        self.telemetry
    }

    fn prune_free_buffers(&mut self, window_len: usize) {
        if self.free_buffers.is_empty() {
            self.telemetry.evicted = 0;
            return;
        }

        let mut evicted: u32 = 0;
        let frame_index = self.frame_index;

        self.free_buffers.retain(|buffer| {
            let stale =
                frame_index.saturating_sub(buffer.last_used_frame) > STALE_BUFFER_FRAME_THRESHOLD;
            if stale {
                evicted = evicted.saturating_add(1);
                false
            } else {
                true
            }
        });

        let target = window_len
            .saturating_mul(BUFFER_RETENTION_MULTIPLIER)
            .max(MIN_BUFFER_RESERVE);

        if self.free_buffers.len() > target {
            self.free_buffers
                .sort_by(|a, b| b.last_used_frame.cmp(&a.last_used_frame));
            let remove = self.free_buffers.len() - target;
            if remove > 0 {
                self.free_buffers.truncate(target);
                evicted = evicted.saturating_add(remove as u32);
            }
        }

        self.telemetry.evicted = evicted;
        self.telemetry.free_buffers = self.free_buffers.len() as u32;
    }

    fn compute_row_window(&self, metrics: RowViewportMetrics) -> Range<usize> {
        if self.rows.is_empty() {
            return 0..0;
        }

        let viewport_start = metrics.scroll_offset.max(0.0) - metrics.overscan;
        let viewport_end = (metrics.scroll_offset + metrics.viewport_height + metrics.overscan)
            .max(viewport_start);

        let mut start_index = 0;
        let mut end_index = self.rows.len();

        for (idx, row) in self.rows.iter().enumerate() {
            if row.bottom < viewport_start {
                continue;
            }
            start_index = idx;
            break;
        }

        for (idx, row) in self.rows.iter().enumerate().skip(start_index) {
            if row.top > viewport_end {
                end_index = idx;
                break;
            }
        }

        start_index..end_index
    }

    fn compute_column_slice(&self, metrics: ColumnViewportMetrics) -> ColumnSlice {
        if self.columns.is_empty() {
            return ColumnSlice::default();
        }

        let mut slice = ColumnSlice::default();
        let mut primary_start: Option<u32> = None;
        let mut primary_end: Option<u32> = None;

        let viewport_start = metrics.scroll_offset - metrics.overscan;
        let viewport_end = metrics.scroll_offset + metrics.viewport_width + metrics.overscan;

        for column in &self.columns {
            match column.strip.frozen {
                FrozenKind::Leading => slice.frozen_leading += 1,
                FrozenKind::Trailing => slice.frozen_trailing += 1,
                FrozenKind::None => {
                    let left = column.strip.offset;
                    let right = column.strip.offset + column.strip.width;
                    if right >= viewport_start && left <= viewport_end {
                        if primary_start.is_none() {
                            primary_start = Some(column.index);
                        }
                        primary_end = Some(column.index);
                    }
                }
            }
        }

        if let Some(start) = primary_start {
            if let Some(end) = primary_end {
                slice.primary_start = start;
                slice.primary_count = end.saturating_sub(start) + 1;
            }
        }

        slice
    }

    fn emit_recycle_plan(&mut self, new_window: &Range<usize>) {
        for index in self.last_window.clone() {
            if !new_window.contains(&index) {
                if let Some(row) = self.rows.get(index).copied() {
                    if let Some(buffer) = self.buffer_map.remove(&row.node_id) {
                        self.recycle_plan.push(RowPlanEntry {
                            node_id: row.node_id,
                            buffer_id: buffer.id,
                            top: row.top,
                            height: row.height,
                            action: RowAction::Recycle,
                        });
                        self.free_buffers.push(BufferState {
                            id: buffer.id,
                            last_used_frame: self.frame_index,
                        });
                        self.telemetry.recycled = self.telemetry.recycled.saturating_add(1);
                    }
                }
            }
        }
    }

    fn emit_row_plan(&mut self, new_window: &Range<usize>) {
        for index in new_window.clone() {
            if let Some(row) = self.rows.get(index).copied() {
                if let Some(buffer) = self.buffer_map.get_mut(&row.node_id) {
                    buffer.last_used_frame = self.frame_index;
                    self.row_plan.push(RowPlanEntry {
                        node_id: row.node_id,
                        buffer_id: buffer.id,
                        top: row.top,
                        height: row.height,
                        action: RowAction::Reuse,
                    });
                    self.telemetry.reused = self.telemetry.reused.saturating_add(1);
                } else {
                    let (buffer_id, action) = self.next_buffer(row.node_id);
                    self.row_plan.push(RowPlanEntry {
                        node_id: row.node_id,
                        buffer_id,
                        top: row.top,
                        height: row.height,
                        action,
                    });
                    match action {
                        RowAction::Adopt => {
                            self.telemetry.adopted = self.telemetry.adopted.saturating_add(1);
                        }
                        RowAction::Allocate => {
                            self.telemetry.allocated = self.telemetry.allocated.saturating_add(1);
                        }
                        RowAction::Reuse => {
                            self.telemetry.reused = self.telemetry.reused.saturating_add(1);
                        }
                        RowAction::Recycle => {
                            self.telemetry.recycled = self.telemetry.recycled.saturating_add(1);
                        }
                    }
                }
            }
        }
    }

    fn next_buffer(&mut self, node_id: NodeId) -> (u32, RowAction) {
        if let Some(mut buffer) = self.free_buffers.pop() {
            buffer.last_used_frame = self.frame_index;
            let id = buffer.id;
            self.buffer_map.insert(node_id, buffer);
            (id, RowAction::Adopt)
        } else {
            let id = self.next_buffer_id;
            self.next_buffer_id = self.next_buffer_id.wrapping_add(1).max(1);
            let state = BufferState {
                id,
                last_used_frame: self.frame_index,
            };
            self.buffer_map.insert(node_id, state);
            (id, RowAction::Allocate)
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::data_model::NodeId;

    #[test]
    fn computes_row_window_with_overscan() {
        let mut virtualizer = HybridVirtualizer::new();
        let rows = vec![
            (NodeId(0), 20.0),
            (NodeId(1), 20.0),
            (NodeId(2), 20.0),
            (NodeId(3), 20.0),
        ];
        virtualizer.set_rows(&rows);
        let row_metrics = RowViewportMetrics {
            scroll_offset: 10.0,
            viewport_height: 30.0,
            overscan: 5.0,
        };
        let column_metrics = ColumnViewportMetrics::default();

        virtualizer.plan(row_metrics, column_metrics);
        let window = virtualizer.row_window();
        assert_eq!(window.start, 0);
        assert!(window.end >= 3);
    }
}
