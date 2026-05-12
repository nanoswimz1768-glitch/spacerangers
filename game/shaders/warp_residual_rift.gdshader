shader_type canvas_item;
render_mode blend_add;

uniform float time_seconds = 0.0;
uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform float fade : hint_range(0.0, 1.0) = 1.0;
uniform float arriving : hint_range(0.0, 1.0) = 0.0;
uniform float residual_level : hint_range(0.0, 1.0) = 0.0;
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
	float active = residual_level * fade;
	float collapse = smoothstep(0.62, 0.96, progress);
	float base_radius = mix(0.68, 0.30, collapse);
	float wobble = sin(a * 6.0 + time_seconds * 1.8) * 0.045 + sin(a * 15.0 - time_seconds * 2.7) * 0.018;
	float ring = band(r - (base_radius + wobble), 0.050);
	float arcs = smoothstep(-0.10, 0.94, sin(a * 8.0 + time_seconds * 4.0) + sin(a * 17.0 - time_seconds * 3.2) * 0.38);
	float sparks = step(0.970, hash(vec2(floor(a * 28.0), floor(r * 18.0 - time_seconds * 3.0))));
	sparks *= band(r - base_radius * (0.72 + hash(vec2(floor(a * 28.0), 5.0)) * 0.36), 0.035);
	float core_smoke = (1.0 - smoothstep(0.08, 0.70, r)) * smoothstep(0.02, 0.16, r);

	vec3 color = outer_color.rgb * ring * arcs * 0.82;
	color += secondary_color.rgb * ring * 0.36;
	color += core_color.rgb * sparks * 1.30;
	color += mix(outer_color.rgb, core_color.rgb, 0.42) * core_smoke * 0.18;
	float alpha = active * clamp(ring * arcs * 0.60 + sparks * 0.36 + core_smoke * 0.12, 0.0, 0.82);
	COLOR = vec4(color, alpha);
}
