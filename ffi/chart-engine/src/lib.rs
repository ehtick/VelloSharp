//! Core rendering engine and FFI surface for VelloSharp real-time charts.

#![cfg_attr(not(test), deny(clippy::all))]
#![allow(clippy::missing_docs_in_private_items)]
#![allow(clippy::too_many_arguments)]

use std::{
    cell::RefCell,
    ffi::{CString, c_char},
    fmt, slice, str,
    sync::{Arc, OnceLock, RwLock},
    time::{Instant, SystemTime},
};

use hashbrown::{HashMap, hash_map::Entry};
use once_cell::sync::Lazy;
use skrifa::raw::{FileRef, FontRef};
use skrifa::{
    MetadataProvider,
    instance::{Location, Size},
};
use time::OffsetDateTime;
use tracing::Level;
use tracing_subscriber::{
    Layer, Registry, layer::Context, layer::SubscriberExt, registry::LookupSpan,
};
use vello::{
    Glyph, Scene,
    kurbo::{Affine, BezPath, Circle, Line, Point, Rect, RoundedRect, Stroke},
    peniko::{Blob, Brush, Color, ColorStop, Fill, FontData, Gradient, color::DynamicColor},
};
use vello_chart_diagnostics::{DiagnosticsCollector, FrameStats};

const MIN_VISIBLE_DURATION_SECS: f64 = 1e-6;
const SCENE_BUFFER_COUNT: usize = 2;
const DEFAULT_STROKE_WIDTH: f64 = 1.5;
const MIN_STROKE_WIDTH: f64 = 0.1;
const DEFAULT_PALETTE: &[(u8, u8, u8)] = &[
    (0x3A, 0xB8, 0xFF),
    (0xF4, 0x5E, 0x8C),
    (0x81, 0xFF, 0xF9),
    (0xFF, 0xD1, 0x4F),
    (0xA3, 0x7D, 0xFF),
];
const SERIES_OVERRIDE_FLAG_LABEL_SET: u32 = 1 << 0;
const SERIES_OVERRIDE_FLAG_LABEL_CLEAR: u32 = 1 << 1;
const SERIES_OVERRIDE_FLAG_STROKE_SET: u32 = 1 << 2;
const SERIES_OVERRIDE_FLAG_STROKE_CLEAR: u32 = 1 << 3;
const SERIES_OVERRIDE_FLAG_COLOR_SET: u32 = 1 << 4;
const SERIES_OVERRIDE_FLAG_COLOR_CLEAR: u32 = 1 << 5;

const SERIES_DEFINITION_FLAG_BASELINE_SET: u32 = 1 << 0;
const SERIES_DEFINITION_FLAG_FILL_OPACITY_SET: u32 = 1 << 1;
const SERIES_DEFINITION_FLAG_STROKE_WIDTH_SET: u32 = 1 << 2;
const SERIES_DEFINITION_FLAG_MARKER_SIZE_SET: u32 = 1 << 3;
const SERIES_DEFINITION_FLAG_BAR_WIDTH_SET: u32 = 1 << 4;

const LABEL_FONT_BYTES: &[u8] =
    include_bytes!("../../../extern/vello/examples/assets/roboto/Roboto-Regular.ttf");
const LABEL_FONT_SIZE: f32 = 14.0;
const LABEL_HORIZONTAL_PADDING: f64 = 6.0;
const LABEL_VERTICAL_PADDING: f64 = 4.0;
const LABEL_CORNER_RADIUS: f64 = 6.0;
const LABEL_HORIZONTAL_OFFSET: f64 = 12.0;
const LABEL_VERTICAL_GAP: f64 = 8.0;

static LABEL_FONT: Lazy<FontData> =
    Lazy::new(|| FontData::new(Blob::new(Arc::new(LABEL_FONT_BYTES)), 0));

const PLOT_LEFT_MARGIN_RATIO: f64 = 0.12;
const PLOT_RIGHT_MARGIN_RATIO: f64 = 0.04;
const PLOT_TOP_MARGIN_RATIO: f64 = 0.08;
const PLOT_BOTTOM_MARGIN_RATIO: f64 = 0.12;
const PLOT_LEFT_MARGIN_MIN: f64 = 48.0;
const PLOT_BOTTOM_MARGIN_MIN: f64 = 32.0;
const MIN_PLOT_DIMENSION: f64 = 32.0;
const AXIS_FONT_SIZE: f32 = 12.0;
const AXIS_LABEL_LEFT_MARGIN: f64 = 10.0;
const AXIS_LABEL_BOTTOM_MARGIN: f64 = 18.0;
const GRID_STROKE_WIDTH: f64 = 1.0;
const AXIS_STROKE_WIDTH: f64 = 1.0;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum SeriesKind {
    Line,
    Area,
    Scatter,
    Bar,
}

impl Default for SeriesKind {
    fn default() -> Self {
        SeriesKind::Line
    }
}

#[derive(Debug, Clone)]
struct SeriesDefinition {
    kind: SeriesKind,
    baseline: Option<f64>,
    fill_opacity: Option<f32>,
    stroke_width: Option<f64>,
    marker_size: Option<f64>,
    bar_width_seconds: Option<f64>,
}

impl Default for SeriesDefinition {
    fn default() -> Self {
        Self {
            kind: SeriesKind::Line,
            baseline: None,
            fill_opacity: Some(0.35),
            stroke_width: None,
            marker_size: Some(4.0),
            bar_width_seconds: None,
        }
    }
}

impl SeriesDefinition {
    fn merge(&mut self, other: &SeriesDefinition) {
        self.kind = other.kind;
        if other.baseline.is_some() {
            self.baseline = other.baseline;
        }
        if other.fill_opacity.is_some() {
            self.fill_opacity = other.fill_opacity;
        }
        if other.stroke_width.is_some() {
            self.stroke_width = other.stroke_width;
        }
        if other.marker_size.is_some() {
            self.marker_size = other.marker_size;
        }
        if other.bar_width_seconds.is_some() {
            self.bar_width_seconds = other.bar_width_seconds;
        }
    }
}

fn series_kind_from_u32(value: u32) -> Result<SeriesKind, VelloChartEngineStatus> {
    match value {
        0 => Ok(SeriesKind::Line),
        1 => Ok(SeriesKind::Area),
        2 => Ok(SeriesKind::Scatter),
        3 => Ok(SeriesKind::Bar),
        _ => {
            set_last_error("Unknown series kind value");
            Err(VelloChartEngineStatus::InvalidArgument)
        }
    }
}

fn series_kind_to_u32(kind: SeriesKind) -> u32 {
    match kind {
        SeriesKind::Line => 0,
        SeriesKind::Area => 1,
        SeriesKind::Scatter => 2,
        SeriesKind::Bar => 3,
    }
}

/// Engine tuning parameters.
#[derive(Debug, Clone)]
pub struct EngineOptions {
    pub visible_duration_seconds: f64,
    pub vertical_padding_ratio: f64,
    pub stroke_width: f64,
    pub palette: Vec<Color>,
    pub show_axes: bool,
}

impl Default for EngineOptions {
    fn default() -> Self {
        Self {
            visible_duration_seconds: 120.0,
            vertical_padding_ratio: 0.08,
            stroke_width: DEFAULT_STROKE_WIDTH,
            palette: Self::default_palette(),
            show_axes: true,
        }
    }
}

impl EngineOptions {
    fn default_palette() -> Vec<Color> {
        DEFAULT_PALETTE
            .iter()
            .map(|(r, g, b)| Color::from_rgba8(*r, *g, *b, 0xFF))
            .collect()
    }
}

/// Streaming sample ingested by the engine.
#[derive(Debug, Clone, Copy)]
pub struct ChartSample {
    pub series_id: u32,
    pub timestamp_seconds: f64,
    pub value: f64,
}

/// Borrowed frame returned from the engine.
pub struct EngineFrame<'a> {
    pub version: u64,
    pub scene: &'a Scene,
    pub stats: FrameStats,
}

/// Rendering engine maintaining per-series state and double-buffered scenes.
pub struct ChartEngine {
    options: EngineOptions,
    diagnostics: DiagnosticsCollector,
    series_definitions: HashMap<u32, SeriesDefinition>,
    series: HashMap<u32, SeriesState>,
    next_palette_slot: usize,
    frame_buffers: [FrameBuffer; SCENE_BUFFER_COUNT],
    next_buffer: usize,
    frame_counter: u64,
    last_buffer: usize,
}

impl ChartEngine {
    pub fn new(options: EngineOptions) -> Self {
        Self {
            options,
            diagnostics: DiagnosticsCollector::default(),
            series_definitions: HashMap::new(),
            series: HashMap::new(),
            next_palette_slot: 0,
            frame_buffers: std::array::from_fn(|_| FrameBuffer::new()),
            next_buffer: 0,
            frame_counter: 0,
            last_buffer: 0,
        }
    }

    pub fn publish_samples(&mut self, samples: &[ChartSample]) {
        self.ingest_samples(samples.iter().copied());
    }

    fn ingest_samples<I>(&mut self, iter: I)
    where
        I: IntoIterator<Item = ChartSample>,
    {
        let visible_duration = self.options.visible_duration_seconds;
        for sample in iter {
            let state = self.ensure_series_state(sample.series_id);
            state.add(
                SeriesPoint {
                    timestamp_seconds: sample.timestamp_seconds,
                    value: sample.value,
                },
                visible_duration,
            );
        }

        tracing::debug!(series = self.series.len(), "ingested streaming samples");
    }

    fn ensure_series_state(&mut self, series_id: u32) -> &mut SeriesState {
        match self.series.entry(series_id) {
            Entry::Occupied(mut entry) => {
                if let Some(definition) = self.series_definitions.get(&series_id).cloned() {
                    entry.get_mut().set_definition(definition);
                }
                entry.into_mut()
            }
            Entry::Vacant(vacant) => {
                let slot = self.next_palette_slot;
                self.next_palette_slot = self.next_palette_slot.wrapping_add(1);
                let definition = self
                    .series_definitions
                    .get(&series_id)
                    .cloned()
                    .unwrap_or_default();
                vacant.insert(SeriesState::new(slot, definition))
            }
        }
    }

    fn apply_series_override(&mut self, spec: SeriesOverrideSpec) {
        let state = self.ensure_series_state(spec.series_id);

        match spec.label {
            OverrideValue::Set(label) => state.style.label = Some(label),
            OverrideValue::Clear => state.style.label = None,
            OverrideValue::Unchanged => {}
        }

        match spec.stroke_width {
            OverrideValue::Set(width) => state.style.stroke_width = Some(width),
            OverrideValue::Clear => state.style.stroke_width = None,
            OverrideValue::Unchanged => {}
        }

        match spec.color {
            OverrideValue::Set(color) => state.style.color = Some(color),
            OverrideValue::Clear => state.style.color = None,
            OverrideValue::Unchanged => {}
        }
    }

    fn configure_series_definitions(&mut self, definitions: Vec<(u32, SeriesDefinition)>) {
        self.series_definitions.clear();
        for (series_id, definition) in definitions {
            self.series_definitions
                .insert(series_id, definition.clone());
            if let Some(state) = self.series.get_mut(&series_id) {
                state.set_definition(definition);
            }
        }

        for (series_id, state) in self.series.iter_mut() {
            if !self.series_definitions.contains_key(series_id) {
                state.set_definition(SeriesDefinition::default());
            }
        }
    }

    fn set_palette(&mut self, palette: &[Color]) {
        self.options.palette = if palette.is_empty() {
            EngineOptions::default_palette()
        } else {
            palette.to_vec()
        };
    }

    fn render_internal(&mut self, width: u32, height: u32) -> &FrameBuffer {
        let width = width.max(1);
        let height = height.max(1);
        let buffer_index = self.next_buffer;
        self.next_buffer = (self.next_buffer + 1) % self.frame_buffers.len();

        {
            let buffer = &mut self.frame_buffers[buffer_index];
            buffer.scene.reset();

            let chart_width = f64::from(width);
            let chart_height = f64::from(height);
            let plot_area = compute_plot_area(chart_width, chart_height);

            let cpu_start = Instant::now();
            let mut encoded_paths =
                draw_background(&mut buffer.scene, chart_width, chart_height, &plot_area);

            let window = self
                .options
                .visible_duration_seconds
                .max(MIN_VISIBLE_DURATION_SECS);

            let mut global_latest = f64::NEG_INFINITY;
            let mut min_value = f64::INFINITY;
            let mut max_value = f64::NEG_INFINITY;
            let mut has_series = false;

            for state in self.series.values() {
                let span = state.span();
                if span.is_empty() {
                    continue;
                }

                has_series = true;
                global_latest = global_latest.max(state.latest_timestamp);
                let (series_min, series_max) = value_bounds(span);
                min_value = min_value.min(series_min);
                max_value = max_value.max(series_max);
            }

            let mut range_start = f64::NAN;
            let mut range_end = f64::NAN;
            let mut value_min_final = min_value;
            let mut value_max_final = max_value;
            let mut axis_layout: Option<AxisLayout> = None;

            if has_series && global_latest.is_finite() {
                if !min_value.is_finite() || !max_value.is_finite() {
                    min_value = 0.0;
                    max_value = 1.0;
                }

                let mut padding =
                    (max_value - min_value).abs() * self.options.vertical_padding_ratio;
                if !padding.is_finite() {
                    padding = 0.0;
                }
                if padding > 0.0 {
                    min_value -= padding;
                    max_value += padding;
                }
                if (max_value - min_value).abs() < f64::EPSILON {
                    max_value = min_value + 1.0;
                }
                value_min_final = min_value;
                value_max_final = max_value;
                let value_range = (value_max_final - value_min_final).max(f64::EPSILON);

                range_end = global_latest;
                range_start = range_end - window;

                axis_layout = Some(compute_axis_layout(
                    range_start,
                    range_end,
                    value_min_final,
                    value_max_final,
                ));

                if self.options.show_axes {
                    if let Some(layout) = axis_layout.as_ref() {
                        encoded_paths += draw_axes_and_grid(&mut buffer.scene, &plot_area, layout);
                    }
                }

                for state in self.series.values() {
                    let span = state.span();
                    if span.is_empty() {
                        continue;
                    }

                    let series_color = state
                        .style
                        .color
                        .unwrap_or_else(|| color_for_series(&self.options, state.palette_slot));

                    let definition = &state.definition;
                    let base_stroke = definition
                        .stroke_width
                        .unwrap_or(self.options.stroke_width)
                        .max(MIN_STROKE_WIDTH);
                    let stroke_width = state
                        .style
                        .stroke_width
                        .unwrap_or(base_stroke)
                        .max(MIN_STROKE_WIDTH);
                    let fill_opacity = definition.fill_opacity.unwrap_or(0.0);
                    let marker_size = definition.marker_size.unwrap_or(4.0).max(0.5);
                    let fallback_bar_width = if span.len() > 1 {
                        window / span.len() as f64
                    } else {
                        window * 0.05
                    };
                    let bar_width_seconds = definition
                        .bar_width_seconds
                        .unwrap_or(fallback_bar_width)
                        .max(window * 0.001);
                    let baseline_value = definition.baseline.unwrap_or(value_min_final);

                    let (paths, label_anchor) = match definition.kind {
                        SeriesKind::Line => render_line_series(
                            &mut buffer.scene,
                            span,
                            series_color,
                            &plot_area,
                            range_start,
                            window,
                            value_min_final,
                            value_range,
                            stroke_width,
                            fill_opacity,
                            baseline_value,
                        ),
                        SeriesKind::Area => render_area_series(
                            &mut buffer.scene,
                            span,
                            series_color,
                            &plot_area,
                            range_start,
                            window,
                            value_min_final,
                            value_range,
                            stroke_width,
                            fill_opacity.max(0.05),
                            baseline_value,
                        ),
                        SeriesKind::Scatter => render_scatter_series(
                            &mut buffer.scene,
                            span,
                            series_color,
                            &plot_area,
                            range_start,
                            window,
                            value_min_final,
                            value_range,
                            marker_size,
                        ),
                        SeriesKind::Bar => render_bar_series(
                            &mut buffer.scene,
                            span,
                            series_color,
                            &plot_area,
                            range_start,
                            window,
                            value_min_final,
                            value_range,
                            bar_width_seconds,
                            baseline_value,
                        ),
                    };

                    encoded_paths += paths;

                    if let (Some(label), Some(anchor)) =
                        (state.style.label.as_deref(), label_anchor)
                    {
                        encoded_paths += draw_series_label(
                            &mut buffer.scene,
                            label,
                            series_color,
                            anchor,
                            chart_width,
                            chart_height,
                            &plot_area,
                        );
                    }
                }
            } else {
                range_start = 0.0;
                range_end = 0.0;
                value_min_final = 0.0;
                value_max_final = 1.0;
            }

            buffer.metadata.update(
                range_start,
                range_end,
                value_min_final,
                value_max_final,
                &plot_area,
                axis_layout.as_ref(),
                self.series.iter(),
                &self.options,
            );
            self.last_buffer = buffer_index;

            let cpu_time_ms = cpu_start.elapsed().as_secs_f32() * 1_000.0;
            let stats = FrameStats {
                cpu_time_ms,
                gpu_time_ms: 0.0,
                queue_latency_ms: 0.0,
                encoded_paths,
                timestamp: unix_millis(),
            };

            tracing::debug!(
                version = self.frame_counter,
                encoded_paths,
                cpu_time_ms,
                width,
                height,
                "rendered chart frame"
            );

            self.diagnostics.record(stats.clone());
            buffer.stats = stats;
            buffer.version = self.frame_counter;
            self.frame_counter = self.frame_counter.wrapping_add(1);
        }

        &self.frame_buffers[buffer_index]
    }

    pub fn render_frame(&mut self, width: u32, height: u32) -> EngineFrame<'_> {
        let buffer = self.render_internal(width, height);
        EngineFrame {
            version: buffer.version,
            scene: &buffer.scene,
            stats: buffer.stats.clone(),
        }
    }

    pub fn diagnostics(&self) -> &DiagnosticsCollector {
        &self.diagnostics
    }

    fn publish_samples_from_ffi(&mut self, samples: &[VelloChartSamplePoint]) {
        self.ingest_samples(samples.iter().map(|sample| ChartSample {
            series_id: sample.series_id,
            timestamp_seconds: sample.timestamp_seconds,
            value: sample.value,
        }));
    }
}

struct FrameBuffer {
    scene: Scene,
    stats: FrameStats,
    version: u64,
    metadata: FrameMetadata,
}

impl FrameBuffer {
    fn new() -> Self {
        Self {
            scene: Scene::new(),
            stats: FrameStats::default(),
            version: 0,
            metadata: FrameMetadata::default(),
        }
    }
}

#[derive(Default, Clone, Copy)]
struct PlotArea {
    left: f64,
    top: f64,
    width: f64,
    height: f64,
}

impl PlotArea {
    fn right(&self) -> f64 {
        self.left + self.width
    }

    fn bottom(&self) -> f64 {
        self.top + self.height
    }
}

#[derive(Debug, Default, Clone)]
struct SeriesStyle {
    label: Option<String>,
    stroke_width: Option<f64>,
    color: Option<Color>,
}

#[derive(Debug)]
struct SeriesState {
    points: Vec<SeriesPoint>,
    start_index: usize,
    latest_timestamp: f64,
    style: SeriesStyle,
    definition: SeriesDefinition,
    palette_slot: usize,
}

#[derive(Default)]
struct FrameMetadata {
    range_start: f64,
    range_end: f64,
    value_min: f64,
    value_max: f64,
    plot_area: PlotArea,
    time_ticks: Vec<VelloChartAxisTickMetadata>,
    time_labels: Vec<CString>,
    value_ticks: Vec<VelloChartAxisTickMetadata>,
    value_labels: Vec<CString>,
    series: Vec<VelloChartSeriesMetadata>,
    series_labels: Vec<CString>,
}

impl FrameMetadata {
    fn clear(&mut self) {
        self.time_ticks.clear();
        self.time_labels.clear();
        self.value_ticks.clear();
        self.value_labels.clear();
        self.series.clear();
        self.series_labels.clear();
    }

    fn update<'a, I>(
        &mut self,
        range_start: f64,
        range_end: f64,
        value_min: f64,
        value_max: f64,
        plot_area: &PlotArea,
        axis_layout: Option<&AxisLayout>,
        series_iter: I,
        options: &EngineOptions,
    ) where
        I: Iterator<Item = (&'a u32, &'a SeriesState)>,
    {
        self.range_start = range_start;
        self.range_end = range_end;
        self.value_min = value_min;
        self.value_max = value_max;
        self.plot_area = PlotArea {
            left: plot_area.left,
            top: plot_area.top,
            width: plot_area.width,
            height: plot_area.height,
        };

        let window = (range_end - range_start)
            .abs()
            .max(MIN_VISIBLE_DURATION_SECS);

        self.time_ticks.clear();
        self.time_labels.clear();
        self.value_ticks.clear();
        self.value_labels.clear();

        if let Some(layout) = axis_layout {
            for tick in &layout.time_ticks {
                let label = make_c_string(&tick.label);
                let label_len = label.as_bytes().len();
                let ptr = label.as_ptr();
                self.time_labels.push(label);
                self.time_ticks.push(VelloChartAxisTickMetadata {
                    position: tick.position,
                    label: ptr,
                    label_len,
                });
            }

            for tick in &layout.value_ticks {
                let label = make_c_string(&tick.label);
                let label_len = label.as_bytes().len();
                let ptr = label.as_ptr();
                self.value_labels.push(label);
                self.value_ticks.push(VelloChartAxisTickMetadata {
                    position: tick.position,
                    label: ptr,
                    label_len,
                });
            }
        }

        self.series.clear();
        self.series_labels.clear();
        for (series_id, state) in series_iter {
            let color = state
                .style
                .color
                .unwrap_or_else(|| color_for_series(options, state.palette_slot));
            let span_len = state.span().len();
            let definition = &state.definition;
            let base_stroke = definition
                .stroke_width
                .unwrap_or(options.stroke_width)
                .max(MIN_STROKE_WIDTH);
            let stroke_width = state
                .style
                .stroke_width
                .unwrap_or(base_stroke)
                .max(MIN_STROKE_WIDTH);
            let fill_opacity = definition.fill_opacity.unwrap_or(0.0).clamp(0.0, 1.0) as f64;
            let marker_size = definition.marker_size.unwrap_or(4.0);
            let fallback_bar_width = if span_len > 1 {
                window / span_len as f64
            } else {
                window * 0.05
            };
            let bar_width_seconds = definition
                .bar_width_seconds
                .unwrap_or(fallback_bar_width)
                .max(window * 0.001);
            let baseline = definition.baseline.unwrap_or(value_min);
            let label_text = state
                .style
                .label
                .clone()
                .unwrap_or_else(|| format!("Series {}", series_id));
            let label = make_c_string(&label_text);
            let label_len = label.as_bytes().len();
            let ptr = label.as_ptr();
            self.series_labels.push(label);
            self.series.push(VelloChartSeriesMetadata {
                series_id: *series_id,
                kind: series_kind_to_u32(definition.kind),
                color: chart_color_from_peniko(color),
                label: ptr,
                label_len,
                stroke_width,
                fill_opacity,
                marker_size,
                bar_width_seconds,
                baseline,
            });
        }
    }

    fn write_to(&self, out: &mut VelloChartFrameMetadata) {
        *out = VelloChartFrameMetadata {
            range_start: self.range_start,
            range_end: self.range_end,
            value_min: self.value_min,
            value_max: self.value_max,
            plot_left: self.plot_area.left,
            plot_top: self.plot_area.top,
            plot_width: self.plot_area.width,
            plot_height: self.plot_area.height,
            time_ticks: self.time_ticks.as_ptr(),
            time_tick_count: self.time_ticks.len(),
            value_ticks: self.value_ticks.as_ptr(),
            value_tick_count: self.value_ticks.len(),
            series: self.series.as_ptr(),
            series_count: self.series.len(),
        };
    }
}

#[repr(C)]
struct VelloChartAxisTickMetadata {
    position: f64,
    label: *const c_char,
    label_len: usize,
}

#[repr(C)]
pub struct VelloChartSeriesDefinition {
    pub series_id: u32,
    pub kind: u32,
    pub flags: u32,
    pub reserved: u32,
    pub baseline: f64,
    pub fill_opacity: f64,
    pub stroke_width: f64,
    pub marker_size: f64,
    pub bar_width_seconds: f64,
}

#[repr(C)]
struct VelloChartSeriesMetadata {
    series_id: u32,
    kind: u32,
    color: VelloChartColor,
    label: *const c_char,
    label_len: usize,
    stroke_width: f64,
    fill_opacity: f64,
    marker_size: f64,
    bar_width_seconds: f64,
    baseline: f64,
}

#[repr(C)]
struct VelloChartFrameMetadata {
    range_start: f64,
    range_end: f64,
    value_min: f64,
    value_max: f64,
    plot_left: f64,
    plot_top: f64,
    plot_width: f64,
    plot_height: f64,
    time_ticks: *const VelloChartAxisTickMetadata,
    time_tick_count: usize,
    value_ticks: *const VelloChartAxisTickMetadata,
    value_tick_count: usize,
    series: *const VelloChartSeriesMetadata,
    series_count: usize,
}

#[derive(Debug, Clone, Copy)]
struct SeriesPoint {
    timestamp_seconds: f64,
    value: f64,
}

#[derive(Debug)]
struct SeriesDefinitionSpec {
    series_id: u32,
    definition: SeriesDefinition,
}

#[derive(Debug)]
struct SeriesOverrideSpec {
    series_id: u32,
    label: OverrideValue<String>,
    stroke_width: OverrideValue<f64>,
    color: OverrideValue<Color>,
}

#[derive(Debug)]
enum OverrideValue<T> {
    Unchanged,
    Set(T),
    Clear,
}

impl<T> Default for OverrideValue<T> {
    fn default() -> Self {
        Self::Unchanged
    }
}

impl SeriesOverrideSpec {
    fn try_from_ffi(value: &VelloChartSeriesOverride) -> Result<Self, VelloChartEngineStatus> {
        let flags = value.flags;

        if flags & SERIES_OVERRIDE_FLAG_LABEL_SET != 0
            && flags & SERIES_OVERRIDE_FLAG_LABEL_CLEAR != 0
        {
            set_last_error("Series label override cannot set and clear simultaneously");
            return Err(VelloChartEngineStatus::InvalidArgument);
        }

        if flags & SERIES_OVERRIDE_FLAG_STROKE_SET != 0
            && flags & SERIES_OVERRIDE_FLAG_STROKE_CLEAR != 0
        {
            set_last_error("Series stroke override cannot set and clear simultaneously");
            return Err(VelloChartEngineStatus::InvalidArgument);
        }

        if flags & SERIES_OVERRIDE_FLAG_COLOR_SET != 0
            && flags & SERIES_OVERRIDE_FLAG_COLOR_CLEAR != 0
        {
            set_last_error("Series color override cannot set and clear simultaneously");
            return Err(VelloChartEngineStatus::InvalidArgument);
        }

        let mut spec = Self {
            series_id: value.series_id,
            label: OverrideValue::Unchanged,
            stroke_width: OverrideValue::Unchanged,
            color: OverrideValue::Unchanged,
        };

        if flags & SERIES_OVERRIDE_FLAG_LABEL_SET != 0 {
            let label = string_from_raw(value.label_ptr, value.label_len)?;
            spec.label = OverrideValue::Set(label);
        } else if flags & SERIES_OVERRIDE_FLAG_LABEL_CLEAR != 0 {
            spec.label = OverrideValue::Clear;
        }

        if flags & SERIES_OVERRIDE_FLAG_STROKE_SET != 0 {
            let width = value.stroke_width;
            if !width.is_finite() || width <= 0.0 {
                set_last_error("Series stroke width must be positive and finite");
                return Err(VelloChartEngineStatus::InvalidArgument);
            }
            spec.stroke_width = OverrideValue::Set(width);
        } else if flags & SERIES_OVERRIDE_FLAG_STROKE_CLEAR != 0 {
            spec.stroke_width = OverrideValue::Clear;
        }

        if flags & SERIES_OVERRIDE_FLAG_COLOR_SET != 0 {
            spec.color = OverrideValue::Set(value.color.into());
        } else if flags & SERIES_OVERRIDE_FLAG_COLOR_CLEAR != 0 {
            spec.color = OverrideValue::Clear;
        }

        Ok(spec)
    }
}

impl SeriesDefinitionSpec {
    fn try_from_ffi(value: &VelloChartSeriesDefinition) -> Result<Self, VelloChartEngineStatus> {
        let kind = series_kind_from_u32(value.kind)?;
        let mut definition = SeriesDefinition::default();
        definition.kind = kind;

        if value.flags & SERIES_DEFINITION_FLAG_BASELINE_SET != 0 {
            if !value.baseline.is_finite() {
                set_last_error("Series baseline must be finite when specified");
                return Err(VelloChartEngineStatus::InvalidArgument);
            }
            definition.baseline = Some(value.baseline);
        }

        if value.flags & SERIES_DEFINITION_FLAG_FILL_OPACITY_SET != 0 {
            if !value.fill_opacity.is_finite() {
                set_last_error("Series fill opacity must be finite when specified");
                return Err(VelloChartEngineStatus::InvalidArgument);
            }

            if !(0.0..=1.0).contains(&value.fill_opacity) {
                set_last_error("Series fill opacity must be between 0.0 and 1.0");
                return Err(VelloChartEngineStatus::InvalidArgument);
            }

            definition.fill_opacity = Some(value.fill_opacity as f32);
        }

        if value.flags & SERIES_DEFINITION_FLAG_STROKE_WIDTH_SET != 0 {
            if !value.stroke_width.is_finite() || value.stroke_width <= 0.0 {
                set_last_error("Series stroke width must be positive and finite");
                return Err(VelloChartEngineStatus::InvalidArgument);
            }

            definition.stroke_width = Some(value.stroke_width);
        }

        if value.flags & SERIES_DEFINITION_FLAG_MARKER_SIZE_SET != 0 {
            if !value.marker_size.is_finite() || value.marker_size <= 0.0 {
                set_last_error("Series marker size must be positive and finite");
                return Err(VelloChartEngineStatus::InvalidArgument);
            }

            definition.marker_size = Some(value.marker_size);
        }

        if value.flags & SERIES_DEFINITION_FLAG_BAR_WIDTH_SET != 0 {
            if !value.bar_width_seconds.is_finite() || value.bar_width_seconds <= 0.0 {
                set_last_error("Series bar width must be positive and finite");
                return Err(VelloChartEngineStatus::InvalidArgument);
            }

            definition.bar_width_seconds = Some(value.bar_width_seconds);
        }

        Ok(Self {
            series_id: value.series_id,
            definition,
        })
    }
}

impl SeriesState {
    fn new(palette_slot: usize, definition: SeriesDefinition) -> Self {
        Self {
            points: Vec::with_capacity(1_024),
            start_index: 0,
            latest_timestamp: 0.0,
            style: SeriesStyle::default(),
            definition,
            palette_slot,
        }
    }

    fn set_definition(&mut self, definition: SeriesDefinition) {
        self.definition = definition;
    }

    fn add(&mut self, sample: SeriesPoint, window_seconds: f64) {
        self.points.push(sample);
        self.latest_timestamp = sample.timestamp_seconds;

        if window_seconds > 0.0 {
            let cutoff = self.latest_timestamp - window_seconds;
            while self.start_index < self.points.len()
                && self.points[self.start_index].timestamp_seconds < cutoff
            {
                self.start_index += 1;
            }

            if self.start_index > 512 && self.start_index > self.points.len() / 2 {
                self.points.drain(..self.start_index);
                self.start_index = 0;
            }
        }
    }

    fn span(&self) -> &[SeriesPoint] {
        if self.start_index >= self.points.len() {
            &[]
        } else {
            &self.points[self.start_index..]
        }
    }
}

fn project_time(timestamp: f64, range_start: f64, window: f64, plot_area: &PlotArea) -> f64 {
    let normalized = ((timestamp - range_start) / window).clamp(0.0, 1.0);
    plot_area.left + normalized * plot_area.width
}

fn project_value(value: f64, value_min: f64, value_range: f64, plot_area: &PlotArea) -> f64 {
    let normalized = ((value - value_min) / value_range).clamp(0.0, 1.0);
    plot_area.top + (1.0 - normalized) * plot_area.height
}

fn project_point(
    sample: &SeriesPoint,
    range_start: f64,
    window: f64,
    plot_area: &PlotArea,
    value_min: f64,
    value_range: f64,
) -> Point {
    let x = project_time(sample.timestamp_seconds, range_start, window, plot_area);
    let y = project_value(sample.value, value_min, value_range, plot_area);
    Point::new(x, y)
}

fn render_line_series(
    scene: &mut Scene,
    span: &[SeriesPoint],
    color: Color,
    plot_area: &PlotArea,
    range_start: f64,
    window: f64,
    value_min: f64,
    value_range: f64,
    stroke_width: f64,
    fill_opacity: f32,
    baseline_value: f64,
) -> (u32, Option<Point>) {
    if span.is_empty() {
        return (0, None);
    }

    if span.len() == 1 {
        let point = project_point(
            &span[0],
            range_start,
            window,
            plot_area,
            value_min,
            value_range,
        );
        let radius = (stroke_width.max(MIN_STROKE_WIDTH)) * 0.5;
        let circle = Circle::new(point, radius);
        scene.fill(
            Fill::NonZero,
            Affine::IDENTITY,
            &Brush::Solid(color),
            None,
            &circle,
        );
        return (1, Some(point));
    }

    let baseline_y = project_value(baseline_value, value_min, value_range, plot_area);
    let fill_alpha = fill_opacity.clamp(0.0, 1.0);

    let mut path = BezPath::new();
    let mut fill_path = BezPath::new();
    let mut area_top = plot_area.bottom();
    let mut last_point = None;

    for (index, sample) in span.iter().enumerate() {
        let point = project_point(
            sample,
            range_start,
            window,
            plot_area,
            value_min,
            value_range,
        );
        if index == 0 {
            path.move_to(point);
            if fill_alpha > 0.0 {
                fill_path.move_to(Point::new(point.x, baseline_y));
                fill_path.line_to(point);
            }
        } else {
            path.line_to(point);
            if fill_alpha > 0.0 {
                fill_path.line_to(point);
            }
        }
        area_top = area_top.min(point.y);
        last_point = Some(point);
    }

    let mut encoded = 0;

    if fill_alpha > 0.0 {
        if let Some(last) = last_point {
            fill_path.line_to(Point::new(last.x, baseline_y));
            fill_path.close_path();

            let gradient = Gradient::new_linear(
                Point::new(0.0, area_top.min(baseline_y)),
                Point::new(0.0, baseline_y.max(plot_area.bottom())),
            )
            .with_stops([
                ColorStop {
                    offset: 0.0,
                    color: DynamicColor::from_alpha_color(color.with_alpha(fill_alpha)),
                },
                ColorStop {
                    offset: 1.0,
                    color: DynamicColor::from_alpha_color(color.with_alpha(0.0)),
                },
            ]);

            scene.fill(
                Fill::NonZero,
                Affine::IDENTITY,
                &Brush::Gradient(gradient),
                None,
                &fill_path,
            );
            encoded += 1;
        }
    }

    let stroke = Stroke::new(stroke_width.max(MIN_STROKE_WIDTH));
    scene.stroke(&stroke, Affine::IDENTITY, &color, None, &path);
    encoded += 1;

    (encoded, last_point)
}

fn render_area_series(
    scene: &mut Scene,
    span: &[SeriesPoint],
    color: Color,
    plot_area: &PlotArea,
    range_start: f64,
    window: f64,
    value_min: f64,
    value_range: f64,
    stroke_width: f64,
    fill_opacity: f32,
    baseline_value: f64,
) -> (u32, Option<Point>) {
    if span.is_empty() {
        return (0, None);
    }

    let baseline_y = project_value(baseline_value, value_min, value_range, plot_area);
    let fill_alpha = fill_opacity.clamp(0.0, 1.0);
    let mut fill_path = BezPath::new();
    let mut outline = BezPath::new();
    let mut last_point = None;

    for (index, sample) in span.iter().enumerate() {
        let point = project_point(
            sample,
            range_start,
            window,
            plot_area,
            value_min,
            value_range,
        );
        if index == 0 {
            fill_path.move_to(Point::new(point.x, baseline_y));
            fill_path.line_to(point);
            outline.move_to(point);
        } else {
            fill_path.line_to(point);
            outline.line_to(point);
        }
        last_point = Some(point);
    }

    let mut encoded = 0;

    if let Some(last) = last_point {
        fill_path.line_to(Point::new(last.x, baseline_y));
        fill_path.close_path();

        scene.fill(
            Fill::NonZero,
            Affine::IDENTITY,
            &Brush::Solid(color.with_alpha(fill_alpha)),
            None,
            &fill_path,
        );
        encoded += 1;

        if stroke_width > 0.0 {
            let stroke = Stroke::new(stroke_width.max(MIN_STROKE_WIDTH));
            scene.stroke(&stroke, Affine::IDENTITY, &color, None, &outline);
            encoded += 1;
        }
    }

    (encoded, last_point)
}

fn render_scatter_series(
    scene: &mut Scene,
    span: &[SeriesPoint],
    color: Color,
    plot_area: &PlotArea,
    range_start: f64,
    window: f64,
    value_min: f64,
    value_range: f64,
    marker_size: f64,
) -> (u32, Option<Point>) {
    if span.is_empty() {
        return (0, None);
    }

    let radius = (marker_size / 2.0).max(0.5);
    let mut last_point = None;

    for sample in span {
        let point = project_point(
            sample,
            range_start,
            window,
            plot_area,
            value_min,
            value_range,
        );
        let circle = Circle::new(point, radius);
        scene.fill(
            Fill::NonZero,
            Affine::IDENTITY,
            &Brush::Solid(color),
            None,
            &circle,
        );
        last_point = Some(point);
    }

    (span.len() as u32, last_point)
}

fn render_bar_series(
    scene: &mut Scene,
    span: &[SeriesPoint],
    color: Color,
    plot_area: &PlotArea,
    range_start: f64,
    window: f64,
    value_min: f64,
    value_range: f64,
    bar_width_seconds: f64,
    baseline_value: f64,
) -> (u32, Option<Point>) {
    if span.is_empty() {
        return (0, None);
    }

    let baseline_y = project_value(baseline_value, value_min, value_range, plot_area);
    let normalized_width = (bar_width_seconds / window).clamp(0.0, 1.0);
    let half_width = (normalized_width * plot_area.width).max(1.0) * 0.5;
    let mut last_point = None;
    let mut encoded = 0;

    for sample in span {
        let point = project_point(
            sample,
            range_start,
            window,
            plot_area,
            value_min,
            value_range,
        );
        let x0 = (point.x - half_width).max(plot_area.left);
        let x1 = (point.x + half_width).min(plot_area.right());
        let (top, bottom) = if point.y < baseline_y {
            (point.y, baseline_y)
        } else {
            (baseline_y, point.y)
        };
        let rect = Rect::new(x0, top, x1, bottom);
        scene.fill(
            Fill::NonZero,
            Affine::IDENTITY,
            &Brush::Solid(color),
            None,
            &rect,
        );
        encoded += 1;
        last_point = Some(Point::new((x0 + x1) * 0.5, top));
    }

    (encoded, last_point)
}

fn draw_background(scene: &mut Scene, width: f64, height: f64, plot: &PlotArea) -> u32 {
    if width <= 0.0 || height <= 0.0 {
        return 0;
    }

    let mut encoded = 0;

    let rect = Rect::new(0.0, 0.0, width, height);
    let gradient =
        Gradient::new_linear(Point::new(0.0, 0.0), Point::new(0.0, height)).with_stops([
            ColorStop {
                offset: 0.0,
                color: DynamicColor::from_alpha_color(Color::from_rgba8(0x10, 0x18, 0x28, 0xF0)),
            },
            ColorStop {
                offset: 1.0,
                color: DynamicColor::from_alpha_color(Color::from_rgba8(0x0B, 0x11, 0x1C, 0xE6)),
            },
        ]);
    scene.fill(
        Fill::NonZero,
        Affine::IDENTITY,
        &Brush::Gradient(gradient),
        None,
        &rect,
    );
    encoded += 1;

    let border_rect = Rect::new(0.5, 0.5, width - 0.5, height - 0.5);
    let border_color = Color::from_rgba8(0x2A, 0x34, 0x4A, 0xFF);
    scene.stroke(
        &Stroke::new(1.0),
        Affine::IDENTITY,
        &border_color,
        None,
        &border_rect,
    );
    encoded += 1;

    let plot_rect = Rect::new(
        plot.left.max(0.5),
        plot.top.max(0.5),
        plot.right().min(width - 0.5),
        plot.bottom().min(height - 0.5),
    );

    let plot_fill = Color::from_rgba8(0x12, 0x1B, 0x2A, 0xC8);
    scene.fill(
        Fill::NonZero,
        Affine::IDENTITY,
        &Brush::Solid(plot_fill),
        None,
        &plot_rect,
    );
    encoded += 1;

    let plot_border = Color::from_rgba8(0x24, 0x30, 0x45, 0xFF);
    scene.stroke(
        &Stroke::new(1.0),
        Affine::IDENTITY,
        &plot_border,
        None,
        &plot_rect,
    );
    encoded += 1;

    encoded
}

fn draw_series_label(
    scene: &mut Scene,
    label: &str,
    color: Color,
    anchor: Point,
    chart_width: f64,
    chart_height: f64,
    plot: &PlotArea,
) -> u32 {
    let layout = match layout_label(label, LABEL_FONT_SIZE) {
        Some(layout) => layout,
        None => return 0,
    };

    let text_width = f64::from(layout.width);
    let text_height = f64::from(layout.height);
    let total_width = text_width + 2.0 * LABEL_HORIZONTAL_PADDING;
    let total_height = text_height + 2.0 * LABEL_VERTICAL_PADDING;

    if total_width <= 0.0 || total_height <= 0.0 {
        return 0;
    }

    let mut left = anchor.x + LABEL_HORIZONTAL_OFFSET;
    let min_left = plot.left.max(0.0);
    let mut max_left = (chart_width - total_width).max(min_left);
    if max_left < min_left {
        max_left = min_left;
    }
    if left < min_left {
        left = min_left;
    }
    if left > max_left {
        left = max_left;
    }
    let plot_right_limit = plot.right().min(chart_width);
    if left + total_width > plot_right_limit {
        left = (plot_right_limit - total_width).max(min_left);
    }

    let mut top = anchor.y - LABEL_VERTICAL_GAP - total_height;
    let min_top = (plot.top - total_height).max(0.0);
    let mut max_top = (plot.bottom() - total_height).min(chart_height - total_height);
    if max_top < min_top {
        max_top = min_top;
    }
    if top < min_top {
        top = min_top;
    }
    if top > max_top {
        top = max_top;
    }

    let right = (left + total_width).min(chart_width);
    let bottom = (top + total_height).min(chart_height);

    if right <= left || bottom <= top {
        return 0;
    }

    let background = RoundedRect::new(left, top, right, bottom, LABEL_CORNER_RADIUS);
    let background_color = color.with_alpha(0.28);
    scene.fill(
        Fill::NonZero,
        Affine::IDENTITY,
        &Brush::Solid(background_color),
        None,
        &background,
    );

    let border_color = color.with_alpha(0.6);
    scene.stroke(
        &Stroke::new(1.0),
        Affine::IDENTITY,
        &border_color,
        None,
        &background,
    );

    let baseline_x = (left + LABEL_HORIZONTAL_PADDING).min(chart_width);
    let baseline_y = (top + LABEL_VERTICAL_PADDING + f64::from(layout.ascent)).min(chart_height);

    scene
        .draw_glyphs(label_font())
        .font_size(LABEL_FONT_SIZE)
        .transform(Affine::translate((baseline_x, baseline_y)))
        .brush(Brush::Solid(label_text_color()))
        .draw(Fill::NonZero, layout.glyphs.into_iter());

    2
}

fn compute_plot_area(width: f64, height: f64) -> PlotArea {
    if width <= 0.0 || height <= 0.0 {
        return PlotArea {
            left: 0.0,
            top: 0.0,
            width: width.max(MIN_PLOT_DIMENSION),
            height: height.max(MIN_PLOT_DIMENSION),
        };
    }

    let mut left_margin = if width >= PLOT_LEFT_MARGIN_MIN * 2.0 {
        PLOT_LEFT_MARGIN_MIN
    } else {
        width * PLOT_LEFT_MARGIN_RATIO
    };
    let mut right_margin = width * PLOT_RIGHT_MARGIN_RATIO;
    let mut top_margin = height * PLOT_TOP_MARGIN_RATIO;
    let mut bottom_margin = if height >= PLOT_BOTTOM_MARGIN_MIN * 2.0 {
        PLOT_BOTTOM_MARGIN_MIN
    } else {
        height * PLOT_BOTTOM_MARGIN_RATIO
    };

    let horizontal_sum = (left_margin + right_margin).max(1e-6);
    if horizontal_sum + MIN_PLOT_DIMENSION > width {
        let available = (width - MIN_PLOT_DIMENSION).max(0.0);
        let scale = if horizontal_sum.abs() < 1e-6 {
            0.0
        } else {
            available / horizontal_sum
        };
        left_margin *= scale;
        right_margin *= scale;
    }

    let vertical_sum = (top_margin + bottom_margin).max(1e-6);
    if vertical_sum + MIN_PLOT_DIMENSION > height {
        let available = (height - MIN_PLOT_DIMENSION).max(0.0);
        let scale = if vertical_sum.abs() < 1e-6 {
            0.0
        } else {
            available / vertical_sum
        };
        top_margin *= scale;
        bottom_margin *= scale;
    }

    let mut plot_width = (width - left_margin - right_margin).max(MIN_PLOT_DIMENSION);
    if plot_width > width {
        plot_width = width;
    }
    let mut left = left_margin.min(width - plot_width).max(0.0);
    let mut right = (left + plot_width).min(width);
    if right - left < MIN_PLOT_DIMENSION {
        right = (left + MIN_PLOT_DIMENSION).min(width);
        left = (right - MIN_PLOT_DIMENSION).max(0.0);
    }
    plot_width = (right - left).max(MIN_PLOT_DIMENSION);

    let mut plot_height = (height - top_margin - bottom_margin).max(MIN_PLOT_DIMENSION);
    if plot_height > height {
        plot_height = height;
    }
    let mut top = top_margin.min(height - plot_height).max(0.0);
    let mut bottom = (top + plot_height).min(height);
    if bottom - top < MIN_PLOT_DIMENSION {
        bottom = (top + MIN_PLOT_DIMENSION).min(height);
        top = (bottom - MIN_PLOT_DIMENSION).max(0.0);
    }
    plot_height = (bottom - top).max(MIN_PLOT_DIMENSION);

    PlotArea {
        left,
        top,
        width: plot_width,
        height: plot_height,
    }
}

struct AxisTick {
    position: f64,
    label: String,
}

struct AxisLayout {
    time_ticks: Vec<AxisTick>,
    value_ticks: Vec<AxisTick>,
}

fn axis_color() -> Color {
    Color::from_rgba8(0x45, 0x53, 0x72, 0xFF)
}

fn grid_color() -> Color {
    Color::from_rgba8(0x23, 0x2E, 0x42, 0xFF)
}

fn axis_label_color() -> Color {
    Color::from_rgba8(0xA8, 0xB7, 0xD6, 0xFF)
}

fn make_c_string(label: &str) -> CString {
    let sanitized = if label.as_bytes().contains(&0) {
        label.replace('\0', " ")
    } else {
        label.to_owned()
    };
    CString::new(sanitized).unwrap_or_else(|_| CString::new("").expect("CString::new failed"))
}

fn chart_color_from_peniko(color: Color) -> VelloChartColor {
    let rgba = color.to_rgba8();
    VelloChartColor {
        r: rgba.r,
        g: rgba.g,
        b: rgba.b,
        a: rgba.a,
    }
}

fn draw_axes_and_grid(scene: &mut Scene, plot: &PlotArea, layout: &AxisLayout) -> u32 {
    let mut encoded = 0;
    let axis_stroke = Stroke::new(AXIS_STROKE_WIDTH);
    let axis_color = axis_color();
    let grid_stroke = Stroke::new(GRID_STROKE_WIDTH);
    let grid_color = grid_color();
    let label_color = axis_label_color();

    scene.stroke(
        &axis_stroke,
        Affine::IDENTITY,
        &axis_color,
        None,
        &Line::new((plot.left, plot.top), (plot.left, plot.bottom())),
    );
    encoded += 1;

    scene.stroke(
        &axis_stroke,
        Affine::IDENTITY,
        &axis_color,
        None,
        &Line::new((plot.left, plot.bottom()), (plot.right(), plot.bottom())),
    );
    encoded += 1;

    for tick in &layout.value_ticks {
        let y = plot.bottom() - tick.position * plot.height;
        if (y - plot.bottom()).abs() < 0.5 || (y - plot.top).abs() < 0.5 {
            continue;
        }
        scene.stroke(
            &grid_stroke,
            Affine::IDENTITY,
            &grid_color,
            None,
            &Line::new((plot.left, y), (plot.right(), y)),
        );
        encoded += 1;
    }

    for tick in &layout.time_ticks {
        let x = plot.left + tick.position * plot.width;
        if (x - plot.left).abs() < 0.5 || (x - plot.right()).abs() < 0.5 {
            continue;
        }
        scene.stroke(
            &grid_stroke,
            Affine::IDENTITY,
            &grid_color,
            None,
            &Line::new((x, plot.top), (x, plot.bottom())),
        );
        encoded += 1;
    }

    for tick in &layout.value_ticks {
        let y = plot.bottom() - tick.position * plot.height;
        if let Some(layout) = layout_label(&tick.label, AXIS_FONT_SIZE) {
            let LabelLayout {
                glyphs,
                width,
                height,
                ..
            } = layout;
            let width = f64::from(width);
            let height = f64::from(height);
            let mut baseline_x = (plot.left - AXIS_LABEL_LEFT_MARGIN - width).max(0.0);
            let min_baseline_x = 0.0;
            baseline_x = baseline_x.max(min_baseline_x);
            let baseline_y = (y + height / 2.0).clamp(plot.top - height, plot.bottom() + height);
            scene
                .draw_glyphs(label_font())
                .font_size(AXIS_FONT_SIZE)
                .transform(Affine::translate((baseline_x, baseline_y)))
                .brush(Brush::Solid(label_color))
                .draw(Fill::NonZero, glyphs.into_iter());
            encoded += 1;
        }
    }

    for tick in &layout.time_ticks {
        let x = plot.left + tick.position * plot.width;
        if let Some(layout) = layout_label(&tick.label, AXIS_FONT_SIZE) {
            let LabelLayout {
                glyphs,
                width,
                height,
                ..
            } = layout;
            let width = f64::from(width);
            let height = f64::from(height);
            let min_x = plot.left;
            let max_x = (plot.right() - width).max(min_x);
            let mut baseline_x = x - width / 2.0;
            baseline_x = baseline_x.clamp(min_x, max_x);
            let baseline_y = plot.bottom() + AXIS_LABEL_BOTTOM_MARGIN + height * 0.25;
            scene
                .draw_glyphs(label_font())
                .font_size(AXIS_FONT_SIZE)
                .transform(Affine::translate((baseline_x, baseline_y)))
                .brush(Brush::Solid(label_color))
                .draw(Fill::NonZero, glyphs.into_iter());
            encoded += 1;
        }
    }

    encoded
}

fn compute_axis_layout(
    range_start: f64,
    range_end: f64,
    min_value: f64,
    max_value: f64,
) -> AxisLayout {
    AxisLayout {
        time_ticks: compute_time_ticks(range_start, range_end, 6),
        value_ticks: compute_value_ticks(min_value, max_value, 6),
    }
}

fn compute_time_ticks(range_start: f64, range_end: f64, max_ticks: usize) -> Vec<AxisTick> {
    let mut ticks = Vec::new();
    if !range_start.is_finite() || !range_end.is_finite() || range_end <= range_start {
        return ticks;
    }

    let total = range_end - range_start;
    let approx_step = total / (max_ticks.max(2) as f64);
    let step = pick_time_step(approx_step);
    if step <= 0.0 {
        return ticks;
    }

    let first_tick = (range_start / step).ceil() * step;
    let mut value = first_tick;
    while value <= range_end + step * 0.5 {
        let position = ((value - range_start) / total).clamp(0.0, 1.0);
        ticks.push(AxisTick {
            position,
            label: format_time_label(value, step),
        });
        value += step;
    }

    if ticks.is_empty() {
        ticks.push(AxisTick {
            position: 0.0,
            label: format_time_label(range_start, step),
        });
        ticks.push(AxisTick {
            position: 1.0,
            label: format_time_label(range_end, step),
        });
    }

    ticks
}

fn pick_time_step(target: f64) -> f64 {
    const STEPS: &[f64] = &[
        0.5, 1.0, 2.0, 5.0, 10.0, 15.0, 30.0, 60.0, 120.0, 300.0, 600.0, 900.0, 1800.0, 3600.0,
        7200.0, 14_400.0, 28_800.0, 43_200.0, 86_400.0,
    ];

    for step in STEPS {
        if *step >= target {
            return *step;
        }
    }

    *STEPS.last().unwrap_or(&60.0)
}

fn format_time_label(timestamp: f64, step: f64) -> String {
    if !timestamp.is_finite() {
        return "NaN".to_string();
    }

    let secs = timestamp.floor();
    let subseconds = ((timestamp - secs) * 1_000.0).round() as i64;
    let secs_i64 = secs as i64;

    if let Ok(dt) = OffsetDateTime::from_unix_timestamp(secs_i64) {
        let time = dt.time();
        if step < 1.0 {
            let millis = subseconds.clamp(0, 999);
            format!(
                "{:02}:{:02}:{:02}.{:03}",
                time.hour(),
                time.minute(),
                time.second(),
                millis
            )
        } else if step < 60.0 {
            format!(
                "{:02}:{:02}:{:02}",
                time.hour(),
                time.minute(),
                time.second()
            )
        } else {
            format!("{:02}:{:02}", time.hour(), time.minute())
        }
    } else {
        format!("{timestamp:.0}")
    }
}

fn compute_value_ticks(min_value: f64, max_value: f64, max_ticks: usize) -> Vec<AxisTick> {
    let mut ticks = Vec::new();
    if !min_value.is_finite() || !max_value.is_finite() || max_value <= min_value {
        return ticks;
    }

    let range = nice_number(max_value - min_value, false);
    let step = nice_number(range / ((max_ticks.max(2) - 1) as f64), true);
    if step <= 0.0 {
        return ticks;
    }

    let mut tick = (min_value / step).floor() * step;
    let tick_max = (max_value / step).ceil() * step;
    while tick <= tick_max + step * 0.5 {
        let position = ((tick - min_value) / (max_value - min_value)).clamp(0.0, 1.0);
        ticks.push(AxisTick {
            position,
            label: format_value_label(tick, step),
        });
        tick += step;
    }

    ticks
}

fn nice_number(range: f64, round: bool) -> f64 {
    if range <= 0.0 || !range.is_finite() {
        return 0.0;
    }

    let exponent = range.log10().floor();
    let fraction = range / 10f64.powf(exponent);

    let nice_fraction = if round {
        if fraction < 1.5 {
            1.0
        } else if fraction < 3.0 {
            2.0
        } else if fraction < 7.0 {
            5.0
        } else {
            10.0
        }
    } else if fraction <= 1.0 {
        1.0
    } else if fraction <= 2.0 {
        2.0
    } else if fraction <= 5.0 {
        5.0
    } else {
        10.0
    };

    nice_fraction * 10f64.powf(exponent)
}

fn format_value_label(value: f64, step: f64) -> String {
    if !value.is_finite() || !step.is_finite() {
        return "NaN".to_string();
    }

    let mut decimals = 0usize;
    if step > 0.0 {
        let log10 = step.log10();
        if log10 < 0.0 {
            decimals = (-log10).ceil() as usize;
        }
    }
    decimals = decimals.min(6);
    format!("{value:.decimals$}")
}

struct LabelLayout {
    glyphs: Vec<Glyph>,
    width: f32,
    height: f32,
    ascent: f32,
}

fn layout_label(text: &str, font_size: f32) -> Option<LabelLayout> {
    let trimmed = text.trim();
    if trimmed.is_empty() {
        return None;
    }

    let font = label_font();
    let font_ref = to_font_ref(font)?;
    let size = Size::new(font_size);
    let axes = font_ref.axes();
    let location = Location::new(axes.len());
    let metrics = font_ref.metrics(size, &location);
    let glyph_metrics = font_ref.glyph_metrics(size, &location);
    let charmap = font_ref.charmap();

    let mut pen_x = 0f32;
    let mut glyphs = Vec::with_capacity(trimmed.len());
    for ch in trimmed.chars() {
        if ch.is_control() && ch != ' ' {
            continue;
        }
        let glyph_id = charmap.map(ch).unwrap_or_default();
        let advance = glyph_metrics.advance_width(glyph_id).unwrap_or_default();
        glyphs.push(Glyph {
            id: glyph_id.to_u32(),
            x: pen_x,
            y: 0.0,
        });
        pen_x += advance;
    }

    if glyphs.is_empty() {
        return None;
    }

    let ascent = metrics.ascent;
    let height = ascent - metrics.descent;

    Some(LabelLayout {
        glyphs,
        width: pen_x,
        height,
        ascent,
    })
}

fn label_font() -> &'static FontData {
    &LABEL_FONT
}

fn label_text_color() -> Color {
    Color::from_rgba8(0xF5, 0xF9, 0xFF, 0xFF)
}

fn to_font_ref(font: &FontData) -> Option<FontRef<'_>> {
    let file_ref = FileRef::new(font.data.as_ref()).ok()?;
    match file_ref {
        FileRef::Font(font) => Some(font),
        FileRef::Collection(collection) => collection.get(font.index).ok(),
    }
}

fn value_bounds(points: &[SeriesPoint]) -> (f64, f64) {
    let mut min = f64::INFINITY;
    let mut max = f64::NEG_INFINITY;

    for point in points {
        min = min.min(point.value);
        max = max.max(point.value);
    }

    if !min.is_finite() || !max.is_finite() {
        (0.0, 1.0)
    } else if (max - min).abs() < f64::EPSILON {
        (min - 1.0, max + 1.0)
    } else {
        (min, max)
    }
}

fn unix_millis() -> u128 {
    SystemTime::now()
        .duration_since(SystemTime::UNIX_EPOCH)
        .map(|d| d.as_millis())
        .unwrap_or(0)
}

// === Diagnostics bridge ===

type TraceCallback =
    unsafe extern "C" fn(ChartTraceLevel, i64, *const c_char, *const c_char, *const c_char);

fn trace_callback_storage() -> &'static RwLock<Option<TraceCallback>> {
    static STORAGE: OnceLock<RwLock<Option<TraceCallback>>> = OnceLock::new();
    STORAGE.get_or_init(|| RwLock::new(None))
}

fn ensure_tracing_layer() {
    static INIT: OnceLock<()> = OnceLock::new();
    INIT.get_or_init(|| {
        let layer = EventSourceLayer::default();
        let subscriber = Registry::default().with(layer);
        let _ = tracing::subscriber::set_global_default(subscriber);
    });
}

#[derive(Default)]
struct EventSourceLayer;

impl<S> Layer<S> for EventSourceLayer
where
    S: tracing::Subscriber + for<'span> LookupSpan<'span>,
{
    fn on_event(&self, event: &tracing::Event<'_>, _ctx: Context<'_, S>) {
        let callback_guard = trace_callback_storage();
        let callback = match callback_guard.read().ok().and_then(|guard| guard.clone()) {
            Some(callback) => callback,
            None => return,
        };

        let mut visitor = MessageVisitor::default();
        event.record(&mut visitor);

        let message_ref = visitor
            .message
            .as_deref()
            .unwrap_or_else(|| event.metadata().name());
        let message = CString::new(sanitize_for_cstring(message_ref)).unwrap_or_default();
        let target = CString::new(event.metadata().target()).unwrap_or_default();
        let properties = CString::new(visitor.format_properties()).unwrap_or_default();
        let timestamp_ms = unix_millis().min(i64::MAX as u128) as i64;
        let level: ChartTraceLevel = (*event.metadata().level()).into();

        unsafe {
            callback(
                level,
                timestamp_ms,
                target.as_ptr(),
                message.as_ptr(),
                properties.as_ptr(),
            );
        }
    }
}

#[derive(Default)]
struct MessageVisitor {
    message: Option<String>,
    fields: Vec<(String, String)>,
}

impl tracing::field::Visit for MessageVisitor {
    fn record_debug(&mut self, field: &tracing::field::Field, value: &dyn fmt::Debug) {
        self.record(field, format!("{value:?}"));
    }

    fn record_str(&mut self, field: &tracing::field::Field, value: &str) {
        self.record(field, value.to_owned());
    }

    fn record_bool(&mut self, field: &tracing::field::Field, value: bool) {
        self.record(field, value.to_string());
    }

    fn record_i64(&mut self, field: &tracing::field::Field, value: i64) {
        self.record(field, value.to_string());
    }

    fn record_u64(&mut self, field: &tracing::field::Field, value: u64) {
        self.record(field, value.to_string());
    }

    fn record_f64(&mut self, field: &tracing::field::Field, value: f64) {
        self.record(field, value.to_string());
    }
}

impl MessageVisitor {
    fn record(&mut self, field: &tracing::field::Field, value: String) {
        if field.name() == "message" {
            self.message = Some(value);
        } else {
            self.fields
                .push((field.name().to_owned(), value.replace('\0', "")));
        }
    }

    fn format_properties(&self) -> String {
        if self.fields.is_empty() {
            String::new()
        } else {
            self.fields
                .iter()
                .map(|(key, val)| format!("{key}={val}"))
                .collect::<Vec<_>>()
                .join(";")
        }
    }
}

fn sanitize_for_cstring(value: &str) -> String {
    value.replace('\0', "")
}

#[repr(i32)]
#[derive(Debug, Copy, Clone)]
pub enum ChartTraceLevel {
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warn = 3,
    Error = 4,
}

impl From<Level> for ChartTraceLevel {
    fn from(level: Level) -> Self {
        match level {
            Level::TRACE => Self::Trace,
            Level::DEBUG => Self::Debug,
            Level::INFO => Self::Info,
            Level::WARN => Self::Warn,
            Level::ERROR => Self::Error,
        }
    }
}

// === FFI surface ===

#[repr(C)]
pub struct VelloSceneHandle {
    inner: Scene,
}

#[repr(C)]
pub struct VelloChartEngineHandle {
    inner: ChartEngine,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct VelloChartEngineOptions {
    pub visible_duration_seconds: f64,
    pub vertical_padding_ratio: f64,
    pub stroke_width: f64,
    pub show_axes: u32,
    pub palette_len: usize,
    pub palette_ptr: *const VelloChartColor,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct VelloChartColor {
    pub r: u8,
    pub g: u8,
    pub b: u8,
    pub a: u8,
}

impl From<VelloChartColor> for Color {
    fn from(value: VelloChartColor) -> Self {
        Color::from_rgba8(value.r, value.g, value.b, value.a)
    }
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct VelloChartSeriesOverride {
    pub series_id: u32,
    pub flags: u32,
    pub label_ptr: *const c_char,
    pub label_len: usize,
    pub stroke_width: f64,
    pub color: VelloChartColor,
}

impl From<VelloChartEngineOptions> for EngineOptions {
    fn from(value: VelloChartEngineOptions) -> Self {
        let defaults = EngineOptions::default();

        let visible_duration_seconds = if value.visible_duration_seconds.is_finite() {
            value
                .visible_duration_seconds
                .max(MIN_VISIBLE_DURATION_SECS)
        } else {
            defaults.visible_duration_seconds
        };

        let vertical_padding_ratio = if value.vertical_padding_ratio.is_finite() {
            value.vertical_padding_ratio.max(0.0)
        } else {
            defaults.vertical_padding_ratio
        };

        let stroke_width = if value.stroke_width.is_finite() && value.stroke_width > 0.0 {
            value.stroke_width
        } else {
            defaults.stroke_width
        };

        let mut palette = palette_from_raw(value.palette_ptr, value.palette_len);
        if palette.is_empty() {
            palette = defaults.palette.clone();
        }

        Self {
            visible_duration_seconds,
            vertical_padding_ratio,
            stroke_width,
            palette,
            show_axes: value.show_axes != 0,
        }
    }
}

fn palette_from_raw(ptr: *const VelloChartColor, len: usize) -> Vec<Color> {
    if ptr.is_null() || len == 0 {
        return Vec::new();
    }

    unsafe { slice::from_raw_parts(ptr, len) }
        .iter()
        .map(|color| Color::from(*color))
        .collect()
}

fn string_from_raw(ptr: *const c_char, len: usize) -> Result<String, VelloChartEngineStatus> {
    if len == 0 {
        return Ok(String::new());
    }

    if ptr.is_null() {
        set_last_error("Null string pointer passed to chart engine");
        return Err(VelloChartEngineStatus::NullPointer);
    }

    let bytes = unsafe { slice::from_raw_parts(ptr.cast::<u8>(), len) };
    match str::from_utf8(bytes) {
        Ok(value) => Ok(value.to_owned()),
        Err(_) => {
            set_last_error("Series label must be valid UTF-8");
            Err(VelloChartEngineStatus::InvalidArgument)
        }
    }
}

fn color_for_series(options: &EngineOptions, index: usize) -> Color {
    if options.palette.is_empty() {
        let (r, g, b) = DEFAULT_PALETTE[index % DEFAULT_PALETTE.len()];
        Color::from_rgba8(r, g, b, 0xFF)
    } else {
        options.palette[index % options.palette.len()]
    }
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct VelloChartSamplePoint {
    pub series_id: u32,
    pub timestamp_seconds: f64,
    pub value: f64,
}

#[repr(C)]
#[derive(Default, Clone, Copy)]
pub struct VelloChartFrameStats {
    pub cpu_time_ms: f32,
    pub gpu_time_ms: f32,
    pub queue_latency_ms: f32,
    pub encoded_paths: u32,
    pub timestamp_ms: i64,
}

impl From<&FrameStats> for VelloChartFrameStats {
    fn from(stats: &FrameStats) -> Self {
        Self {
            cpu_time_ms: stats.cpu_time_ms,
            gpu_time_ms: stats.gpu_time_ms,
            queue_latency_ms: stats.queue_latency_ms,
            encoded_paths: stats.encoded_paths,
            timestamp_ms: stats.timestamp.min(i64::MAX as u128) as i64,
        }
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloChartEngineStatus {
    Success = 0,
    NullPointer = 1,
    InvalidArgument = 2,
    Unknown = 255,
}

thread_local! {
    static LAST_ERROR: RefCell<Option<CString>> = const { RefCell::new(None) };
}

fn clear_last_error() {
    LAST_ERROR.with(|slot| slot.borrow_mut().take());
}

fn set_last_error(msg: impl Into<String>) {
    let message =
        CString::new(msg.into()).unwrap_or_else(|_| CString::new("invalid error").unwrap());
    LAST_ERROR.with(|slot| *slot.borrow_mut() = Some(message));
}

fn slice_from_raw<'a, T>(ptr: *const T, len: usize) -> Result<&'a [T], VelloChartEngineStatus> {
    if ptr.is_null() && len != 0 {
        set_last_error("Null slice pointer passed to chart engine");
        Err(VelloChartEngineStatus::NullPointer)
    } else {
        // SAFETY: caller ensures that the pointer is valid for len elements.
        Ok(unsafe { slice::from_raw_parts(ptr, len) })
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn vello_chart_engine_last_error_message() -> *const c_char {
    LAST_ERROR.with(|slot| {
        slot.borrow()
            .as_ref()
            .map_or(std::ptr::null(), |cstr| cstr.as_ptr())
    })
}

#[unsafe(no_mangle)]
pub extern "C" fn vello_chart_engine_create(
    options: VelloChartEngineOptions,
) -> *mut VelloChartEngineHandle {
    clear_last_error();
    let engine = ChartEngine::new(options.into());
    Box::into_raw(Box::new(VelloChartEngineHandle { inner: engine }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_chart_engine_destroy(handle: *mut VelloChartEngineHandle) {
    if !handle.is_null() {
        unsafe { drop(Box::from_raw(handle)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_chart_engine_publish_samples(
    handle: *mut VelloChartEngineHandle,
    samples: *const VelloChartSamplePoint,
    sample_count: usize,
) -> VelloChartEngineStatus {
    clear_last_error();
    let Some(engine) = (unsafe { handle.as_mut() }) else {
        set_last_error("Null engine handle passed to publish_samples");
        return VelloChartEngineStatus::NullPointer;
    };

    let Ok(sample_slice) = slice_from_raw(samples, sample_count) else {
        return VelloChartEngineStatus::NullPointer;
    };

    engine.inner.publish_samples_from_ffi(sample_slice);
    VelloChartEngineStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_chart_engine_set_palette(
    handle: *mut VelloChartEngineHandle,
    palette_ptr: *const VelloChartColor,
    palette_len: usize,
) -> VelloChartEngineStatus {
    clear_last_error();
    let Some(engine) = (unsafe { handle.as_mut() }) else {
        set_last_error("Null engine handle passed to set_palette");
        return VelloChartEngineStatus::NullPointer;
    };

    if palette_len == 0 {
        engine.inner.set_palette(&[]);
        return VelloChartEngineStatus::Success;
    }

    if palette_ptr.is_null() {
        set_last_error("Null palette pointer passed to set_palette");
        return VelloChartEngineStatus::NullPointer;
    }

    let palette_vec: Vec<Color> = unsafe { slice::from_raw_parts(palette_ptr, palette_len) }
        .iter()
        .map(|color| (*color).into())
        .collect();

    engine.inner.set_palette(palette_vec.as_slice());
    VelloChartEngineStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_chart_engine_set_series_definitions(
    handle: *mut VelloChartEngineHandle,
    definitions_ptr: *const VelloChartSeriesDefinition,
    definitions_len: usize,
) -> VelloChartEngineStatus {
    clear_last_error();
    let Some(engine) = (unsafe { handle.as_mut() }) else {
        set_last_error("Null engine handle passed to set_series_definitions");
        return VelloChartEngineStatus::NullPointer;
    };

    let definitions = if definitions_len == 0 {
        &[]
    } else {
        if definitions_ptr.is_null() {
            set_last_error("Null series definition pointer passed to set_series_definitions");
            return VelloChartEngineStatus::NullPointer;
        }
        unsafe { slice::from_raw_parts(definitions_ptr, definitions_len) }
    };

    let mut parsed = Vec::with_capacity(definitions.len());
    for definition in definitions {
        match SeriesDefinitionSpec::try_from_ffi(definition) {
            Ok(spec) => parsed.push((spec.series_id, spec.definition)),
            Err(status) => return status,
        }
    }

    engine.inner.configure_series_definitions(parsed);
    VelloChartEngineStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_chart_engine_apply_series_overrides(
    handle: *mut VelloChartEngineHandle,
    overrides_ptr: *const VelloChartSeriesOverride,
    overrides_len: usize,
) -> VelloChartEngineStatus {
    clear_last_error();
    let Some(engine) = (unsafe { handle.as_mut() }) else {
        set_last_error("Null engine handle passed to apply_series_overrides");
        return VelloChartEngineStatus::NullPointer;
    };

    if overrides_len == 0 {
        return VelloChartEngineStatus::Success;
    }

    if overrides_ptr.is_null() {
        set_last_error("Null overrides pointer passed to apply_series_overrides");
        return VelloChartEngineStatus::NullPointer;
    }

    let overrides = unsafe { slice::from_raw_parts(overrides_ptr, overrides_len) };
    for override_ref in overrides {
        match SeriesOverrideSpec::try_from_ffi(override_ref) {
            Ok(spec) => engine.inner.apply_series_override(spec),
            Err(status) => return status,
        }
    }

    VelloChartEngineStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_chart_engine_render(
    handle: *mut VelloChartEngineHandle,
    scene: *mut VelloSceneHandle,
    width: u32,
    height: u32,
    out_stats: *mut VelloChartFrameStats,
) -> VelloChartEngineStatus {
    clear_last_error();
    let Some(engine) = (unsafe { handle.as_mut() }) else {
        set_last_error("Null engine handle passed to render");
        return VelloChartEngineStatus::NullPointer;
    };
    let Some(scene_handle) = (unsafe { scene.as_mut() }) else {
        set_last_error("Null scene handle passed to render");
        return VelloChartEngineStatus::NullPointer;
    };

    let frame = engine.inner.render_frame(width, height);
    scene_handle.inner.reset();
    scene_handle.inner.append(frame.scene, None);

    if let Some(stats_ptr) = unsafe { out_stats.as_mut() } {
        *stats_ptr = (&frame.stats).into();
    }

    VelloChartEngineStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_chart_engine_last_frame_metadata(
    handle: *mut VelloChartEngineHandle,
    out_metadata: *mut VelloChartFrameMetadata,
) -> VelloChartEngineStatus {
    clear_last_error();
    let Some(engine) = (unsafe { handle.as_mut() }) else {
        set_last_error("Null engine handle passed to last_frame_metadata");
        return VelloChartEngineStatus::NullPointer;
    };
    let Some(out) = (unsafe { out_metadata.as_mut() }) else {
        set_last_error("Null metadata pointer passed to last_frame_metadata");
        return VelloChartEngineStatus::NullPointer;
    };

    let buffer_index = engine
        .inner
        .last_buffer
        .min(engine.inner.frame_buffers.len().saturating_sub(1));
    let buffer = &engine.inner.frame_buffers[buffer_index];
    buffer.metadata.write_to(out);
    VelloChartEngineStatus::Success
}

#[unsafe(no_mangle)]
pub extern "C" fn vello_chart_engine_set_trace_callback(
    callback: Option<TraceCallback>,
) -> VelloChartEngineStatus {
    ensure_tracing_layer();

    let storage = trace_callback_storage();
    if let Ok(mut guard) = storage.write() {
        *guard = callback;
    } else {
        set_last_error("Failed to register trace callback");
        return VelloChartEngineStatus::Unknown;
    }

    VelloChartEngineStatus::Success
}
