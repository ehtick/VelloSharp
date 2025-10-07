//! Lightweight benchmark harness capturing baseline scene generation metrics.

use std::time::{Duration, Instant};

use rand::{Rng, SeedableRng, rngs::StdRng};
use serde::Serialize;
use vello_chart_engine::{ChartEngine, ChartSample, EngineOptions};

#[derive(Debug, Serialize)]
struct BenchmarkSample {
    series_count: usize,
    points_per_series: usize,
    iterations: usize,
    total_ms: f64,
    avg_ms: f64,
    avg_cpu_frame_ms: f64,
    encoded_paths: u32,
}

fn run_engine_benchmark(
    series_count: usize,
    points_per_series: usize,
    iterations: usize,
) -> BenchmarkSample {
    let mut engine = ChartEngine::new(EngineOptions::default());
    let mut encoded_paths = 0;
    let mut cpu_accumulator = 0f64;
    let mut rng = StdRng::seed_from_u64(42);
    let mut scratch_samples = Vec::with_capacity(series_count * points_per_series);

    let start = Instant::now();
    for frame_index in 0..iterations {
        scratch_samples.clear();

        for series in 0..series_count {
            for point_idx in 0..points_per_series {
                let noise: f64 = rng.gen_range(-5.0..=5.0);
                let value = (series as f64 * 12.0) + noise + (point_idx as f64 * 0.15).sin() * 8.0;
                let timestamp_seconds = frame_index as f64 + (point_idx as f64 * 0.001);

                scratch_samples.push(ChartSample {
                    series_id: series as u32,
                    timestamp_seconds,
                    value,
                });
            }
        }

        engine.publish_samples(&scratch_samples);
        let frame = engine.render_frame(1_280, 720);
        encoded_paths = encoded_paths.max(frame.stats.encoded_paths);
        cpu_accumulator += frame.stats.cpu_time_ms as f64;
    }

    let total_ms = duration_to_ms(start.elapsed());
    let avg_ms = total_ms / iterations as f64;
    let avg_cpu_frame_ms = cpu_accumulator / iterations as f64;

    BenchmarkSample {
        series_count,
        points_per_series,
        iterations,
        total_ms,
        avg_ms,
        avg_cpu_frame_ms,
        encoded_paths,
    }
}

fn duration_to_ms(duration: Duration) -> f64 {
    duration.as_secs_f64() * 1_000.0
}

fn main() {
    let sample = run_engine_benchmark(4, 1_024, 120);
    println!("{}", serde_json::to_string_pretty(&sample).unwrap());
}
