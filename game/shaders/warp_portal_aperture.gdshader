shader_type canvas_item;
render_mode blend_mix;

uniform float time_seconds = 0.0;
uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform float fade : hint_range(0.0, 1.0) = 1.0;
uniform float arriving : hint_range(0.0, 1.0) = 0.0;
uniform float residual_level : hint_range(0.0, 1.0) = 0.0;
uniform vec4 outer_color : source_color = vec4(0.16, 0.72, 1.0, 1.0);
uniform vec4 core_color : source_color = vec4(0.86, 1.0, 1.0, 1.0);
uniform vec4 secondary_color : source_color = vec4(0.54, 0.18, 1.0, 1.0);

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	float r = length(uv);
	float a = atan(uv.y, uv.x);
	float swallow = smoothstep(0.44, 0.88, progress) * (1.0 - smoothstep(0.97, 1.0, progress));
	float release = (1.0 - smoothstep(0.04, 0.42, progress));
	float active = mix(swallow, release, arriving) * fade * (1.0 - residual_level);

	float wobble = sin(a * 7.0 + time_seconds * 2.7) * 0.025 + sin(a * 13.0 - time_seconds * 3.4) * 0.014;
	float aperture_radius = mix(0.20, 0.44, smoothstep(0.30, 0.88, progress)) + wobble;
	float core = 1.0 - smoothstep(aperture_radius * 0.38, aperture_radius, r);
	float lip = 1.0 - smoothstep(0.0, 0.050, abs(r - aperture_radius));
	float broken = 0.45 + smoothstep(-0.20, 0.92, sin(a * 9.0 - time_seconds * 4.2 + hash(vec2(floor(a * 6.0), 1.0)) * 2.0)) * 0.55;
	float outside_clip = 1.0 - smoothstep(0.78, 0.98, r);

	vec3 dark = vec3(0.0, 0.003, 0.012);
	vec3 rim = mix(outer_color.rgb, secondary_color.rgb, 0.36);
	vec3 color = mix(dark, rim, lip * 0.18);
	color += core_color.rgb * lip * broken * 0.20;

	float alpha = active * outside_clip * clamp(core * 0.68 + lip * broken * 0.46, 0.0, 0.78);
	COLOR = vec4(color, alpha);
}
