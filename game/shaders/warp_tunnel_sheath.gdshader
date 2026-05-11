shader_type canvas_item;
render_mode blend_mix;

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
		p = mat2(vec2(1.62, 1.04), vec2(-1.04, 1.62)) * p + vec2(3.9, 6.2);
		amp *= 0.52;
	}
	return v;
}

void fragment() {
	vec2 p = vec2(UV.x * 2.0 - 1.0, UV.y);
	float x = p.x;
	float y = p.y;
	float side = abs(x);
	float flow = mix(1.0, -1.0, arriving);
	float t = time_seconds;
	float open = smoothstep(0.02, 0.18, progress) * (1.0 - smoothstep(0.94, 1.0, progress));
	float energy = open * fade;

	float perspective = smoothstep(0.02, 0.18, y) * (1.0 - smoothstep(0.88, 1.0, y));
	float soft_edge = 1.0 - smoothstep(0.84, 1.0, side);
	float hollow = smoothstep(0.08, 0.36, side);
	float wall = smoothstep(0.18, 0.80, side) * soft_edge;

	float n = fbm(vec2(x * 2.6 + t * 0.18, y * 7.4 - t * 1.28 * flow));
	float smoke = smoothstep(0.26, 0.82, n + sin(y * 12.0 - t * 2.4 * flow + x * 1.5) * 0.16);
	float rib = smoothstep(0.84, 1.0, sin(y * 30.0 - t * 8.0 * flow + n * 4.0)) * wall;
	float intake = smoothstep(0.0, 0.22, y) * (1.0 - smoothstep(0.34, 0.92, side));

	vec3 base = mix(vec3(0.004, 0.018, 0.040), outer_color.rgb, 0.50);
	vec3 plasma = mix(base, secondary_color.rgb, smoke * 0.34);
	plasma = mix(plasma, core_color.rgb, rib * 0.30);

	float signal = smoke * (wall * 0.64 + hollow * 0.24) + rib * 0.38 + intake * 0.22;
	float alpha = energy * perspective * soft_edge * clamp(signal, 0.0, 0.46);
	COLOR = vec4(plasma, alpha);
}
