shader_type canvas_item;
render_mode blend_add;

uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform float seed = 0.0;
uniform float sun_burn : hint_range(0.0, 1.0) = 0.0;
uniform float alpha_scale : hint_range(0.0, 2.0) = 1.0;
uniform float energy : hint_range(0.0, 2.0) = 1.0;
uniform vec4 hot_color : source_color = vec4(1.0, 0.86, 0.28, 1.0);
uniform vec4 mid_color : source_color = vec4(1.0, 0.25, 0.04, 1.0);

float hash21(vec2 p) {
	p = fract(p * vec2(234.17, 389.41));
	p += dot(p, p + vec2(37.19));
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
	float amp = 0.56;
	float norm = 0.0;
	mat2 rot = mat2(vec2(0.76, -0.65), vec2(0.65, 0.76));
	for (int i = 0; i < 4; i++) {
		total += value_noise(p) * amp;
		norm += amp;
		p = rot * p * 2.08 + vec2(11.3, 29.1);
		amp *= 0.50;
	}
	return total / norm;
}

void fragment() {
	vec2 p = UV * 2.0 - vec2(1.0);
	float r = length(p);
	float a = atan(p.y, p.x);
	float t = clamp(progress, 0.0, 1.0);
	float n = fbm(p * 4.1 + vec2(seed + t * 1.7, seed * 0.33 - t * 0.9));
	float radial = fbm(vec2(a * 1.55 + seed * 0.27, r * 3.8 - t * 2.0));
	float ragged = (n - 0.5) * 0.22 + (radial - 0.5) * 0.20;
	float flash_fade = 1.0 - smoothstep(0.05, mix(0.34, 0.52, sun_burn), t);
	float expansion = mix(0.25, mix(0.95, 0.78, sun_burn), smoothstep(0.0, 0.72, t));

	float core = 1.0 - smoothstep(0.06, 0.36 + t * 0.30 + ragged * 0.18, r);
	float hot_shell = smoothstep(expansion * 0.20, expansion * 0.48, r + ragged) *
		(1.0 - smoothstep(expansion * 0.60, expansion * 1.02, r + ragged * 0.55));
	float vapor = (1.0 - smoothstep(0.24, 1.05, r + ragged * 0.45)) * (1.0 - smoothstep(0.42, 0.98, t));
	float impact_heat = 1.0 - sun_burn;
	float alpha = (
		core * flash_fade * mix(0.22, 0.92, sun_burn) +
		hot_shell * mix(0.08, 0.38, sun_burn) +
		vapor * sun_burn * 0.18
	) * alpha_scale * energy;
	alpha *= smoothstep(1.34, 0.08, r + ragged * 0.35);
	vec3 color = mix(mid_color.rgb, hot_color.rgb, clamp(core * mix(0.45, 1.0, sun_burn) + radial * 0.25 * (1.0 - impact_heat * 0.35), 0.0, 1.0));
	COLOR = vec4(color, clamp(alpha, 0.0, 1.0));
}
