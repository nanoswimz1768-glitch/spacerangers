shader_type canvas_item;
render_mode unshaded, blend_mix;

uniform sampler2D frame_prev : repeat_disable, filter_linear_mipmap;
uniform sampler2D frame_current : repeat_disable, filter_linear_mipmap;
uniform sampler2D frame_next : repeat_disable, filter_linear_mipmap;
uniform sampler2D frame_after_next : repeat_disable, filter_linear_mipmap;

uniform float frame_blend = 0.0;
uniform float rim_strength = 0.82;
uniform float rim_inner_start = 0.72;
uniform float rim_inner_full = 0.82;
uniform float rim_outer_fade = 0.95;
uniform float rim_outer_end = 1.04;
uniform vec4 disk_tint : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float tint_strength = 0.0;

vec4 to_premul(vec4 color) {
	return vec4(color.rgb * color.a, color.a);
}

vec4 from_premul(vec4 color) {
	if (color.a <= 0.0001) {
		return vec4(0.0);
	}

	return vec4(color.rgb / color.a, color.a);
}

vec4 temporal_weight(vec4 previous, vec4 current, vec4 next) {
	return previous * 0.23 + current * 0.54 + next * 0.23;
}

void fragment() {
	vec4 previous = to_premul(texture(frame_prev, UV));
	vec4 current = to_premul(texture(frame_current, UV));
	vec4 next = to_premul(texture(frame_next, UV));
	vec4 after_next = to_premul(texture(frame_after_next, UV));

	vec4 normal = mix(current, next, frame_blend);
	vec4 stable_current = temporal_weight(previous, current, next);
	vec4 stable_next = temporal_weight(current, next, after_next);
	vec4 stable = mix(stable_current, stable_next, frame_blend);

	float radius = length(UV * 2.0 - 1.0);
	float rim_mask = smoothstep(rim_inner_start, rim_inner_full, radius)
		* (1.0 - smoothstep(rim_outer_fade, rim_outer_end, radius))
		* rim_strength;

	vec4 color = from_premul(mix(normal, stable, rim_mask));
	vec3 multiplied = color.rgb * disk_tint.rgb;
	float energy = max(max(color.r, color.g), color.b);
	vec3 colorized = disk_tint.rgb * energy * 1.12;
	color.rgb = mix(multiplied, colorized, clamp(tint_strength, 0.0, 1.0));
	color.a *= disk_tint.a;
	COLOR = color;
}
