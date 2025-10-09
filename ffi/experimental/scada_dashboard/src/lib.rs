//! Experimental SCADA dashboard scene builder combining chart, gauge, and grid prototypes.

#![allow(clippy::missing_panics_doc)]
#![allow(clippy::missing_errors_doc)]

use std::time::Instant;

use gauges_prototypes::{
    AnalogGaugeConfig, LinearBarConfig, build_analog_gauge_scene, build_vertical_bar_scene,
};
use poc_line_scene::{LineSceneConfig, build_scene as build_chart_scene};
use serde::Serialize;
use vello::{
    Scene,
    kurbo::{Affine, Rect, Stroke, Vec2},
    peniko::{Brush, Color, Fill},
};

/// Configuration for the data grid prototype embedded in the SCADA dashboard.
#[derive(Debug, Clone)]
pub struct GridConfig {
    pub width: f64,
    pub height: f64,
    pub rows: usize,
    pub columns: usize,
    pub header_height: f64,
    pub row_height: f64,
    pub row_gap: f64,
}

impl Default for GridConfig {
    fn default() -> Self {
        Self {
            width: 760.0,
            height: 260.0,
            rows: 8,
            columns: 4,
            header_height: 36.0,
            row_height: 26.0,
            row_gap: 2.0,
        }
    }
}

/// Layout and content configuration for the SCADA dashboard prototype.
#[derive(Debug, Clone)]
pub struct DashboardConfig {
    pub width: f64,
    pub height: f64,
    pub chart: LineSceneConfig,
    pub analog: AnalogGaugeConfig,
    pub bar: LinearBarConfig,
    pub grid: GridConfig,
    pub chart_seed_base: u64,
}

impl Default for DashboardConfig {
    fn default() -> Self {
        Self {
            width: 1280.0,
            height: 720.0,
            chart: LineSceneConfig::default(),
            analog: AnalogGaugeConfig::default(),
            bar: LinearBarConfig::default(),
            grid: GridConfig::default(),
            chart_seed_base: 7,
        }
    }
}

/// Summary statistics captured during simulation runs.
#[derive(Debug, Clone, Serialize)]
pub struct DashboardStats {
    pub scenario: &'static str,
    pub frames: usize,
    pub avg_cpu_frame_ms: f64,
    pub max_cpu_frame_ms: f64,
    pub p99_cpu_frame_ms: f64,
    pub encoded_paths: u32,
}

struct FrameInputs {
    chart_seed: u64,
    analog_value: f64,
    bar_value: f64,
    grid_highlight_row: usize,
}

/// Builds the combined dashboard scene for the given frame inputs.
pub(crate) fn build_dashboard_scene(config: &DashboardConfig, inputs: FrameInputs) -> Scene {
    let mut scene = Scene::new();

    // Compose chart area.
    let mut chart_config = config.chart.clone();
    chart_config.seed = inputs.chart_seed;
    let chart_scene = build_chart_scene(&chart_config);
    let chart_transform = Affine::scale_non_uniform(0.72, 0.48)
        .then_translate(Vec2::new(48.0, 48.0));
    scene.append(&chart_scene, Some(chart_transform));

    // Compose analog gauge.
    let analog_scene = build_analog_gauge_scene(&config.analog, inputs.analog_value);
    let analog_transform = Affine::scale_non_uniform(0.75, 0.75)
        .then_translate(Vec2::new(config.width - 360.0, 64.0));
    scene.append(&analog_scene, Some(analog_transform));

    // Compose bar graph gauge.
    let bar_scene = build_vertical_bar_scene(&config.bar, inputs.bar_value);
    let bar_transform = Affine::scale_non_uniform(0.85, 0.85)
        .then_translate(Vec2::new(config.width - 280.0, config.height - 360.0));
    scene.append(&bar_scene, Some(bar_transform));

    // Compose data grid panel.
    let grid_scene = build_data_grid_scene(&config.grid, inputs.grid_highlight_row);
    let grid_offset_y = config.height - config.grid.height - 48.0;
    let grid_transform = Affine::translate((48.0, grid_offset_y));
    scene.append(&grid_scene, Some(grid_transform));

    // Surrounding frame.
    let frame_rect = Rect::new(24.0, 24.0, config.width - 24.0, config.height - 24.0);
    scene.stroke(
        &Stroke::new(4.0),
        Affine::IDENTITY,
        &Color::from_rgb8(0x36, 0x3F, 0x4A),
        None,
        &frame_rect,
    );

    scene
}

/// Runs the dashboard simulation and produces timing statistics.
pub fn simulate_dashboard(config: &DashboardConfig, frames: usize) -> DashboardStats {
    assert!(frames > 0, "frames must be positive");

    let mut durations = Vec::with_capacity(frames);
    let mut max_paths = 0;

    for frame in 0..frames {
        let phase = frame as f64 / frames as f64;
        let analog_span = config.analog.max_value - config.analog.min_value;
        let analog_value =
            config.analog.min_value + analog_span * (0.5 + 0.5 * (phase * std::f64::consts::TAU).sin());
        let bar_span = config.bar.max_value - config.bar.min_value;
        let bar_value =
            config.bar.min_value + bar_span * (0.5 + 0.5 * (phase * 2.0 * std::f64::consts::PI).cos());
        let inputs = FrameInputs {
            chart_seed: config.chart_seed_base.wrapping_add(frame as u64),
            analog_value,
            bar_value,
            grid_highlight_row: frame % config.grid.rows.max(1),
        };

        let start = Instant::now();
        let scene = build_dashboard_scene(config, inputs);
        let elapsed = start.elapsed().as_secs_f64() * 1_000.0;
        durations.push(elapsed);
        max_paths = max_paths.max(scene.encoding().n_paths);
    }

    durations.sort_by(|a, b| a.partial_cmp(b).unwrap());
    let sum: f64 = durations.iter().sum();
    let avg = sum / frames as f64;
    let max = *durations.last().unwrap();
    let p99_index = ((frames as f64 * 0.99).ceil() as usize).saturating_sub(1);
    let p99 = durations[p99_index];

    DashboardStats {
        scenario: "scada_dashboard",
        frames,
        avg_cpu_frame_ms: avg,
        max_cpu_frame_ms: max,
        p99_cpu_frame_ms: p99,
        encoded_paths: max_paths,
    }
}

fn build_data_grid_scene(config: &GridConfig, highlight_row: usize) -> Scene {
    let mut scene = Scene::new();

    let background = Rect::new(0.0, 0.0, config.width, config.height);
    scene.fill(
        Fill::NonZero,
        Affine::IDENTITY,
        &Brush::Solid(Color::from_rgb8(0x16, 0x1B, 0x20)),
        None,
        &background,
    );

    let header_rect = Rect::new(0.0, 0.0, config.width, config.header_height);
    scene.fill(
        Fill::NonZero,
        Affine::IDENTITY,
        &Brush::Solid(Color::from_rgb8(0x22, 0x28, 0x30)),
        None,
        &header_rect,
    );

    let row_height = config.row_height.max(4.0);
    for row in 0..config.rows {
        let y = config.header_height + row as f64 * row_height;
        if y + row_height > config.height {
            break;
        }
        let row_rect = Rect::new(
            0.0,
            y,
            config.width,
            (y + row_height - config.row_gap).min(config.height),
        );
        let color = if row == highlight_row {
            Color::from_rgb8(0x1E, 0x3A, 0x4C)
        } else if row % 2 == 0 {
            Color::from_rgb8(0x18, 0x1E, 0x26)
        } else {
            Color::from_rgb8(0x14, 0x19, 0x1F)
        };
        scene.fill(
            Fill::NonZero,
            Affine::IDENTITY,
            &Brush::Solid(color),
            None,
            &row_rect,
        );
    }

    // Column separators.
    if config.columns > 1 {
        let column_width = config.width / config.columns as f64;
        for column in 1..config.columns {
            let x = column as f64 * column_width;
            let separator = Rect::new(
                x - 0.5,
                config.header_height,
                x + 0.5,
                config.height,
            );
            scene.fill(
                Fill::NonZero,
                Affine::IDENTITY,
                &Brush::Solid(Color::from_rgb8(0x25, 0x2D, 0x36)),
                None,
                &separator,
            );
        }
    }

    // Outline.
    scene.stroke(
        &Stroke::new(2.0),
        Affine::IDENTITY,
        &Color::from_rgb8(0x34, 0x3E, 0x48),
        None,
        &background,
    );

    scene
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn dashboard_scene_encodes_geometry() {
        let config = DashboardConfig::default();
        let inputs = FrameInputs {
            chart_seed: config.chart_seed_base,
            analog_value: 60.0,
            bar_value: 50.0,
            grid_highlight_row: 2,
        };
        let scene = build_dashboard_scene(&config, inputs);
        assert!(
            scene.encoding().n_paths > 0,
            "dashboard scene should encode geometry",
        );
    }

    #[test]
    fn simulation_produces_stats() {
        let config = DashboardConfig::default();
        let stats = simulate_dashboard(&config, 16);
        assert_eq!(stats.scenario, "scada_dashboard");
        assert!(stats.avg_cpu_frame_ms >= 0.0);
    }
}
