//! Prototype line scene generator used to validate dynamic scene updates for real-time charts.

#![allow(clippy::missing_panics_doc)]
#![allow(clippy::missing_errors_doc)]

use rand::{Rng, SeedableRng, rngs::StdRng};
use vello::{
    Scene,
    kurbo::{Affine, BezPath, Point, Stroke},
    peniko::Color,
};

/// Configuration for generating a prototype line scene.
#[derive(Debug, Clone)]
pub struct LineSceneConfig {
    /// Number of independent line series.
    pub series_count: usize,
    /// Number of points in each series.
    pub points_per_series: usize,
    /// Seed applied to the pseudo random generator to keep output deterministic.
    pub seed: u64,
}

impl Default for LineSceneConfig {
    fn default() -> Self {
        Self {
            series_count: 4,
            points_per_series: 1_024,
            seed: 42,
        }
    }
}

/// Builds a Vello scene with a configurable set of polyline series.
pub fn build_scene(config: &LineSceneConfig) -> Scene {
    let mut scene = Scene::new();
    let mut rng = StdRng::seed_from_u64(config.seed);

    for series in 0..config.series_count {
        let mut path = BezPath::new();
        for point_idx in 0..config.points_per_series {
            let x = point_idx as f64;
            let noise: f64 = rng.random_range(-5.0..=5.0);
            let y = (series * 12) as f64 + noise + (point_idx as f64 * 0.15).sin() * 8.0;
            let pt = Point::new(x, y);
            if point_idx == 0 {
                path.move_to(pt);
            } else {
                path.line_to(pt);
            }
        }

        let hue = 0.12 * series as f32;
        let color = color_from_hue(hue);
        scene.stroke(&Stroke::new(1.5), Affine::IDENTITY, &color, None, &path);
    }

    scene
}

fn color_from_hue(hue: f32) -> Color {
    let r = (hue.sin() * 0.5 + 0.5).clamp(0.0, 1.0);
    let g = ((hue + 2.094).sin() * 0.5 + 0.5).clamp(0.0, 1.0);
    let b = ((hue + 4.188).sin() * 0.5 + 0.5).clamp(0.0, 1.0);
    Color::from_rgb8(
        (r * 255.0).round() as u8,
        (g * 255.0).round() as u8,
        (b * 255.0).round() as u8,
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn generates_scene_with_expected_geometry() {
        let config = LineSceneConfig {
            series_count: 2,
            points_per_series: 16,
            seed: 99,
        };
        let scene = build_scene(&config);
        assert!(
            scene.encoding().n_paths > 0,
            "scene should encode at least one stroked path"
        );
    }
}
