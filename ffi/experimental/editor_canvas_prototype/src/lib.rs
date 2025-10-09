//! Prototype simulating drag/drop operations in the unified visual editor.

#![allow(clippy::missing_panics_doc)]
#![allow(clippy::missing_errors_doc)]

use std::time::Instant;

use gauges_prototypes::{
    AnalogGaugeConfig, LinearBarConfig, build_analog_gauge_scene, build_vertical_bar_scene,
};
use poc_line_scene::{LineSceneConfig, build_scene as build_chart_scene};
use rand::{Rng, SeedableRng, rngs::StdRng};
use serde::Serialize;
use vello::{
    Scene,
    kurbo::{Affine, Rect, Stroke, Vec2},
    peniko::{Brush, Color, Fill},
};

const CHART_BASE_WIDTH: f64 = 1024.0;
const CHART_BASE_HEIGHT: f64 = 480.0;
const ANALOG_BASE_SIZE: f64 = 320.0;
const BAR_BASE_WIDTH: f64 = 180.0;
const BAR_BASE_HEIGHT: f64 = 320.0;

/// Kind of control dropped on the canvas.
#[derive(Debug, Clone, Copy)]
pub enum ControlKind {
    Chart,
    AnalogGauge,
    BarGauge,
    Panel,
}

/// A simulated drop/placement operation.
#[derive(Debug, Clone)]
pub struct DropOperation {
    pub kind: ControlKind,
    pub position: Vec2,
    pub size: Vec2,
    pub seed: u64,
}

/// Canvas configuration used during simulation.
#[derive(Debug, Clone)]
pub struct CanvasConfig {
    pub width: f64,
    pub height: f64,
    pub operations: Vec<DropOperation>,
    pub analog_config: AnalogGaugeConfig,
    pub bar_config: LinearBarConfig,
    pub chart_config: LineSceneConfig,
}

impl Default for CanvasConfig {
    fn default() -> Self {
        Self {
            width: 1400.0,
            height: 840.0,
            operations: Vec::new(),
            analog_config: AnalogGaugeConfig::default(),
            bar_config: LinearBarConfig::default(),
            chart_config: LineSceneConfig::default(),
        }
    }
}

/// Statistics returned from a simulation run.
#[derive(Debug, Clone, Serialize)]
pub struct EditorStats {
    pub scenario: &'static str,
    pub operations: usize,
    pub avg_cpu_frame_ms: f64,
    pub max_cpu_frame_ms: f64,
    pub p99_cpu_frame_ms: f64,
    pub encoded_paths: u32,
}

/// Simulates a drag/drop session, measuring scene rebuild cost per operation.
pub fn simulate_editor_session(mut config: CanvasConfig) -> EditorStats {
    if config.operations.is_empty() {
        config.operations = generate_operations(&config, 24);
    }

    let mut durations = Vec::with_capacity(config.operations.len());
    let mut max_paths = 0;

    for op_count in 1..=config.operations.len() {
        let start = Instant::now();
        let scene = build_scene_from_operations(&config, op_count);
        let elapsed = start.elapsed().as_secs_f64() * 1_000.0;
        durations.push(elapsed);
        max_paths = max_paths.max(scene.encoding().n_paths);
    }

    durations.sort_by(|a, b| a.partial_cmp(b).unwrap());
    let sum: f64 = durations.iter().sum();
    let avg = sum / durations.len() as f64;
    let max = *durations.last().unwrap();
    let p99_index =
        ((durations.len() as f64 * 0.99).ceil() as usize).saturating_sub(1);
    let p99 = durations[p99_index];

    EditorStats {
        scenario: "editor_canvas",
        operations: config.operations.len(),
        avg_cpu_frame_ms: avg,
        max_cpu_frame_ms: max,
        p99_cpu_frame_ms: p99,
        encoded_paths: max_paths,
    }
}

fn generate_operations(config: &CanvasConfig, count: usize) -> Vec<DropOperation> {
    let mut rng = StdRng::seed_from_u64(42);
    (0..count)
        .map(|index| {
            let kind = match index % 4 {
                0 => ControlKind::Chart,
                1 => ControlKind::AnalogGauge,
                2 => ControlKind::BarGauge,
                _ => ControlKind::Panel,
            };
            let w = match kind {
                ControlKind::Chart => rng.random_range(420.0..=520.0),
                ControlKind::AnalogGauge => rng.random_range(280.0..=340.0),
                ControlKind::BarGauge => rng.random_range(220.0..=260.0),
                ControlKind::Panel => rng.random_range(300.0..=420.0),
            };
            let h = match kind {
                ControlKind::Chart => rng.random_range(260.0..=320.0),
                ControlKind::AnalogGauge => rng.random_range(280.0..=340.0),
                ControlKind::BarGauge => rng.random_range(280.0..=360.0),
                ControlKind::Panel => rng.random_range(120.0..=180.0),
            };
            let padding = 32.0;
            let max_x = (config.width - w - padding).max(padding);
            let max_y = (config.height - h - padding).max(padding);
            let x = rng.random_range(padding..=max_x);
            let y = rng.random_range(padding..=max_y);
            DropOperation {
                kind,
                position: Vec2::new(x, y),
                size: Vec2::new(w, h),
                seed: rng.random(),
            }
        })
        .collect()
}

fn build_scene_from_operations(config: &CanvasConfig, op_count: usize) -> Scene {
    let mut scene = Scene::new();

    // Background grid
    let background = Rect::new(0.0, 0.0, config.width, config.height);
    scene.fill(
        Fill::NonZero,
        Affine::IDENTITY,
        &Brush::Solid(Color::from_rgb8(0x14, 0x1A, 0x20)),
        None,
        &background,
    );
    scene.stroke(
        &Stroke::new(3.0),
        Affine::IDENTITY,
        &Color::from_rgb8(0x2A, 0x33, 0x3C),
        None,
        &background,
    );

    for op in config.operations.iter().take(op_count) {
        match op.kind {
            ControlKind::Chart => append_chart(config, &mut scene, op),
            ControlKind::AnalogGauge => append_analog_gauge(config, &mut scene, op),
            ControlKind::BarGauge => append_bar_gauge(config, &mut scene, op),
            ControlKind::Panel => append_panel(&mut scene, op),
        }
        append_selection_bounds(&mut scene, op);
    }

    scene
}

fn append_chart(config: &CanvasConfig, scene: &mut Scene, op: &DropOperation) {
    let mut chart_config = config.chart_config.clone();
    chart_config.seed = op.seed;
    chart_config.series_count = 3;
    chart_config.points_per_series = 256;
    let chart_scene = build_chart_scene(&chart_config);
    let scale_x = op.size.x / CHART_BASE_WIDTH;
    let scale_y = op.size.y / CHART_BASE_HEIGHT;
    let transform = Affine::scale_non_uniform(scale_x, scale_y)
        .then_translate(Vec2::new(op.position.x, op.position.y));
    scene.append(&chart_scene, Some(transform));
}

fn append_analog_gauge(config: &CanvasConfig, scene: &mut Scene, op: &DropOperation) {
    let gauge_scene = build_analog_gauge_scene(&config.analog_config, 60.0);
    let scale = (op.size.x.min(op.size.y) / ANALOG_BASE_SIZE).min(1.5);
    let transform = Affine::scale_non_uniform(scale, scale)
        .then_translate(Vec2::new(op.position.x, op.position.y));
    scene.append(&gauge_scene, Some(transform));
}

fn append_bar_gauge(config: &CanvasConfig, scene: &mut Scene, op: &DropOperation) {
    let bar_scene = build_vertical_bar_scene(&config.bar_config, 50.0);
    let scale_x = op.size.x / BAR_BASE_WIDTH;
    let scale_y = op.size.y / BAR_BASE_HEIGHT;
    let transform = Affine::scale_non_uniform(scale_x, scale_y)
        .then_translate(Vec2::new(op.position.x, op.position.y));
    scene.append(&bar_scene, Some(transform));
}

fn append_panel(scene: &mut Scene, op: &DropOperation) {
    let rect = Rect::new(
        op.position.x,
        op.position.y,
        op.position.x + op.size.x,
        op.position.y + op.size.y,
    );
    scene.fill(
        Fill::NonZero,
        Affine::IDENTITY,
        &Brush::Solid(Color::from_rgb8(0x1F, 0x28, 0x30)),
        None,
        &rect,
    );
    scene.stroke(
        &Stroke::new(2.0),
        Affine::IDENTITY,
        &Color::from_rgb8(0x37, 0x43, 0x4E),
        None,
        &rect,
    );
}

fn append_selection_bounds(scene: &mut Scene, op: &DropOperation) {
    let rect = Rect::new(
        op.position.x - 4.0,
        op.position.y - 4.0,
        op.position.x + op.size.x + 4.0,
        op.position.y + op.size.y + 4.0,
    );
    scene.stroke(
        &Stroke::new(1.0),
        Affine::IDENTITY,
        &Color::from_rgb8(0x4B, 0x9B, 0xFF),
        None,
        &rect,
    );
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn scene_contains_geometry() {
        let mut config = CanvasConfig::default();
        config.operations = generate_operations(&config, 8);
        let scene = build_scene_from_operations(&config, 4);
        assert!(
            scene.encoding().n_paths > 0,
            "scene should contain strokes/fills"
        );
    }

    #[test]
    fn simulation_returns_stats() {
        let stats = simulate_editor_session(CanvasConfig::default());
        assert_eq!(stats.scenario, "editor_canvas");
        assert!(stats.operations > 0);
    }
}
