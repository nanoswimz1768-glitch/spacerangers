shader_type canvas_item;
render_mode blend_add;

uniform float time_seconds = 0.0;
uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform float fade : hint_range(0.0, 1.0) = 1.0;
uniform float arriving : hint_range(0.0, 1.0) = 0.0;
uniform vec4 outer_color : source_color = vec4(0.16, 0.72, 1.0, 1.0);
uniform vec4 core_color : source_color = vec4(0.86, 1.0, 1.0, 1.0);
uniform vec4 secondary_color : source_color = vec4(0.54, 0.18, 1.0, 1.0);

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float streak_field(vec2 p, float speed, float cells_x, float cells_y, float seed) {
	vec2 space = vec2((p.x + 1.0) * cells_x, p.y * cells_y - time_seconds * speed);
	vec2 cell = floor(space);
	vec2 f = fract(space);
	vec2 anchor = vec2(hash(cell + seed), hash(cell + seed + 18.0));
	float gate = step(0.955, hash(cell + seed + 41.0));
	float head = smoothstep(0.034, 0.0, abs(f.x - anchor.x)) * smoothstep(0.030, 0.0, abs(f.y - anchor.y));
	float tail = smoothstep(0.030, 0.0, abs(f.x - anchor.x)) * smoothstep(0.28, 0.0, max(0.0, f.y - anchor.y));
	return gate * (head * 1.8 + tail * 0.56);
}

void fragment() {
	vec2 p = vec2(UV.x * 2.0 - 1.0, UV.y);
	float side = abs(p.x);
	float y = p.y;
	float flow = mix(1.0, -1.0, arriving);
	float open = smoothstep(0.02, 0.18, progress) * (1.0 - smoothstep(0.94, 1.0, progress));
	float tube = (1.0 - smoothstep(0.92, 1.0, side)) * smoothstep(0.04, 0.16, y) * (1.0 - smoothstep(0.90, 1.0, y));
	float wall_bias = smoothstep(0.08, 0.72, side);
	float center_bias = 1.0 - smoothstep(0.02, 0.42, side);

	float near_stars = streak_field(vec2(p.x, y), 24.0 * flow, 22.0, 74.0, 3.1) * wall_bias;
	float far_stars = streak_field(vec2(p.x * 0.74, y), 13.0 * flow, 16.0, 48.0, 29.7) * (0.36 + center_bias * 0.72);
	float micro = streak_field(vec2(p.x * 1.35, y), 38.0 * flow, 38.0, 112.0, 71.0) * 0.52;

	float depth = smoothstep(0.12, 0.92, y);
	float acceleration = 0.45 + smoothstep(0.18, 0.74, progress) * 0.55;
	float signal = near_stars * 0.82 + far_stars * 0.56 + micro * 0.44;
	float alpha = fade * open * tube * acceleration * clamp(signal, 0.0, 0.92);
	vec3 color = mix(core_color.rgb, outer_color.rgb, depth * 0.42) + secondary_color.rgb * 0.08;
	COLOR = vec4(color, alpha);
}
