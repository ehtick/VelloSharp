//! Experimental gauge scene builders used during Phase 0 discovery.

#![allow(clippy::missing_panics_doc)]
#![allow(clippy::missing_errors_doc)]

use std::{f64::consts::PI, time::Instant};

use rand::{rngs::StdRng, Rng, SeedableRng};
use serde::Serialize;
use vello::{
    Scene,
    kurbo::{Affine, BezPath, Circle, Point, Rect, Stroke},
    peniko::{Brush, Color, Fill},
};

/// Configuration for building an analog (radial) gauge.
#[derive(Debug, Clone)]
pub struct AnalogGaugeConfig {
    /// Width and height of the square viewport in logical pixels.
    pub size: f64,
    /// Minimum value represented by the gauge.
    pub min_value: f64,
    /// Maximum value represented by the gauge.
    pub max_value: f64,
    /// Number of major tick marks (inclusive of min and max).
    pub major_tick_count: usize,
    /// Number of minor ticks between each pair of major ticks.
    pub minor_ticks_per_major: usize,
    /// Total sweep of the dial in degrees.
    pub sweep_degrees: f64,
    /// Starting angle (degrees) measured clockwise from the positive X axis.
    pub start_angle_degrees: f64,
}

impl Default for AnalogGaugeConfig {
    fn default() -> Self {
        Self {
            size: 320.0,
            min_value: 0.0,
            max_value: 120.0,
            major_tick_count: 12,
            minor_ticks_per_major: 5,
            sweep_degrees: 240.0,
            start_angle_degrees: -210.0,
        }
    }
}

/// Configuration for building a vertical bar graph gauge.
#[derive(Debug, Clone)]
pub struct LinearBarConfig {
    /// Width of the bar graph in logical pixels.
    pub width: f64,
    /// Height of the bar graph in logical pixels.
    pub height: f64,
    /// Minimum telemetry value.
    pub min_value: f64,
    /// Maximum telemetry value.
    pub max_value: f64,
    /// Value representing an alarm threshold (optional highlight band).
    pub alarm_threshold: Option<f64>,
}

impl Default for LinearBarConfig {
    fn default() -> Self {
        Self {
            width: 180.0,
            height: 320.0,
            min_value: 0.0,
            max_value: 100.0,
            alarm_threshold: Some(85.0),
        }
    }
}

/// Summary statistics captured during prototype simulation.
#[derive(Debug, Clone, Serialize)]
pub struct PrototypeStats {
    pub scenario: &'static str,
    pub frames: usize,
    pub avg_cpu_frame_ms: f64,
    pub max_cpu_frame_ms: f64,
    pub p99_cpu_frame_ms: f64,
    pub encoded_paths: u32,
}

/// Builds a Vello scene representing an analog gauge at the specified value.
pub fn build_analog_gauge_scene(config: &AnalogGaugeConfig, value: f64) -> Scene {
    let mut scene = Scene::new();

    let center = Point::new(config.size * 0.5, config.size * 0.5);
    let radius = config.size * 0.45;
    let inner_radius = radius * 0.75;
    let background = Brush::Solid(Color::from_rgb8(18, 22, 27));
    let dial_fill = Brush::Solid(Color::from_rgb8(28, 34, 40));
    let accent = Color::from_rgb8(0x5A, 0xC8, 0xFF);
    let alarm_color = Color::from_rgb8(0xFF, 0x5A, 0x5F);
    let tick_color = Color::from_rgb8(0xC7, 0xCF, 0xD6);

    // Outer bezel.
    let bezel = Circle::new(center, radius + 6.0);
    scene.fill(Fill::NonZero, Affine::IDENTITY, &background, None, &bezel);
    scene.stroke(&Stroke::new(4.0), Affine::IDENTITY, &tick_color, None, &bezel);

    // Dial background.
    let dial = Circle::new(center, radius);
    scene.fill(Fill::NonZero, Affine::IDENTITY, &dial_fill, None, &dial);

    // Warning arc (upper 20 percent).
    let arc_start = config.start_angle_degrees.to_radians();
    let sweep_radians = config.sweep_degrees.to_radians();
    let warn_start_angle = arc_start + sweep_radians * 0.8;
    let warn_end_angle = arc_start + sweep_radians;
    let warning_path = build_arc(center, inner_radius + 10.0, warn_start_angle, warn_end_angle);
    scene.stroke(&Stroke::new(12.0), Affine::IDENTITY, &alarm_color, None, &warning_path);

    // Major ticks.
    for major in 0..=config.major_tick_count {
        let angle = arc_start
            + sweep_radians * (major as f64 / config.major_tick_count as f64);
        let tick_path = build_tick(center, radius, inner_radius, angle);
        scene.stroke(&Stroke::new(3.2), Affine::IDENTITY, &tick_color, None, &tick_path);

        if major < config.major_tick_count {
            for minor in 1..config.minor_ticks_per_major {
                let minor_ratio = minor as f64 / config.minor_ticks_per_major as f64;
                let minor_angle = angle
                    + sweep_radians * minor_ratio / config.major_tick_count as f64;
                let minor_path = build_tick(center, radius, radius - 14.0, minor_angle);
                scene.stroke(&Stroke::new(1.6), Affine::IDENTITY, &tick_color, None, &minor_path);
            }
        }
    }

    // Needle.
    let clamped_value = value.clamp(config.min_value, config.max_value);
    let normalized = (clamped_value - config.min_value)
        / (config.max_value - config.min_value).max(f64::EPSILON);
    let needle_angle = arc_start + sweep_radians * normalized;
    let needle_length = inner_radius;
    let needle_back = inner_radius * 0.2;

    let front = polar_to_point(center, needle_angle, needle_length);
    let back_left = polar_to_point(center, needle_angle + (8.0_f64.to_radians()), needle_back);
    let back_right = polar_to_point(center, needle_angle - (8.0_f64.to_radians()), needle_back);

    let mut needle = BezPath::new();
    needle.move_to(front);
    needle.line_to(back_left);
    needle.line_to(back_right);
    needle.close_path();

    let needle_brush = Brush::Solid(accent);
    scene.fill(Fill::NonZero, Affine::IDENTITY, &needle_brush, None, &needle);

    // Needle hub.
    let hub = Circle::new(center, 9.5);
    scene.fill(
        Fill::NonZero,
        Affine::IDENTITY,
        &Brush::Solid(Color::from_rgb8(0x12, 0x15, 0x18)),
        None,
        &hub,
    );
    scene.stroke(&Stroke::new(2.0), Affine::IDENTITY, &accent, None, &hub);

    scene
}

/// Builds a Vello scene representing a vertical bar graph with alarm band.
pub fn build_vertical_bar_scene(config: &LinearBarConfig, value: f64) -> Scene {
    let mut scene = Scene::new();
    let frame_rect = Rect::new(0.0, 0.0, config.width, config.height);
    let frame_brush = Brush::Solid(Color::from_rgb8(18, 22, 27));
    scene.fill(Fill::NonZero, Affine::IDENTITY, &frame_brush, None, &frame_rect);

    let border = frame_rect.inset(-4.0);
    scene.stroke(
        &Stroke::new(4.0),
        Affine::IDENTITY,
        &Color::from_rgb8(0xC7, 0xCF, 0xD6),
        None,
        &border,
    );

    if let Some(threshold) = config.alarm_threshold {
        let threshold_ratio = normalize_value(threshold, config.min_value, config.max_value);
        let threshold_height = config.height * threshold_ratio;
        let alarm_rect = Rect::new(
            12.0,
            config.height - threshold_height,
            config.width - 12.0,
            config.height - threshold_height + 18.0,
        );
        let alarm_brush = Brush::Solid(Color::from_rgb8(0xFF, 0x5A, 0x5F));
        scene.fill(Fill::NonZero, Affine::IDENTITY, &alarm_brush, None, &alarm_rect);
    }

    let normalized_value = normalize_value(value, config.min_value, config.max_value);
    let fill_height = config.height * normalized_value;
    let value_rect = Rect::new(
        20.0,
        config.height - fill_height,
        config.width - 20.0,
        config.height - 20.0,
    );
    let value_brush = Brush::Solid(Color::from_rgb8(0x5A, 0xC8, 0xFF));
    scene.fill(Fill::NonZero, Affine::IDENTITY, &value_brush, None, &value_rect);

    // Gridlines every 10 percent.
    for step in 1..10 {
        let ratio = step as f64 / 10.0;
        let y = config.height - (config.height * ratio);
        let mut grid_path = BezPath::new();
        grid_path.move_to(Point::new(16.0, y));
        grid_path.line_to(Point::new(config.width - 16.0, y));
        scene.stroke(&Stroke::new(1.0), Affine::IDENTITY, &Color::from_rgb8(0x3A, 0x42, 0x4A), None, &grid_path);
    }

    scene
}

/// Runs an analog gauge simulation and returns performance statistics.
pub fn simulate_analog_gauge(
    config: &AnalogGaugeConfig,
    frame_count: usize,
) -> PrototypeStats {
    simulate_internal(
        "analog_dial",
        frame_count,
        |frame| {
            let phase = frame as f64 / frame_count.max(1) as f64;
            let value = config.min_value
                + (config.max_value - config.min_value) * (0.5 + 0.5 * (phase * PI * 2.0).sin());
            build_analog_gauge_scene(config, value)
        },
    )
}

/// Runs a vertical bar graph simulation and returns performance statistics.
pub fn simulate_vertical_bar(
    config: &LinearBarConfig,
    frame_count: usize,
) -> PrototypeStats {
    let mut rng = StdRng::seed_from_u64(42);
    simulate_internal(
        "vertical_bargraph",
        frame_count,
        |_| {
            let jitter: f64 = rng.random_range(-0.05..=0.05);
            let base: f64 = rng.random_range(0.2..=0.85);
            let clamped = (base + jitter).clamp(0.0_f64, 1.0_f64);
            let value =
                config.min_value + (config.max_value - config.min_value) * clamped;
            build_vertical_bar_scene(config, value)
        },
    )
}

fn simulate_internal<F>(scenario: &'static str, frame_count: usize, mut builder: F) -> PrototypeStats
where
    F: FnMut(usize) -> Scene,
{
    assert!(frame_count > 0, "frame_count must be greater than zero");
    let mut durations = Vec::with_capacity(frame_count);
    let mut max_paths = 0;

    for frame in 0..frame_count {
        let start = Instant::now();
        let scene = builder(frame);
        let elapsed = start.elapsed().as_secs_f64() * 1_000.0;
        durations.push(elapsed);
        max_paths = max_paths.max(scene.encoding().n_paths);
    }

    durations.sort_by(|a, b| a.partial_cmp(b).unwrap());
    let sum: f64 = durations.iter().sum();
    let avg = sum / frame_count as f64;
    let max = *durations.last().unwrap();
    let p99_index = ((frame_count as f64 * 0.99).ceil() as usize).saturating_sub(1);
    let p99 = durations[p99_index];

    PrototypeStats {
        scenario,
        frames: frame_count,
        avg_cpu_frame_ms: avg,
        max_cpu_frame_ms: max,
        p99_cpu_frame_ms: p99,
        encoded_paths: max_paths,
    }
}

fn build_tick(center: Point, outer_radius: f64, inner_radius: f64, angle: f64) -> BezPath {
    let start = polar_to_point(center, angle, outer_radius);
    let end = polar_to_point(center, angle, inner_radius);
    let mut path = BezPath::new();
    path.move_to(start);
    path.line_to(end);
    path
}

fn build_arc(center: Point, radius: f64, start_angle: f64, end_angle: f64) -> BezPath {
    let mut path = BezPath::new();
    let start = polar_to_point(center, start_angle, radius);
    path.move_to(start);
    let sweep = end_angle - start_angle;
    let segments = (sweep.abs() / (PI / 6.0)).ceil().max(1.0) as usize;
    for segment in 1..=segments {
        let t = segment as f64 / segments as f64;
        let angle = start_angle + sweep * t;
        let point = polar_to_point(center, angle, radius);
        path.line_to(point);
    }
    path
}

fn normalize_value(value: f64, min: f64, max: f64) -> f64 {
    if max <= min {
        return 0.0;
    }
    ((value - min) / (max - min)).clamp(0.0, 1.0)
}

fn polar_to_point(center: Point, angle: f64, radius: f64) -> Point {
    Point::new(
        center.x + radius * angle.cos(),
        center.y + radius * angle.sin(),
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn analog_scene_encodes_geometry() {
        let config = AnalogGaugeConfig::default();
        let scene = build_analog_gauge_scene(&config, 60.0);
        assert!(
            scene.encoding().n_paths > 0,
            "analog gauge scene should encode stroke geometry",
        );
    }

    #[test]
    fn vertical_bar_scene_encodes_geometry() {
        let config = LinearBarConfig::default();
        let scene = build_vertical_bar_scene(&config, 50.0);
        assert!(
            scene.encoding().n_paths > 0,
            "bar graph scene should encode stroke geometry",
        );
    }

    #[test]
    fn simulations_produce_stats() {
        let analog_stats = simulate_analog_gauge(&AnalogGaugeConfig::default(), 12);
        assert_eq!(analog_stats.scenario, "analog_dial");
        let bar_stats = simulate_vertical_bar(&LinearBarConfig::default(), 12);
        assert_eq!(bar_stats.scenario, "vertical_bargraph");
    }
}
