//! Diagnostics primitives shared between Rust and .NET layers.

use serde::{Deserialize, Serialize};
use web_time::Instant;

/// Captures timing metrics for a rendered frame.
#[derive(Debug, Clone, Serialize, Deserialize, Default)]
pub struct FrameStats {
    pub cpu_time_ms: f32,
    pub gpu_time_ms: f32,
    pub queue_latency_ms: f32,
    pub encoded_paths: u32,
    pub timestamp: u128,
}

/// Aggregates frame statistics for later inspection.
#[derive(Debug, Default)]
pub struct DiagnosticsCollector {
    recent: heapless::Deque<FrameStats, 256>,
}

impl DiagnosticsCollector {
    pub fn record(&mut self, stats: FrameStats) {
        if self.recent.is_full() {
            let _ = self.recent.pop_front();
        }
        self.recent.push_back(stats).ok();
    }

    pub fn latest(&self) -> Option<&FrameStats> {
        self.recent.back()
    }
}

/// Helper to compute elapsed milliseconds between instants.
pub fn elapsed_ms(start: Instant, end: Instant) -> f32 {
    (end - start).as_secs_f32() * 1_000.0
}
