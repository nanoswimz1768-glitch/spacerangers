shader_type canvas_item;
render_mode blend_add;

uniform float time_seconds = 0.0;
uniform float charge : hint_range(0.0, 1.0) = 0.0;
uniform float intensity : hint_range(0.0, 2.0) = 0.0;
uniform float transit : hint_range(0.0, 1.0) = 0.0;
uniform float ready : hint_range(0.0, 1.0) = 0.0;
uniform sampler2D ship_texture : repeat_disable, filter_linear_mipmap;
uniform vec2 ship_texture_pixel_size = vec2(0.00390625, 0.00390625);
uniform vec4 outer_color : source_color = vec4(0.12, 0.82, 1.0, 1.0);
uniform vec4 core_color : source_color = vec4(0.62, 1.0, 1.0, 1.0);

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float alpha_at(vec2 uv) {
	if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) {
		return 0.0;
	}
	return texture(ship_texture, uv).a;
}

void fragment() {
	vec4 source = texture(ship_texture, UV);
	float alpha = source.a;
	float live = intensity * smoothstep(0.02, 0.24, charge + transit);

	float t = time_seconds;
	float edge_width = 1.65 + charge * 1.95 + transit * 1.20;
	vec2 px = ship_texture_pixel_size * edge_width;

	float a_l = alpha_at(UV + vec2(-px.x, 0.0));
	float a_r = alpha_at(UV + vec2(px.x, 0.0));
	float a_u = alpha_at(UV + vec2(0.0, -px.y));
	float a_d = alpha_at(UV + vec2(0.0, px.y));
	float a_ul = alpha_at(UV + vec2(-px.x, -px.y));
	float a_ur = alpha_at(UV + vec2(px.x, -px.y));
	float a_dl = alpha_at(UV + vec2(-px.x, px.y));
	float a_dr = alpha_at(UV + vec2(px.x, px.y));

	float max_neighbor = max(max(max(a_l, a_r), max(a_u, a_d)), max(max(a_ul, a_ur), max(a_dl, a_dr)));
	float min_neighbor = min(min(min(a_l, a_r), min(a_u, a_d)), min(min(a_ul, a_ur), min(a_dl, a_dr)));
	float outer_rim = max(max_neighbor - alpha, 0.0);
	float inner_rim = alpha * max(alpha - min_neighbor, 0.0);
	float hull = smoothstep(0.08, 0.62, alpha);

	float flow_down = sin(UV.y * 20.0 + UV.x * 5.0 - t * (4.2 + charge * 2.0));
	float flow_cross = sin((UV.x - UV.y) * 16.0 + t * (2.2 + charge * 2.8));
	float flow = smoothstep(0.74, 1.0, flow_down + flow_cross * 0.28);
	flow *= hull * (0.18 + charge * 0.32 + ready * 0.18);

	float side_balance = 1.0 - smoothstep(0.34, 0.58, abs(UV.x - 0.5));
	float nose_bias = smoothstep(0.82, 0.18, UV.y) * side_balance * hull;
	float nose_pulse = smoothstep(0.58, 1.0, sin((1.0 - UV.y) * 12.0 - t * (5.6 + charge * 2.5)));

	vec2 cell = floor(UV * vec2(34.0, 42.0) + vec2(0.0, -t * (1.8 + charge * 2.2)));
	float spark_gate = step(0.985 - charge * 0.022 - transit * 0.018, hash(cell));
	float sparks = spark_gate * hull * (outer_rim + inner_rim * 0.65);

	float ready_breathe = 0.55 + 0.45 * sin(t * 7.0);
	float rim = outer_rim * (0.78 + ready * 0.20) + inner_rim * (0.44 + ready * 0.16);
	float surface = flow * 0.30 + nose_bias * nose_pulse * (0.07 + charge * 0.14 + ready * 0.11);
	float signal = rim * (0.22 + charge * 0.36 + transit * 0.42 + ready * ready_breathe * 0.16)
		+ surface
		+ sparks * (0.28 + ready * 0.18 + transit * 0.20);

	vec3 color = outer_color.rgb * rim * (0.62 + charge * 0.48 + transit * 0.34);
	color += mix(outer_color.rgb, core_color.rgb, 0.34) * surface * (0.88 + charge * 0.42);
	color += core_color.rgb * sparks * 1.22;

	float final_alpha = live * clamp(signal * 1.28, 0.0, 0.62 + transit * 0.12);
	COLOR = vec4(color, final_alpha);
}
