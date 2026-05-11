shader_type canvas_item;
render_mode blend_add;

uniform float time_seconds = 0.0;
uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform float fade : hint_range(0.0, 1.0) = 1.0;
uniform float arriving : hint_range(0.0, 1.0) = 0.0;
uniform vec4 outer_color : source_color = vec4(0.16, 0.72, 1.0, 1.0);
uniform vec4 core_color : source_color = vec4(0.86, 1.0, 1.0, 1.0);
uniform vec4 secondary_color : source_color = vec4(0.48, 0.18, 0.94, 1.0);

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
		p = mat2(vec2(1.55, 1.08), vec2(-1.08, 1.55)) * p + vec2(6.1, 4.4);
		amp *= 0.52;
	}
	return v;
}

void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	float r = length(uv);
	float a = atan(uv.y, uv.x);
	float t = time_seconds;
	float flow = mix(1.0, -1.0, arriving);
	float open = smoothstep(0.02, 0.20, progress) * (1.0 - smoothstep(0.94, 1.0, progress));

	float n = fbm(vec2(cos(a) * 2.0, sin(a) * 2.0) + vec2(t * 0.22, -t * 0.12) + r * 3.0);
	float spiral = sin(a * 8.0 + (1.0 / max(r, 0.035)) * 2.7 - t * 6.3 * flow + n * 6.6);
	float spiral_gate = smoothstep(0.14, 0.94, spiral);
	float arms = spiral_gate * smoothstep(0.08, 0.22, r) * (1.0 - smoothstep(0.74, 0.98, r));
	float rings = smoothstep(0.80, 1.0, sin((1.0 / max(r, 0.04)) * 6.6 - t * 11.5 * flow + n * 3.1));
	rings *= smoothstep(0.12, 0.26, r) * (1.0 - smoothstep(0.84, 0.98, r));
	float core = 1.0 - smoothstep(0.0, 0.15, r);
	float glow = (1.0 - smoothstep(0.16, 0.64, r)) * smoothstep(0.02, 0.16, r);
	float rim_noise = n * 0.075 + sin(a * 5.0 - t * 1.8 * flow) * 0.025;
	float rim = 1.0 - smoothstep(0.0, 0.065, abs(r - (0.70 + rim_noise)));
	float edge = 1.0 - smoothstep(0.90, 1.0, r);
	float outside_clip = 1.0 - smoothstep(0.985, 1.0, r);

	vec2 star_cell = floor(vec2(a * 18.0, r * 18.0 - t * 5.0 * flow));
	float spark_gate = step(0.968, hash(star_cell + 15.0));
	float sparks = spark_gate * smoothstep(0.015, 0.0, abs(fract(r * 18.0 - t * 5.0 * flow) - hash(star_cell + 31.0)));
	sparks *= smoothstep(0.20, 0.38, r) * (1.0 - smoothstep(0.76, 0.96, r));

	vec3 plasma = mix(outer_color.rgb, secondary_color.rgb, smoothstep(0.20, 0.86, n) * 0.58);
	vec3 color = vec3(0.0);
	color += plasma * arms * 1.35;
	color += outer_color.rgb * rings * 0.42;
	color += secondary_color.rgb * rim * 0.76;
	color += core_color.rgb * core * 2.9;
	color += core_color.rgb * glow * 0.72;
	color += core_color.rgb * sparks * 1.35;

	float signal = arms * 0.66 + rings * 0.20 + rim * 0.34 + core * 0.72 + glow * 0.28 + sparks * 0.36;
	float alpha = fade * open * edge * outside_clip * clamp(signal, 0.0, 0.94);
	COLOR = vec4(color, alpha);
}
