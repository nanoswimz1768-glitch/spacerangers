shader_type canvas_item;
render_mode unshaded, blend_mix;

uniform sampler2D surface_map : repeat_enable, filter_linear;
uniform float time_seconds = 0.0;
uniform float daylight = 1.0;
uniform float rotation_speed = 0.012;
uniform float flow_strength = 0.0;
uniform float atmosphere_strength = 0.18;
uniform float contrast = 1.0;
uniform float saturation = 1.0;
uniform vec4 atmosphere_color : source_color = vec4(0.35, 0.70, 1.0, 1.0);

const float PLANET_PI = 3.14159265359;
const float PLANET_TAU = 6.28318530718;

vec3 apply_saturation(vec3 color, float amount) {
	float gray = dot(color, vec3(0.299, 0.587, 0.114));
	return mix(vec3(gray), color, amount);
}

void fragment() {
	vec2 disk = UV * 2.0 - 1.0;
	float radius_sq = dot(disk, disk);
	if (radius_sq > 1.0) {
		discard;
	}

	float z = sqrt(max(0.0, 1.0 - radius_sq));
	vec3 normal = normalize(vec3(disk.x, disk.y, z));
	float longitude = atan(disk.x, z) / PLANET_TAU + 0.5 + time_seconds * rotation_speed;
	float latitude = asin(clamp(-disk.y, -1.0, 1.0));
	float map_y = 0.5 - latitude / PLANET_PI;
	float band_flow = sin(map_y * PLANET_TAU * 7.0 + time_seconds * 0.16) * flow_strength;
	vec2 surface_uv = vec2(longitude + band_flow, clamp(map_y, 0.002, 0.998));

	vec3 surface = texture(surface_map, surface_uv).rgb;
	surface = (surface - vec3(0.5)) * contrast + vec3(0.5);
	surface = apply_saturation(surface, saturation);
	surface = clamp(surface, vec3(0.0), vec3(1.25));

	vec3 light_dir = normalize(vec3(-0.52, -0.34, 0.78));
	float diffuse = dot(normal, light_dir);
	float day = smoothstep(-0.24, 0.54, diffuse) * daylight;
	float shade = mix(0.17, 1.08, day);

	float edge = sqrt(radius_sq);
	float limb = smoothstep(1.0, 0.14, edge);
	vec3 color = surface * shade * mix(0.82, 1.0, limb);

	float rim = pow(smoothstep(0.50, 1.0, edge), 2.0);
	color += atmosphere_color.rgb * rim * atmosphere_strength * (0.34 + day * 0.70);

	float alpha = 1.0 - smoothstep(0.982, 1.0, edge);
	COLOR = vec4(clamp(color, vec3(0.0), vec3(1.25)), alpha);
}
