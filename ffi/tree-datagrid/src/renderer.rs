use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

#[derive(Clone, Copy, Debug)]
pub struct RendererOptions {
    pub target_fps: f32,
}

impl Default for RendererOptions {
    fn default() -> Self {
        Self { target_fps: 120.0 }
    }
}

#[derive(Clone, Copy, Debug, Default)]
pub struct FrameStats {
    pub frame_index: u64,
    pub cpu_time_ms: f32,
    pub gpu_time_ms: f32,
    pub queue_time_ms: f32,
    pub frame_interval_ms: f32,
    pub timestamp_ms: i64,
    pub gpu_sample_count: u32,
}

#[derive(Clone, Copy, Debug, Default)]
struct GpuTimingSummary {
    gpu_time_ms: f32,
    queue_time_ms: f32,
    sample_count: u32,
}

pub struct RendererLoop {
    target_interval: Duration,
    last_frame_end: Option<Instant>,
    current_frame_start: Option<Instant>,
    frame_index: u64,
    pending_gpu_summary: Option<GpuTimingSummary>,
}

impl RendererLoop {
    pub fn new(options: RendererOptions) -> Self {
        let target_fps = options.target_fps.max(1.0);
        let target_interval = Duration::from_secs_f64(1.0 / target_fps as f64);
        Self {
            target_interval,
            last_frame_end: None,
            current_frame_start: None,
            frame_index: 0,
            pending_gpu_summary: None,
        }
    }

    pub fn begin_frame(&mut self) -> bool {
        let now = Instant::now();
        if let Some(last_start) = self.current_frame_start {
            if now.duration_since(last_start) < self.target_interval {
                return false;
            }
        }

        if let Some(prev_end) = self.last_frame_end {
            if now.duration_since(prev_end) < self.target_interval {
                return false;
            }
        }

        self.current_frame_start = Some(now);
        true
    }

    pub fn record_gpu_summary(&mut self, gpu_time_ms: f32, queue_time_ms: f32, sample_count: u32) {
        self.pending_gpu_summary = Some(GpuTimingSummary {
            gpu_time_ms: gpu_time_ms.max(0.0),
            queue_time_ms: queue_time_ms.max(0.0),
            sample_count,
        });
    }

    pub fn end_frame(&mut self, gpu_time_ms: f32, queue_time_ms: f32) -> FrameStats {
        let now = Instant::now();
        let start = self.current_frame_start.take().unwrap_or(now);
        let cpu_elapsed = now.duration_since(start);
        let interval = if let Some(prev_end) = self.last_frame_end {
            now.duration_since(prev_end)
        } else {
            self.target_interval
        };

        self.last_frame_end = Some(now);

        let timestamp_ms = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map(|duration| duration.as_millis().min(i64::MAX as u128) as i64)
            .unwrap_or_default();

        let pending = self.pending_gpu_summary.take();
        let (effective_gpu_ms, effective_queue_ms, sample_count) = if let Some(summary) = pending {
            (
                summary.gpu_time_ms,
                summary.queue_time_ms,
                summary.sample_count,
            )
        } else {
            (gpu_time_ms, queue_time_ms, 0)
        };

        let stats = FrameStats {
            frame_index: self.frame_index,
            cpu_time_ms: (cpu_elapsed.as_secs_f64() * 1_000.0) as f32,
            gpu_time_ms: effective_gpu_ms,
            queue_time_ms: effective_queue_ms,
            frame_interval_ms: (interval.as_secs_f64() * 1_000.0) as f32,
            timestamp_ms,
            gpu_sample_count: sample_count,
        };

        self.frame_index = self.frame_index.wrapping_add(1);
        stats
    }
}
