shader_type spatial;
// Terreno por desplazamiento con ruido (Luna / Marte). Plano llano cerca del origen
// para que el astronauta y los módulos se apoyen.

uniform sampler2D height_map;
uniform float height_scale = 6.0;
uniform float plane_size = 400.0;
uniform float flat_radius = 14.0;
uniform vec4 color_low : source_color = vec4(0.35, 0.35, 0.36, 1.0);
uniform vec4 color_high : source_color = vec4(0.62, 0.62, 0.60, 1.0);

varying float h;

float height_at(vec2 uv, vec2 world_xz) {
	float raw = texture(height_map, uv).r;
	float mask = smoothstep(flat_radius, flat_radius * 2.5, length(world_xz));
	return raw * height_scale * mask;
}

void vertex() {
	float e = 1.0 / 256.0;
	float step_world = e * plane_size;
	vec2 xz = VERTEX.xz;
	float hc = height_at(UV, xz);
	float hx = height_at(UV + vec2(e, 0.0), xz + vec2(step_world, 0.0));
	float hz = height_at(UV + vec2(0.0, e), xz + vec2(0.0, step_world));
	h = texture(height_map, UV).r;
	VERTEX.y += hc;
	NORMAL = normalize(vec3(hc - hx, step_world, hc - hz));
}

void fragment() {
	ALBEDO = mix(color_low.rgb, color_high.rgb, h);
	ROUGHNESS = 0.97;
	SPECULAR = 0.1;
}
