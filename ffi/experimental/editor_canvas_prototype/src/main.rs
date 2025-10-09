use editor_canvas_prototype::{CanvasConfig, simulate_editor_session};

fn main() {
    let stats = simulate_editor_session(CanvasConfig::default());
    let payload = serde_json::json!({
        stats.scenario: {
            "operations": stats.operations,
            "avg_cpu_frame_ms": stats.avg_cpu_frame_ms,
            "p99_cpu_frame_ms": stats.p99_cpu_frame_ms,
            "max_cpu_frame_ms": stats.max_cpu_frame_ms,
            "encoded_paths": stats.encoded_paths,
        }
    });

    println!("{}", serde_json::to_string_pretty(&payload).unwrap());
}
