shader_type canvas_item;
render_mode unshaded, blend_mix;

uniform sampler2D surface_map : repeat_enable, filter_linear_mipmap;
uniform sampler2D clouds_map : repeat_enable, filter_linear_mipmap;
uniform float time_seconds = 0.0;
uniform float daylight = 1.0;
uniform vec4 atmosphere_color : source_color = vec4(0.22, 0.64, 1.0, 1.0);

const float EARTH_PI = 3.14159265359;
const float EARTH_TAU = 6.28318530718;

void fragment() {
	vec2 disk = UV * 2.0 - 1.0;
	float radius_sq = dot(disk, disk);
	if (radius_sq > 1.0) {
		discard;
	}

	float z = sqrt(max(0.0, 1.0 - radius_sq));
	vec3 normal = normalize(vec3(disk.x, disk.y, z));
	float longitude = atan(disk.x, z) / EARTH_TAU + 0.5 + time_seconds * 0.018;
	float latitude = asin(clamp(-disk.y, -1.0, 1.0));
	float map_y = 0.5 - latitude / EARTH_PI;
	vec2 surface_uv = vec2(longitude, map_y);

	vec3 surface = texture(surface_map, surface_uv).rgb;

	vec2 cloud_uv = vec2(longitude + time_seconds * 0.030, map_y + sin(longitude * EARTH_TAU * 2.0) * 0.004);
	float clouds = texture(clouds_map, cloud_uv).r;

	vec3 light_dir = normalize(vec3(-0.52, -0.34, 0.78));
	float diffuse = dot(normal, light_dir);
	float day = smoothstep(-0.22, 0.50, diffuse) * daylight;
	float shade = mix(0.18, 1.06, day);

	vec3 color = surface * shade;
	color = mix(color, vec3(0.86, 0.93, 1.0), clouds * 0.46 * day);

	float edge = sqrt(radius_sq);
	float rim = pow(smoothstep(0.58, 1.0, edge), 2.1);
	color += atmosphere_color.rgb * rim * (0.18 + day * 0.34);

	float alpha = 1.0 - smoothstep(0.982, 1.0, edge);
	COLOR = vec4(color, alpha);
}
