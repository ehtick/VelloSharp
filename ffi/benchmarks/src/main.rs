//! Lightweight benchmark harness capturing baseline scene generation metrics.

use std::env;
use std::process;
use std::time::{Duration, Instant};

use rand::{rngs::StdRng, Rng, SeedableRng};
use serde::Serialize;
use vello_chart_engine::{ChartEngine, ChartSample, EngineOptions};
use vello_composition::{
    DirtyIntent, EasingFunction, EasingTrackDescriptor, RepeatMode, SceneGraphCache,
    TimelineGroupConfig, TimelineSystem,
};

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

#[derive(Debug, Serialize)]
struct AnimationBenchmarkSample {
    scenario: &'static str,
    track_count: usize,
    ticks: usize,
    total_ms: f64,
    avg_tick_ms: f64,
    max_tick_ms: f64,
    samples_emitted: usize,
}

enum Scenario {
    Chart,
    Timeline,
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
                let noise: f64 = rng.random_range(-5.0..=5.0);
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

fn run_timeline_benchmark(track_count: usize, ticks: usize) -> AnimationBenchmarkSample {
    let mut system = TimelineSystem::new();
    let group = system.create_group(TimelineGroupConfig::default());
    let mut cache = SceneGraphCache::new();

    system.set_group_playing(group, true);

    let mut channel: u16 = 0;
    for _ in 0..track_count {
        let node = cache.create_node(None);
        let descriptor = EasingTrackDescriptor {
            node_id: node,
            channel_id: channel,
            repeat: RepeatMode::PingPong,
            easing: EasingFunction::EaseInOutQuad,
            start_value: 0.0,
            end_value: 1.0,
            duration: 0.45,
            dirty_intent: DirtyIntent::None,
        };

        system
            .add_easing_track(group, descriptor)
            .unwrap_or_else(|| panic!("failed to add easing track for channel {channel}"));

        channel = channel.wrapping_add(1);
    }

    let delta = 1.0 / 120.0;
    let mut samples_emitted = 0usize;
    let mut tick_accumulator = Duration::ZERO;
    let mut max_tick = Duration::ZERO;

    // Warm-up tick to populate caches before timing.
    let _ = system.tick(delta, Some(&mut cache));

    let start = Instant::now();
    for _ in 0..ticks {
        let tick_start = Instant::now();
        let samples = system.tick(delta, Some(&mut cache));
        let tick_duration = tick_start.elapsed();

        samples_emitted += samples.len();
        tick_accumulator += tick_duration;
        if tick_duration > max_tick {
            max_tick = tick_duration;
        }
    }

    let total_elapsed = start.elapsed();
    let avg_tick_ms = duration_to_ms(tick_accumulator) / ticks as f64;

    AnimationBenchmarkSample {
        scenario: "timeline_10k_tracks",
        track_count,
        ticks,
        total_ms: duration_to_ms(total_elapsed),
        avg_tick_ms,
        max_tick_ms: duration_to_ms(max_tick),
        samples_emitted,
    }
}

fn parse_scenario(arg: &str) -> Option<Scenario> {
    match arg {
        "chart" => Some(Scenario::Chart),
        "timeline" => Some(Scenario::Timeline),
        other => {
            if let Some(value) = other.strip_prefix("--scenario=") {
                return parse_scenario(value);
            }
            None
        }
    }
}

fn duration_to_ms(duration: Duration) -> f64 {
    duration.as_secs_f64() * 1_000.0
}

fn main() {
    let scenario = match env::args().nth(1) {
        Some(arg) => match parse_scenario(&arg) {
            Some(scenario) => scenario,
            None => {
                eprintln!("Unknown scenario '{arg}'. Expected 'chart' or 'timeline'.");
                process::exit(1);
            }
        },
        None => Scenario::Chart,
    };

    match scenario {
        Scenario::Chart => {
            let sample = run_engine_benchmark(4, 1_024, 120);
            println!("{}", serde_json::to_string_pretty(&sample).unwrap());
        }
        Scenario::Timeline => {
            let sample = run_timeline_benchmark(10_000, 480);
            println!("{}", serde_json::to_string_pretty(&sample).unwrap());
        }
    }
}
