use scada_dashboard::{DashboardConfig, simulate_dashboard};

fn main() {
    let config = DashboardConfig::default();
    let stats = simulate_dashboard(&config, 240);
    let payload = serde_json::json!({
        stats.scenario: {
            "frames": stats.frames,
            "avg_cpu_frame_ms": stats.avg_cpu_frame_ms,
            "p99_cpu_frame_ms": stats.p99_cpu_frame_ms,
            "max_cpu_frame_ms": stats.max_cpu_frame_ms,
            "encoded_paths": stats.encoded_paths,
        }
    });

    println!("{}", serde_json::to_string_pretty(&payload).unwrap());
}
