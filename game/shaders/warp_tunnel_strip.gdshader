shader_type canvas_item;
render_mode blend_add;

uniform float time_seconds = 0.0;
uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform float fade : hint_range(0.0, 1.0) = 1.0;
uniform float arriving : hint_range(0.0, 1.0) = 0.0;
uniform vec4 outer_color : source_color = vec4(0.16, 0.72, 1.0, 1.0);
uniform vec4 core_color : source_color = vec4(0.86, 1.0, 1.0, 1.0);
uniform vec4 secondary_color : source_color = vec4(0.54, 0.18, 1.0, 1.0);
uniform vec4 shadow_color : source_color = vec4(0.0, 0.006, 0.025, 1.0);

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	float a = hash(i);
	float b = hash(i + vec2(1.0, 0.0));
	float c = hash(i + vec2(0.0, 1.0));
	float d = hash(i + vec2(1.0, 1.0));
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm(vec2 p) {
	float v = 0.0;
	float amp = 0.5;
	for (int i = 0; i < 4; i++) {
		v += noise(p) * amp;
		p = mat2(vec2(1.6, 1.1), vec2(-1.1, 1.6)) * p + vec2(4.3, 2.7);
		amp *= 0.52;
	}
	return v;
}

float band(float value, float width) {
	return 1.0 - smoothstep(0.0, width, abs(value));
}

void fragment() {
	vec2 p = vec2(UV.x * 2.0 - 1.0, UV.y);
	float x = p.x;
	float y = p.y;
	float open = smoothstep(0.02, 0.18, progress) * (1.0 - smoothstep(0.94, 1.0, progress));
	float energy = open * fade;
	float flow = mix(1.0, -1.0, arriving);
	float t = time_seconds;

	float side = abs(x);
	float tube_mask = 1.0 - smoothstep(0.90, 1.0, side);
	float wall_mask = smoothstep(0.22, 0.84, side) * (1.0 - smoothstep(0.91, 1.0, side));
	float edge_wall = band(side - 0.74, 0.08);
	float spine = 1.0 - smoothstep(0.04, 0.26, side);
	float perspective = smoothstep(0.015, 0.16, y) * (1.0 - smoothstep(0.88, 1.0, y));

	float n = fbm(vec2(x * 2.1 + t * 0.16, y * 6.4 - t * 1.7 * flow));
	float wall_glow = wall_mask * smoothstep(0.10, 0.78, y) * (1.0 - smoothstep(0.90, 1.0, y)) * (0.46 + n * 0.54);
	float long_wave = sin((x * 6.6 + n * 3.4) + y * 18.0 - t * 7.2 * flow);
	float filaments = pow(smoothstep(0.70, 1.0, long_wave), 2.25) * wall_mask;

	float ring_phase = sin(y * 58.0 - t * 14.0 * flow + n * 4.6);
	float depth_rings = smoothstep(0.82, 1.0, ring_phase) * (0.18 + side * 0.82) * wall_mask;

	float swirl = sin((x * 2.2 + y * 7.2 - t * 3.2 * flow) + n * 5.6);
	float plasma = smoothstep(0.02, 0.92, swirl) * wall_mask;

	float core_lance = spine * smoothstep(0.08, 0.72, y) * (1.0 - smoothstep(0.68, 0.96, y));
	core_lance *= 0.35 + 0.65 * smoothstep(0.62, 1.0, sin(y * 34.0 - t * 10.0 * flow + n * 3.0));

	vec2 star_cell = floor(vec2((x + 1.0) * 18.0, y * 56.0 - t * 10.0 * flow));
	vec2 star_f = fract(vec2((x + 1.0) * 18.0, y * 56.0 - t * 10.0 * flow));
	vec2 star_pos = vec2(hash(star_cell + 11.0), hash(star_cell + 33.0));
	float star_gate = step(0.955, hash(star_cell + 71.0));
	float sparks = smoothstep(0.038, 0.0, length((star_f - star_pos) * vec2(1.0, 0.50))) * star_gate * tube_mask;

	float mouth_energy = smoothstep(0.62, 0.96, y) * (1.0 - smoothstep(0.58, 0.96, side));
	float ship_gate = smoothstep(0.0, 0.10, y);
	float alpha = energy * tube_mask * ship_gate * perspective;

	vec3 color = vec3(0.0);
	color += mix(outer_color.rgb, secondary_color.rgb, 0.26) * wall_glow * 0.38;
	color += mix(outer_color.rgb, secondary_color.rgb, 0.38) * plasma * 0.62;
	color += outer_color.rgb * filaments * (0.82 + y * 0.62);
	color += core_color.rgb * depth_rings * 0.42;
	color += core_color.rgb * sparks * 1.55;
	color += core_color.rgb * core_lance * 0.52;
	color += core_color.rgb * mouth_energy * 0.45;
	color += outer_color.rgb * edge_wall * 0.72;

	float signal = wall_glow * 0.26 + plasma * 0.34 + filaments * 0.62 + depth_rings * 0.28 + edge_wall * 0.42 + sparks * 0.34 + mouth_energy * 0.26 + core_lance * 0.22;
	alpha *= clamp(signal, 0.0, 0.92);
	COLOR = vec4(color, alpha);
}
