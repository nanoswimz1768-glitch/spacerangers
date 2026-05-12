shader_type canvas_item;
render_mode blend_mix;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform float time_seconds = 0.0;
uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform float fade : hint_range(0.0, 1.0) = 1.0;
uniform float arriving : hint_range(0.0, 1.0) = 0.0;
uniform float residual_level : hint_range(0.0, 1.0) = 0.0;
uniform vec4 outer_color : source_color = vec4(0.16, 0.72, 1.0, 1.0);
uniform vec4 core_color : source_color = vec4(0.86, 1.0, 1.0, 1.0);
uniform vec4 secondary_color : source_color = vec4(0.54, 0.18, 1.0, 1.0);

void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	float r = length(uv);
	float a = atan(uv.y, uv.x);
	float open = smoothstep(0.02, 0.18, progress) * (1.0 - smoothstep(0.96, 1.0, progress));
	float phase_pull = arriving > 0.5
		? 1.0 - smoothstep(0.32, 0.92, progress)
		: smoothstep(0.08, 0.72, progress);
	float residual = residual_level * fade;
	float energy = max(open * phase_pull * fade, residual * 0.55);

	float lens = (1.0 - smoothstep(0.08, 0.92, r)) * smoothstep(0.02, 0.20, r);
	float swirl = sin(a * 5.0 - time_seconds * 2.2 + r * 8.0) * 0.0045;
	float pull = (0.026 + residual * 0.018) * lens * energy;
	vec2 direction = normalize(uv + vec2(0.0001, 0.0001));
	vec2 distorted_uv = SCREEN_UV - direction * pull + vec2(-direction.y, direction.x) * swirl * energy;
	vec4 sample_col = texture(screen_texture, distorted_uv);

	float rim = 1.0 - smoothstep(0.0, 0.09, abs(r - (0.50 + sin(a * 4.0 + time_seconds) * 0.018)));
	float glow = (1.0 - smoothstep(0.12, 0.76, r)) * smoothstep(0.06, 0.18, r);
	vec3 tint = mix(outer_color.rgb, secondary_color.rgb, 0.22);
	vec3 color = sample_col.rgb + tint * rim * energy * 0.10 + core_color.rgb * glow * energy * 0.035;
	float alpha = clamp(energy * lens * 0.58 + rim * energy * 0.16, 0.0, 0.68);
	COLOR = vec4(color, alpha);
}
