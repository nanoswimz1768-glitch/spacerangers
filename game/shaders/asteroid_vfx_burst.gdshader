shader_type canvas_item;
render_mode blend_mix;

uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform float seed = 0.0;
uniform float sun_burn : hint_range(0.0, 1.0) = 0.0;
uniform float alpha_scale : hint_range(0.0, 2.0) = 1.0;
uniform float energy : hint_range(0.0, 2.0) = 1.0;
uniform vec4 hot_color : source_color = vec4(1.0, 0.78, 0.30, 1.0);
uniform vec4 mid_color : source_color = vec4(0.86, 0.45, 0.16, 1.0);
uniform vec4 dust_color : source_color = vec4(0.42, 0.36, 0.29, 1.0);
uniform vec4 dark_color : source_color = vec4(0.10, 0.09, 0.08, 1.0);

float hash21(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + vec2(45.32));
	return fract(p.x * p.y);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	float a = hash21(i);
	float b = hash21(i + vec2(1.0, 0.0));
	float c = hash21(i + vec2(0.0, 1.0));
	float d = hash21(i + vec2(1.0, 1.0));
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm(vec2 p) {
	float total = 0.0;
	float amp = 0.54;
	float norm = 0.0;
	mat2 rot = mat2(vec2(0.82, -0.57), vec2(0.57, 0.82));
	for (int i = 0; i < 5; i++) {
		total += value_noise(p) * amp;
		norm += amp;
		p = rot * p * 2.03 + vec2(17.1, 9.7);
		amp *= 0.52;
	}
	return total / norm;
}

void fragment() {
	vec2 p = UV * 2.0 - vec2(1.0);
	float r = length(p);
	float a = atan(p.y, p.x);
	float t = clamp(progress, 0.0, 1.0);
	float open = smoothstep(0.0, 0.16, t);
	float fade = 1.0 - smoothstep(0.70, 1.0, t);
	float n = fbm(p * 3.4 + vec2(seed * 1.37 + t * 0.95, seed * 0.71 - t * 0.58));
	float radial = fbm(vec2(a * 1.28 + seed * 0.31, r * 3.2 - t * 1.6));
	float ragged = (n - 0.5) * 0.28 + (radial - 0.5) * 0.22;
	float blast = smoothstep(0.0, 0.78, t);
	float expansion = mix(0.32, mix(1.10, 0.88, sun_burn), blast);

	float hot_core = 1.0 - smoothstep(0.11 + t * 0.05, 0.42 + t * 0.34 + ragged * 0.22, r);
	hot_core *= 1.0 - smoothstep(0.0, mix(0.30, 0.44, sun_burn), t);

	float shell_inner = smoothstep(expansion * 0.26, expansion * 0.58, r + ragged * 0.78);
	float shell_outer = 1.0 - smoothstep(expansion * 0.70, expansion * 1.12, r + ragged * 0.54);
	float shell = shell_inner * shell_outer;

	float smoke_body = (1.0 - smoothstep(0.18, 1.18, r + ragged * 0.50)) * open * fade;
	float chipped_edge = smoothstep(0.33, 0.90, n + radial * 0.32);
	float impact_dust = max(shell * 0.74, smoke_body * 0.46);
	float solar_dust = smoke_body * 0.32 + hot_core * 0.16;
	float dust_alpha = mix(impact_dust, solar_dust, sun_burn) * chipped_edge;
	float ember_strength = mix(0.18, 1.0, sun_burn);
	float ember = hot_core * ember_strength * (0.75 + radial * 0.35 + sun_burn * 0.26) * energy;

	vec3 dust_mix = mix(dark_color.rgb, dust_color.rgb, clamp(n * 1.35, 0.0, 1.0));
	vec3 color = mix(dust_mix, mid_color.rgb, shell * mix(0.22, 0.60, sun_burn));
	color = mix(color, hot_color.rgb, clamp(ember + sun_burn * hot_core * 0.35, 0.0, 1.0));

	float alpha = (dust_alpha + ember * mix(0.20, 0.72, sun_burn)) * alpha_scale;
	alpha *= 1.0 - smoothstep(1.28, 1.46, r + ragged * 0.42);
	COLOR = vec4(color, clamp(alpha, 0.0, 1.0));
}
