use gauges_prototypes::{
    AnalogGaugeConfig, LinearBarConfig, simulate_analog_gauge, simulate_vertical_bar,
};

fn main() {
    let analog_config = AnalogGaugeConfig::default();
    let bar_config = LinearBarConfig::default();

    let analog_stats = simulate_analog_gauge(&analog_config, 240);
    let bar_stats = simulate_vertical_bar(&bar_config, 240);

    let payload = serde_json::json!({
        "analog_dial": {
            "frames": analog_stats.frames,
            "avg_cpu_frame_ms": analog_stats.avg_cpu_frame_ms,
            "max_cpu_frame_ms": analog_stats.max_cpu_frame_ms,
            "p99_cpu_frame_ms": analog_stats.p99_cpu_frame_ms,
            "encoded_paths": analog_stats.encoded_paths,
        },
        "vertical_bargraph": {
            "frames": bar_stats.frames,
            "avg_cpu_frame_ms": bar_stats.avg_cpu_frame_ms,
            "max_cpu_frame_ms": bar_stats.max_cpu_frame_ms,
            "p99_cpu_frame_ms": bar_stats.p99_cpu_frame_ms,
            "encoded_paths": bar_stats.encoded_paths,
        },
    });

    println!("{}", serde_json::to_string_pretty(&payload).unwrap());
}
