struct Uniforms {
    time: f32,
    amplitude: f32,
    chroma: f32,
};

@group(0) @binding(0)
var<uniform> uniforms: Uniforms;

@fragment
fn fs_main(@location(0) uv: vec2<f32>) -> @location(0) vec4<f32> {
    let ripple = sin(length(uv - vec2<f32>(0.5, 0.5)) * 18.0 - uniforms.time * 3.2);
    let mask = smoothstep(0.58, 0.22, abs(ripple)) * uniforms.amplitude;
    let hue = uniforms.chroma * ripple * 0.35;

    let base = vec3<f32>(0.1, 0.16, 0.28) + vec3<f32>(0.45 + hue, 0.32 - hue * 0.5, 0.62 + hue * 0.8) * mask;
    return vec4<f32>(base, 1.0);
}
