use std::ops::Range;

use hashbrown::{HashMap, HashSet};

const MIN_BUFFER_RESERVE: usize = 128;
const BUFFER_RETENTION_MULTIPLIER: usize = 6;
const STALE_BUFFER_FRAME_THRESHOLD: u64 = 240;

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct VirtualNodeId(pub u32);

impl From<u32> for VirtualNodeId {
    fn from(value: u32) -> Self {
        Self(value)
    }
}

impl From<VirtualNodeId> for u32 {
    fn from(value: VirtualNodeId) -> Self {
        value.0
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum FrozenKind {
    None,
    Leading,
    Trailing,
}

#[derive(Clone, Copy, Debug)]
pub struct ColumnStrip {
    pub offset: f64,
    pub width: f64,
    pub frozen: FrozenKind,
    pub key: u32,
}

impl ColumnStrip {
    pub fn new(offset: f64, width: f64, frozen: FrozenKind, key: u32) -> Self {
        Self {
            offset,
            width,
            frozen,
            key,
        }
    }
}

#[derive(Clone, Copy, Debug, Default)]
pub struct FrozenColumns {
    pub leading: u32,
    pub trailing: u32,
}

#[derive(Clone, Copy, Debug, Default)]
pub struct RowViewportMetrics {
    pub scroll_offset: f64,
    pub viewport_extent: f64,
    pub overscan: f64,
}

#[derive(Clone, Copy, Debug, Default)]
pub struct ColumnViewportMetrics {
    pub scroll_offset: f64,
    pub viewport_extent: f64,
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
    pub node_id: VirtualNodeId,
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
    node_id: VirtualNodeId,
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
    buffer_map: HashMap<VirtualNodeId, BufferState>,
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
        self.row_plan.clear();
        self.recycle_plan.clear();
        self.row_window = 0..0;
        self.last_window = 0..0;
        self.column_slice = ColumnSlice::default();
        self.buffer_map.clear();
        self.free_buffers.clear();
        self.next_buffer_id = 1;
        self.frame_index = 0;
        self.total_height = 0.0;
        self.telemetry = VirtualizerTelemetry::default();
    }

    pub fn set_rows(&mut self, rows: &[(VirtualNodeId, f64)]) {
        self.rows.clear();
        self.rows.reserve(rows.len());
        let mut cursor = 0.0;
        for (node_id, height) in rows {
            let height = height.max(0.0) as f32;
            let top = cursor;
            cursor += height as f64;
            self.rows.push(RowMetric {
                node_id: *node_id,
                height,
                top,
                bottom: cursor,
            });
        }
        self.total_height = cursor;
        self.telemetry.rows_total = rows.len() as u32;
    }

    pub fn set_columns(&mut self, columns: &[ColumnStrip]) {
        self.columns.clear();
        self.columns.reserve(columns.len());
        for (index, strip) in columns.iter().enumerate() {
            self.columns.push(ColumnMetric {
                strip: *strip,
                index: index as u32,
            });
        }
    }

    pub fn plan(&mut self, row_metrics: RowViewportMetrics, column_metrics: ColumnViewportMetrics) {
        self.frame_index = self.frame_index.wrapping_add(1);
        self.telemetry.window_len = 0;
        self.telemetry.reused = 0;
        self.telemetry.adopted = 0;
        self.telemetry.allocated = 0;
        self.telemetry.recycled = 0;
        self.column_slice = ColumnSlice::default();
        self.row_plan.clear();
        self.recycle_plan.clear();

        let row_window = self.compute_row_window(&row_metrics);
        self.emit_recycle_plan(&row_window);
        self.emit_row_plan(&row_window);
        self.trim_stale_buffers();
        self.last_window = row_window.clone();
        self.row_window = row_window;
        self.telemetry.window_len = self.row_window.len() as u32;

        self.column_slice = self.compute_column_slice(&column_metrics);

        self.telemetry.active_buffers = self.buffer_map.len() as u32;
        self.telemetry.free_buffers = self.free_buffers.len() as u32;
    }

    pub fn row_plan(&self) -> &[RowPlanEntry] {
        &self.row_plan
    }

    pub fn recycle_plan(&self) -> &[RowPlanEntry] {
        &self.recycle_plan
    }

    pub fn row_window(&self) -> &Range<usize> {
        &self.row_window
    }

    pub fn total_height(&self) -> f64 {
        self.total_height
    }

    pub fn column_slice(&self) -> ColumnSlice {
        self.column_slice
    }

    pub fn telemetry(&self) -> VirtualizerTelemetry {
        self.telemetry
    }

    fn compute_row_window(&self, metrics: &RowViewportMetrics) -> Range<usize> {
        if self.rows.is_empty() {
            return 0..0;
        }

        let viewport_start = metrics.scroll_offset.max(0.0);
        let viewport_end = viewport_start + metrics.viewport_extent.max(0.0);
        let overscan = metrics.overscan.max(0.0);

        let target_start = viewport_start - overscan;
        let target_end = viewport_end + overscan;

        let start_index = self
            .rows
            .binary_search_by(|metric| {
                if metric.bottom < target_start {
                    std::cmp::Ordering::Less
                } else if metric.top > target_start {
                    std::cmp::Ordering::Greater
                } else {
                    std::cmp::Ordering::Equal
                }
            })
            .unwrap_or_else(|index| index);

        let mut end_index = start_index;
        while end_index < self.rows.len() && self.rows[end_index].top < target_end {
            end_index += 1;
        }

        start_index.min(self.rows.len())..end_index.min(self.rows.len())
    }

    fn compute_column_slice(&self, metrics: &ColumnViewportMetrics) -> ColumnSlice {
        if self.columns.is_empty() {
            return ColumnSlice::default();
        }

        let viewport_start = metrics.scroll_offset.max(0.0);
        let viewport_end = viewport_start + metrics.viewport_extent.max(0.0);
        let overscan = metrics.overscan.max(0.0);

        let mut slice = ColumnSlice::default();
        let mut primary_indices = HashSet::new();

        for metric in &self.columns {
            let offset = metric.strip.offset;
            let extent = metric.strip.offset + metric.strip.width;
            let frozen = metric.strip.frozen;

            match frozen {
                FrozenKind::Leading => {
                    slice.frozen_leading = slice.frozen_leading.saturating_add(1)
                }
                FrozenKind::Trailing => {
                    slice.frozen_trailing = slice.frozen_trailing.saturating_add(1)
                }
                FrozenKind::None => {}
            }

            if offset <= viewport_end + overscan && extent >= viewport_start - overscan {
                primary_indices.insert(metric.index);
            }
        }

        if !primary_indices.is_empty() {
            let min_index = *primary_indices.iter().min().unwrap();
            let max_index = *primary_indices.iter().max().unwrap();
            slice.primary_start = min_index;
            slice.primary_count = max_index.saturating_sub(min_index) + 1;
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

    fn next_buffer(&mut self, node_id: VirtualNodeId) -> (u32, RowAction) {
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

    fn trim_stale_buffers(&mut self) {
        let max_free = self
            .row_window
            .len()
            .max(MIN_BUFFER_RESERVE)
            .saturating_mul(BUFFER_RETENTION_MULTIPLIER);
        if self.free_buffers.len() <= max_free {
            return;
        }

        self.free_buffers
            .sort_by_key(|buffer| buffer.last_used_frame);
        while self.free_buffers.len() > max_free {
            self.free_buffers.pop();
            self.telemetry.evicted = self.telemetry.evicted.saturating_add(1);
        }

        self.free_buffers.retain(|buffer| {
            self.frame_index.saturating_sub(buffer.last_used_frame) <= STALE_BUFFER_FRAME_THRESHOLD
        });
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn computes_row_window_with_overscan() {
        let mut virtualizer = HybridVirtualizer::new();
        let rows = vec![
            (VirtualNodeId(0), 20.0),
            (VirtualNodeId(1), 20.0),
            (VirtualNodeId(2), 20.0),
            (VirtualNodeId(3), 20.0),
        ];
        virtualizer.set_rows(&rows);
        let row_metrics = RowViewportMetrics {
            scroll_offset: 10.0,
            viewport_extent: 30.0,
            overscan: 5.0,
        };
        let column_metrics = ColumnViewportMetrics::default();

        virtualizer.plan(row_metrics, column_metrics);
        let window = virtualizer.row_window();
        assert_eq!(window.start, 0);
        assert!(window.end >= 3);
    }
}
