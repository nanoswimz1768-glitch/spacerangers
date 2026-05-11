shader_type canvas_item;
render_mode blend_mix;

uniform float time_seconds = 0.0;
uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform float fade : hint_range(0.0, 1.0) = 0.0;
uniform float arriving : hint_range(0.0, 1.0) = 0.0;
uniform vec2 focus_uv = vec2(0.5, 0.42);

uniform float speed : hint_range(0.0, 10.0) = 2.7;
uniform float intensity : hint_range(0.0, 4.0) = 1.35;
uniform float streaks : hint_range(4.0, 96.0) = 58.0;
uniform float tunnel_size : hint_range(0.02, 0.8) = 0.16;
uniform float twist : hint_range(-10.0, 10.0) = 3.2;

uniform vec4 dark_color : source_color = vec4(0.002, 0.004, 0.018, 1.0);
uniform vec4 deep_color : source_color = vec4(0.03, 0.04, 0.13, 1.0);
uniform vec4 warp_color : source_color = vec4(0.18, 0.64, 1.0, 1.0);
uniform vec4 secondary_color : source_color = vec4(0.52, 0.16, 0.95, 1.0);
uniform vec4 hot_color : source_color = vec4(0.92, 0.98, 1.0, 1.0);

uniform float flash_progress : hint_range(0.0, 1.0) = 1.0;
uniform float flash_strength : hint_range(0.0, 1.0) = 0.0;

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
		p = mat2(vec2(1.62, 1.12), vec2(-1.12, 1.62)) * p + vec2(7.1, 3.8);
		amp *= 0.52;
	}
	return v;
}

float band(float value, float width) {
	return 1.0 - smoothstep(0.0, width, abs(value));
}

void fragment() {
	vec2 uv = UV - focus_uv;
	uv.x *= SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;

	float r = length(uv);
	float a = atan(uv.y, uv.x);
	float t = time_seconds * speed;
	float flow = mix(1.0, -1.0, arriving);
	float inv_r = 1.0 / max(r, 0.028);

	float open = smoothstep(0.02, 0.18, progress) * (1.0 - smoothstep(0.92, 1.0, progress));
	float energy = fade * open;

	float uv_angle = (a + 3.14159265) / 6.2831853;
	float angular_cell = floor(uv_angle * streaks);
	float angular_noise = hash(vec2(angular_cell, floor(inv_r * 0.85)));
	float organic_wobble = fbm(vec2(a * 1.8 + t * 0.12, inv_r * 0.22 - t * 0.18)) - 0.5;

	float spiral_phase = a * streaks
		+ inv_r * 2.85
		- t * 8.8 * flow
		+ sin(inv_r * 0.74 - t * 0.72) * twist
		+ organic_wobble * 4.8;
	float spiral = sin(spiral_phase);
	float rays = pow(smoothstep(0.92 - angular_noise * 0.035, 1.0, spiral), 2.2);
	rays *= 0.28 + angular_noise * 0.65;
	rays *= smoothstep(tunnel_size * 1.35, 0.52, r);

	float second_spiral = sin(a * (streaks * 0.42) - inv_r * 3.3 + t * 5.6 * flow);
	float mist_noise = fbm(vec2(cos(a + inv_r * 0.09), sin(a - inv_r * 0.08)) * (2.4 + r * 2.1) + vec2(t * 0.14, -t * 0.10));
	float mist = smoothstep(0.08, 0.96, second_spiral + mist_noise * 0.86 - r * 0.28) * 0.42;
	float arm_phase = sin(a * 5.5 + inv_r * 1.75 - t * 3.4 * flow + mist_noise * 4.8);
	float arms = smoothstep(-0.12, 0.92, arm_phase) * smoothstep(0.04, 0.78, r) * (1.0 - smoothstep(0.90, 1.48, r));

	float rings = sin(inv_r * 5.2 - t * 11.5 * flow + organic_wobble * 2.6);
	rings = smoothstep(0.82, 1.0, rings);

	vec2 star_space = vec2(uv_angle * 92.0, inv_r * 7.2 - t * 5.4 * flow);
	vec2 star_cell = floor(star_space);
	vec2 star_f = fract(star_space);
	vec2 star_pos = vec2(hash(star_cell + 13.7), hash(star_cell + 91.3));
	float star_gate = step(0.955, hash(star_cell + 42.0));
	float star_dot = smoothstep(0.060, 0.0, length((star_f - star_pos) * vec2(1.0, 0.48)));
	float stars = star_dot * star_gate;
	stars *= smoothstep(0.20, 0.88, r) * (1.0 - smoothstep(1.08, 1.52, r));

	float core = 1.0 - smoothstep(0.0, tunnel_size, r);
	float deep_center = smoothstep(tunnel_size * 0.42, tunnel_size * 1.25, r);
	float edge_fade = 1.0 - smoothstep(0.82, 1.46, r);
	float depth_fade = clamp(1.38 - r, 0.0, 1.0);

	float flash_radius = mix(0.035, 0.72, smoothstep(0.0, 1.0, flash_progress));
	float shockwave = band(r - flash_radius, 0.018 + flash_progress * 0.026) * flash_strength;

	vec3 plasma_color = mix(warp_color.rgb, secondary_color.rgb, smoothstep(0.18, 0.95, mist_noise) * 0.62);
	vec3 col = mix(dark_color.rgb, deep_color.rgb, edge_fade * 0.55) * (0.20 + edge_fade * 0.30);
	col += plasma_color * arms * depth_fade * 0.55 * intensity;
	col += warp_color.rgb * rays * depth_fade * 0.34 * intensity;
	col += plasma_color * mist * depth_fade * 1.18 * intensity;
	col += warp_color.rgb * rings * depth_fade * 0.18 * intensity;
	col += hot_color.rgb * stars * 1.80 * intensity;
	col += hot_color.rgb * core * 2.70 * intensity;
	col += mix(warp_color.rgb, hot_color.rgb, 0.55) * shockwave * 2.2;

	float vignette = 0.28 + edge_fade * 1.16;
	col *= vignette;
	col *= mix(0.44, 1.0, deep_center);

	float tunnel_alpha = clamp(
		0.12 +
		rays * 0.16 +
		arms * 0.18 +
		mist * 0.34 +
		rings * 0.05 +
		core * 0.60 +
		stars * 0.35 +
		shockwave * 0.42,
		0.0,
		0.88);
	float alpha = clamp(fade * tunnel_alpha + flash_strength * 0.10, 0.0, 0.92);

	COLOR = vec4(col, alpha);
}
