shader_type canvas_item;
render_mode blend_add;

uniform float time_seconds = 0.0;
uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform float fade : hint_range(0.0, 1.0) = 1.0;
uniform float arriving : hint_range(0.0, 1.0) = 0.0;
uniform float shock_kind : hint_range(0.0, 1.0) = 0.0;
uniform vec4 outer_color : source_color = vec4(0.16, 0.72, 1.0, 1.0);
uniform vec4 core_color : source_color = vec4(0.86, 1.0, 1.0, 1.0);
uniform vec4 secondary_color : source_color = vec4(0.54, 0.18, 1.0, 1.0);

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float band(float value, float width) {
	return 1.0 - smoothstep(0.0, width, abs(value));
}

void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	float r = length(uv);
	float a = atan(uv.y, uv.x);

	float origin_start = 1.0 - smoothstep(0.00, 0.30, progress);
	float origin_exit = smoothstep(0.56, 0.92, progress) * (1.0 - smoothstep(0.92, 1.0, progress));
	float mouth_start = 1.0 - smoothstep(0.00, 0.28, progress);
	float mouth_finish = smoothstep(0.60, 0.96, progress) * (1.0 - smoothstep(0.96, 1.0, progress));

	float outbound_pulse = mix(origin_start, mouth_finish, shock_kind);
	float arrival_pulse = mix(origin_exit, mouth_start, shock_kind);
	float pulse = mix(outbound_pulse, arrival_pulse, arriving) * fade;

	float outbound_phase = mix(smoothstep(0.00, 0.30, progress), smoothstep(0.60, 0.96, progress), shock_kind);
	float arrival_phase = mix(smoothstep(0.56, 0.92, progress), smoothstep(0.00, 0.28, progress), shock_kind);
	float phase = mix(outbound_phase, arrival_phase, arriving);

	float wobble = sin(a * 9.0 + time_seconds * 5.6) * 0.018 + sin(a * 17.0 - time_seconds * 3.2) * 0.010;
	float radius = mix(0.12, 0.92, phase) + wobble;
	float ring = band(r - radius, mix(0.050, 0.018, phase));
	float radial = 1.0 - smoothstep(0.68, 1.0, r);
	float arcs = smoothstep(-0.15, 0.92,
		sin(a * 7.0 + time_seconds * 2.4) +
		sin(a * 13.0 - time_seconds * 3.1) * 0.42);
	ring *= 0.18 + arcs * 0.82;
	float spokes = smoothstep(0.88, 1.0, sin(a * 18.0 - time_seconds * 8.0 + hash(vec2(floor(a * 9.0), 1.0)) * 2.0));
	spokes *= band(r - radius * 0.86, 0.11);

	vec3 color = mix(outer_color.rgb, secondary_color.rgb, 0.22) * ring;
	color += core_color.rgb * (ring * 0.55 + spokes * 0.28);
	float alpha = pulse * clamp(ring * 0.46 + spokes * 0.20 + radial * ring * 0.12, 0.0, 0.62);
	COLOR = vec4(color, alpha);
}
