shader_type canvas_item;
render_mode blend_add;

uniform float time_seconds = 0.0;
uniform float charge : hint_range(0.0, 1.0) = 0.0;
uniform float intensity : hint_range(0.0, 2.0) = 0.0;
uniform float transit : hint_range(0.0, 1.0) = 0.0;
uniform float ready : hint_range(0.0, 1.0) = 0.0;
uniform vec4 outer_color : source_color = vec4(0.12, 0.82, 1.0, 1.0);
uniform vec4 core_color : source_color = vec4(0.86, 1.0, 1.0, 1.0);

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float band(float value, float width) {
	return 1.0 - smoothstep(0.0, width, abs(value));
}

void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	uv.y *= 1.08;
	float r = length(uv);
	float a = atan(uv.y, uv.x);
	float t = time_seconds;
	float build = smoothstep(0.02, 0.25, charge);
	float live = intensity * build;

	float breathe = 0.5 + 0.5 * sin(t * (3.0 + charge * 2.2));
	float inner = 1.0 - smoothstep(0.10, 0.58, r);
	float field = (1.0 - smoothstep(0.42, 0.98, r)) * smoothstep(0.06, 0.20, r);
	float ready_ring = band(r - (0.58 + breathe * 0.026), 0.022 + transit * 0.014) * (ready * 0.72 + transit * 0.80);

	float spiral_a = a + sin(r * 7.4 - t * 2.1) * (0.34 + charge * 0.16);
	float arc_wave = sin(spiral_a * 5.0 - t * (2.4 + charge * 2.2)) + sin(a * 9.0 + r * 10.0 + t * 1.6) * 0.35;
	float arcs = smoothstep(0.34, 1.0, arc_wave) * band(r - (0.67 + sin(a * 3.0 + t) * 0.026), 0.072);
	float filament_wave = sin(spiral_a * 8.0 + r * 19.0 - t * (7.0 + charge * 4.0));
	float filaments = smoothstep(0.76, 1.0, filament_wave);
	filaments *= smoothstep(0.18, 0.42, r) * (1.0 - smoothstep(0.82, 1.02, r));

	float nose = smoothstep(0.14, 0.78, -uv.y) * (1.0 - smoothstep(0.06, 0.32, abs(uv.x)));
	float nose_pulse = smoothstep(0.72, 1.0, sin((-uv.y) * 8.0 - t * (7.0 + charge * 4.0)));
	float core_stream = nose * nose_pulse * (0.30 + charge * 0.70);
	float nose_lance = nose * (1.0 - smoothstep(0.02, 0.11 + charge * 0.03, abs(uv.x)));
	nose_lance *= smoothstep(0.08, 0.70, -uv.y) * (1.0 - smoothstep(0.72, 0.95, -uv.y));

	vec2 spark_space = vec2((a + 3.14159265) * 6.0, r * 16.0 - t * (5.0 + charge * 7.0));
	vec2 spark_cell = floor(spark_space);
	vec2 spark_f = fract(spark_space);
	float gate = step(0.940 - charge * 0.030 - transit * 0.035, hash(spark_cell + 19.0));
	float sparks = smoothstep(0.050, 0.0, length((spark_f - vec2(hash(spark_cell + 3.0), hash(spark_cell + 7.0))) * vec2(1.0, 0.55))) * gate;
	sparks *= smoothstep(0.28, 0.78, r) * (1.0 - smoothstep(0.82, 1.0, r));

	float scan = band(fract((uv.y + 1.0) * 7.0 - t * (1.6 + charge * 2.4)) - 0.5, 0.035);
	scan *= (1.0 - smoothstep(0.50, 0.96, r)) * smoothstep(0.06, 0.34, r);
	float core_glow = (1.0 - smoothstep(0.04, 0.30, r)) * (0.42 + ready * 0.44 + transit * 0.50);

	vec3 color = outer_color.rgb * field * (0.48 + charge * 0.66);
	color += outer_color.rgb * arcs * (1.05 + charge * 0.72);
	color += mix(outer_color.rgb, core_color.rgb, 0.28) * filaments * (0.98 + charge * 0.88);
	color += mix(outer_color.rgb, core_color.rgb, 0.52) * ready_ring * 1.28;
	color += core_color.rgb * core_stream * 0.94;
	color += core_color.rgb * nose_lance * (0.24 + charge * 0.42 + ready * 0.24);
	color += mix(outer_color.rgb, core_color.rgb, 0.68) * sparks * 2.00;
	color += mix(outer_color.rgb, core_color.rgb, 0.40) * scan * (0.20 + ready * 0.36);
	color += core_color.rgb * core_glow * 0.24;
	color += core_color.rgb * inner * transit * 0.30;

	float signal = field * 0.34 + arcs * 0.62 + filaments * 0.56 + ready_ring * 0.78 + core_stream * 0.48 + nose_lance * 0.30 + sparks * 0.68 + scan * 0.28 + core_glow * 0.30 + inner * transit * 0.18;
	float alpha = live * clamp(signal * 1.72, 0.0, 0.98);
	COLOR = vec4(color * (1.0 + ready * 0.20 + transit * 0.16), alpha);
}
